using DefectInspection.Api.Data;
using DefectInspection.Api.Dtos;
using DefectInspection.Api.Models;
using DefectInspection.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DefectInspection.Api.Controllers;

[ApiController]
[Route("api/inspections")]
public class InspectionsController(AppDbContext db, IGeminiVisionService gemini, IWebHostEnvironment env,
    ILogger<InspectionsController> log) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };

    /// <summary>Step 1-3: upload image, run Gemini vision, store an UNCONFIRMED draft report.</summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<AnalyzeResponse>> Analyze([FromForm] AnalyzeRequest req, CancellationToken ct)
    {
        var file = req.Image!;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(ext, out var mime))
            return Problem("Only .jpg, .jpeg, .png, .webp images are supported.", statusCode: 415);
        if (file.Length == 0) return Problem("Uploaded file is empty.", statusCode: 400);
        if (file.Length > MaxBytes) return Problem("Image exceeds the 10 MB limit.", statusCode: 413);

        var product = await db.Products.FindAsync([req.ProductId], ct);
        if (product is null) return Problem($"Product {req.ProductId} not found.", statusCode: 404);
        var inspector = await db.Inspectors.FindAsync([req.InspectorId], ct);
        if (inspector is null) return Problem($"Inspector {req.InspectorId} not found.", statusCode: 404);

        // Read once into memory (stream starts at position 0) and reuse the bytes for both Base64 and disk.
        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await using var s = file.OpenReadStream();
            await s.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        if (!LooksLikeImage(bytes))
            return Problem("File content is not a valid JPEG/PNG/WebP image.", statusCode: 415);

        var categories = await db.DefectCategories.AsNoTracking().OrderBy(c => c.DefectCategoryId).ToListAsync(ct);

        GeminiResult ai;
        try
        {
            ai = await gemini.AnalyzeAsync(bytes, mime, file.FileName, categories, ct);
        }
        catch (ImageUnclearException ex)
        {
            // Nothing is persisted for unusable images.
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Image is blurry or unclear",
                Detail = ex.Message,
                Status = 422,
                Extensions = { ["code"] = "IMAGE_UNCLEAR" }
            });
        }
        catch (AiServiceException ex)
        {
            var status = ex.Status == System.Net.HttpStatusCode.TooManyRequests ? 429 : 502;
            return Problem(ex.Message, statusCode: status, title: "AI service error");
        }

        var uploads = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(uploads);
        var stored = $"{Guid.NewGuid():N}{ext}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(uploads, stored), bytes, ct);

        var report = new InspectionReport
        {
            ProductId = product.ProductId,
            InspectorId = inspector.InspectorId,
            DefectCategoryId = ai.DefectCategoryId,
            ImageUrl = $"/uploads/{stored}",
            DefectDescription = ai.Description,
            QualityStatus = ai.QualityStatus,
            ConfidenceScore = Math.Clamp(ai.Confidence, 0, 1),
            IsUserConfirmed = false
        };
        db.InspectionReports.Add(report);
        await db.SaveChangesAsync(ct);

        var dto = await ProjectReports(db.InspectionReports.Where(r => r.ReportId == report.ReportId)).SingleAsync(ct);
        return Ok(new AnalyzeResponse(dto, "AI analysis complete. Please review and confirm."));
    }

    /// <summary>Step 4-5: QC confirms (optionally edits) the draft; stock is updated atomically.</summary>
    [HttpPost("/api/confirm-report")]
    [HttpPost("confirm")]
    public async Task<ActionResult<ReportDto>> Confirm([FromBody] ConfirmReportRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var report = await db.InspectionReports.Include(r => r.Product).FirstOrDefaultAsync(r => r.ReportId == req.ReportId, ct);
            if (report is null) return Problem($"Report {req.ReportId} not found.", statusCode: 404);
            if (report.IsUserConfirmed) return Problem("Report was already confirmed.", statusCode: 409);

            var category = await db.DefectCategories.FindAsync([req.DefectCategoryId], ct);
            if (category is null) return Problem($"Defect category {req.DefectCategoryId} not found.", statusCode: 400);

            // Business validation: severity and verdict must agree.
            if (category.SeverityLevel is "Major" or "Critical" && req.QualityStatus == "Pass")
                return Problem($"A '{category.SeverityLevel}' defect cannot be marked Pass.", statusCode: 400);
            if (category.SeverityLevel == "None" && req.QualityStatus == "Fail")
                return Problem("'No defect' cannot be marked Fail; pick a defect category.", statusCode: 400);
            if (req.QualityStatus == "Fail" && string.IsNullOrWhiteSpace(req.DefectDescription))
                return Problem("A description is required when failing a product.", statusCode: 400);

            var p = report.Product!;
            var inspected = p.PassedStock + p.FailedStock + req.Quantity;
            if (inspected > p.TotalStock)
                return Problem($"Inspected units ({inspected}) would exceed total stock ({p.TotalStock}).", statusCode: 400);

            if (req.QualityStatus == "Pass") p.PassedStock += req.Quantity; else p.FailedStock += req.Quantity;

            report.DefectCategoryId = category.DefectCategoryId;
            report.QualityStatus = req.QualityStatus;
            report.DefectDescription = req.DefectDescription ?? report.DefectDescription;
            report.IsUserConfirmed = true;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return Problem("Stock was modified by someone else. Please retry.", statusCode: 409);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Confirm failed for report {Id}", req.ReportId);
            await tx.RollbackAsync(ct);
            throw;
        }

        var dto = await ProjectReports(db.InspectionReports.Where(r => r.ReportId == req.ReportId)).SingleAsync(ct);
        return Ok(dto);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReportDto>>> List(
        [FromQuery] string? status, [FromQuery] int? productId, [FromQuery] bool? confirmed,
        [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var q = db.InspectionReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.QualityStatus == status);
        if (productId is not null) q = q.Where(r => r.ProductId == productId);
        if (confirmed is not null) q = q.Where(r => r.IsUserConfirmed == confirmed);
        return Ok(await ProjectReports(q.OrderByDescending(r => r.CreatedAt).Take(Math.Clamp(take, 1, 500))).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportDto>> Get(int id, CancellationToken ct)
    {
        var dto = await ProjectReports(db.InspectionReports.AsNoTracking().Where(r => r.ReportId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Discard an unconfirmed draft (and its image). Confirmed reports are an audit trail and cannot be deleted.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Discard(int id, CancellationToken ct)
    {
        var r = await db.InspectionReports.FindAsync([id], ct);
        if (r is null) return NotFound();
        if (r.IsUserConfirmed) return Problem("Confirmed reports cannot be deleted.", statusCode: 409);
        db.InspectionReports.Remove(r);
        await db.SaveChangesAsync(ct);
        var path = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), r.ImageUrl.TrimStart('/'));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        return NoContent();
    }

    internal static IQueryable<ReportDto> ProjectReports(IQueryable<InspectionReport> q) =>
        q.Select(r => new ReportDto(r.ReportId, r.ProductId, r.Product!.ProductName, r.InspectorId, r.Inspector!.InspectorName,
            r.DefectCategoryId, r.DefectCategory!.CategoryName, r.DefectCategory.SeverityLevel,
            r.ImageUrl, r.DefectDescription, r.QualityStatus, r.ConfidenceScore, r.IsUserConfirmed, r.CreatedAt));

    private static bool LooksLikeImage(byte[] b) =>
        b.Length > 12 && (
            (b[0] == 0xFF && b[1] == 0xD8) ||                                              // JPEG
            (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) ||              // PNG
            (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E')); // WebP
}
