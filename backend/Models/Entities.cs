using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DefectInspection.Api.Models;

public class Product
{
    public int ProductId { get; set; }
    [Required, MaxLength(200)] public string ProductName { get; set; } = "";
    [MaxLength(100)] public string? Category { get; set; }
    public int TotalStock { get; set; }
    public int PassedStock { get; set; }
    public int FailedStock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optimistic concurrency token so parallel confirmations cannot corrupt stock counters.</summary>
    [Timestamp, JsonIgnore] public byte[]? RowVersion { get; set; }

    [JsonIgnore] public List<InspectionReport> InspectionReports { get; set; } = new();
}

public class Inspector
{
    public int InspectorId { get; set; }
    [Required, MaxLength(100)] public string InspectorName { get; set; } = "";
    [MaxLength(100)] public string? Department { get; set; }
    [MaxLength(100)] public string? Email { get; set; }

    [JsonIgnore] public List<InspectionReport> InspectionReports { get; set; } = new();
}

public class DefectCategory
{
    public int DefectCategoryId { get; set; }
    [Required, MaxLength(100)] public string CategoryName { get; set; } = "";
    /// <summary>None | Minor | Major | Critical</summary>
    [Required, MaxLength(50)] public string SeverityLevel { get; set; } = "None";
    [MaxLength(500)] public string? Description { get; set; }

    [JsonIgnore] public List<InspectionReport> InspectionReports { get; set; } = new();
}

public class InspectionReport
{
    [Key] public int ReportId { get; set; }
    public int ProductId { get; set; }
    public int InspectorId { get; set; }
    public int DefectCategoryId { get; set; }
    [Required, MaxLength(500)] public string ImageUrl { get; set; } = "";
    public string? DefectDescription { get; set; }
    /// <summary>Pass | Fail</summary>
    [Required, MaxLength(50)] public string QualityStatus { get; set; } = "Pass";
    public double ConfidenceScore { get; set; }
    public bool IsUserConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public Inspector? Inspector { get; set; }
    public DefectCategory? DefectCategory { get; set; }
}
