using System.ComponentModel.DataAnnotations;

namespace DefectInspection.Api.Dtos;

public class AnalyzeRequest
{
    [Required] public IFormFile? Image { get; set; }
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int InspectorId { get; set; }
}

public class ConfirmReportRequest
{
    [Range(1, int.MaxValue)] public int ReportId { get; set; }
    /// <summary>QC-reviewed values; they override the AI's suggestion.</summary>
    [Range(1, int.MaxValue)] public int DefectCategoryId { get; set; }
    [Required, RegularExpression("^(Pass|Fail)$", ErrorMessage = "QualityStatus must be 'Pass' or 'Fail'.")]
    public string QualityStatus { get; set; } = "";
    [MaxLength(4000)] public string? DefectDescription { get; set; }
    /// <summary>Number of units this inspection represents (added to PassedStock or FailedStock).</summary>
    [Range(1, 100000)] public int Quantity { get; set; } = 1;
}

public class CreateProductRequest
{
    [Required, MaxLength(200)] public string ProductName { get; set; } = "";
    [MaxLength(100)] public string? Category { get; set; }
    [Range(0, 10_000_000)] public int TotalStock { get; set; }
}

public record GeminiResult(
    bool IsImageClear,
    string? UnclearReason,
    string QualityStatus,
    int DefectCategoryId,
    string Description,
    double Confidence);

public record ReportDto(
    int ReportId, int ProductId, string ProductName, int InspectorId, string InspectorName,
    int DefectCategoryId, string DefectCategoryName, string Severity,
    string ImageUrl, string? DefectDescription, string QualityStatus,
    double ConfidenceScore, bool IsUserConfirmed, DateTime CreatedAt);

public record AnalyzeResponse(ReportDto Report, string Message);
