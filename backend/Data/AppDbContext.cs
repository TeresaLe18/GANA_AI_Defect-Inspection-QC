using DefectInspection.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DefectInspection.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inspector> Inspectors => Set<Inspector>();
    public DbSet<DefectCategory> DefectCategories => Set<DefectCategory>();
    public DbSet<InspectionReport> InspectionReports => Set<InspectionReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().ToTable(t => t.HasCheckConstraint(
            "CK_Product_Stock",
            "[PassedStock] >= 0 AND [FailedStock] >= 0 AND [PassedStock] + [FailedStock] <= [TotalStock]"));

        b.Entity<InspectionReport>(e =>
        {
            // Restrict: deleting a product/inspector/category must not silently wipe QC history.
            e.HasOne(r => r.Product).WithMany(p => p.InspectionReports).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Inspector).WithMany(i => i.InspectionReports).HasForeignKey(r => r.InspectorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.DefectCategory).WithMany(c => c.InspectionReports).HasForeignKey(r => r.DefectCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.CreatedAt);
        });

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        b.Entity<DefectCategory>().HasData(
            new DefectCategory { DefectCategoryId = 1, CategoryName = "No defect", SeverityLevel = "None", Description = "Product meets quality standards; no defect detected." },
            new DefectCategory { DefectCategoryId = 2, CategoryName = "Surface scratch", SeverityLevel = "Minor", Description = "Light scratch that does not affect function." },
            new DefectCategory { DefectCategoryId = 3, CategoryName = "Stain / discoloration", SeverityLevel = "Minor", Description = "Small stain or discoloration that can be cleaned." },
            new DefectCategory { DefectCategoryId = 4, CategoryName = "Dent / deformation", SeverityLevel = "Major", Description = "Significant dent or deformation of the product." },
            new DefectCategory { DefectCategoryId = 5, CategoryName = "Crack / break", SeverityLevel = "Major", Description = "Product is cracked, broken or snapped." },
            new DefectCategory { DefectCategoryId = 6, CategoryName = "Missing / damaged part", SeverityLevel = "Critical", Description = "A key component is missing or damaged." });

        b.Entity<Inspector>().HasData(
            new Inspector { InspectorId = 1, InspectorName = "An Nguyen", Department = "QC", Email = "an.nguyen@example.com" },
            new Inspector { InspectorId = 2, InspectorName = "Binh Tran", Department = "Warehouse", Email = "binh.tran@example.com" });

        b.Entity<Product>().HasData(
            new Product { ProductId = 1, ProductName = "Thermos Flask 500ml", Category = "Household", TotalStock = 200, CreatedAt = seedDate },
            new Product { ProductId = 2, ProductName = "Bluetooth Headphones X1", Category = "Electronics", TotalStock = 150, CreatedAt = seedDate },
            new Product { ProductId = 3, ProductName = "Carton Box 30x20x15", Category = "Packaging", TotalStock = 500, CreatedAt = seedDate });
    }
}
