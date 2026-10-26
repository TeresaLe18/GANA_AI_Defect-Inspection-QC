using DefectInspection.Api.Data;
using DefectInspection.Api.Dtos;
using DefectInspection.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DefectInspection.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Product>> List(CancellationToken ct) =>
        await db.Products.AsNoTracking().OrderBy(p => p.ProductName).ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductRequest req, CancellationToken ct)
    {
        var p = new Product { ProductName = req.ProductName.Trim(), Category = req.Category, TotalStock = req.TotalStock };
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { id = p.ProductId }, p);
    }
}

[ApiController]
public class LookupController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/inspectors")]
    public async Task<IEnumerable<Inspector>> Inspectors(CancellationToken ct) =>
        await db.Inspectors.AsNoTracking().OrderBy(i => i.InspectorName).ToListAsync(ct);

    [HttpGet("api/defect-categories")]
    public async Task<IEnumerable<DefectCategory>> Categories(CancellationToken ct) =>
        await db.DefectCategories.AsNoTracking().OrderBy(c => c.DefectCategoryId).ToListAsync(ct);

    [HttpGet("api/dashboard/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var reports = db.InspectionReports.AsNoTracking().Where(r => r.IsUserConfirmed);
        var totals = await db.Products.AsNoTracking().GroupBy(_ => 1).Select(g => new
        {
            total = g.Sum(p => p.TotalStock), passed = g.Sum(p => p.PassedStock), failed = g.Sum(p => p.FailedStock)
        }).FirstOrDefaultAsync(ct);
        var byDefect = await reports.GroupBy(r => r.DefectCategory!.CategoryName)
            .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().OrderBy(p => p.ProductName)
            .Select(p => new { p.ProductId, p.ProductName, p.TotalStock, p.PassedStock, p.FailedStock }).ToListAsync(ct);

        var total = totals?.total ?? 0; var passed = totals?.passed ?? 0; var failed = totals?.failed ?? 0;
        var inspected = passed + failed;
        return Ok(new
        {
            totalStock = total, passedStock = passed, failedStock = failed,
            pendingStock = total - inspected,
            passRate = inspected == 0 ? (double?)null : Math.Round(100.0 * passed / inspected, 1),
            confirmedReports = await reports.CountAsync(ct),
            pendingReports = await db.InspectionReports.CountAsync(r => !r.IsUserConfirmed, ct),
            defectsByCategory = byDefect,
            products
        });
    }
}
