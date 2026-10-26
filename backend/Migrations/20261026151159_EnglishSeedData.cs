using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DefectInspection.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnglishSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 1,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "No defect", "Product meets quality standards; no defect detected." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 2,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Surface scratch", "Light scratch that does not affect function." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 3,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Stain / discoloration", "Small stain or discoloration that can be cleaned." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 4,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Dent / deformation", "Significant dent or deformation of the product." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 5,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Crack / break", "Product is cracked, broken or snapped." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 6,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Missing / damaged part", "A key component is missing or damaged." });

            migrationBuilder.UpdateData(
                table: "Inspectors",
                keyColumn: "InspectorId",
                keyValue: 1,
                column: "InspectorName",
                value: "An Nguyen");

            migrationBuilder.UpdateData(
                table: "Inspectors",
                keyColumn: "InspectorId",
                keyValue: 2,
                column: "InspectorName",
                value: "Binh Tran");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 1,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Household", "Thermos Flask 500ml" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 2,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Electronics", "Bluetooth Headphones X1" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 3,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Packaging", "Carton Box 30x20x15" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 1,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Không lỗi (No defect)", "Sản phẩm đạt chất lượng, không phát hiện khuyết tật." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 2,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Trầy xước bề mặt (Surface scratch)", "Vết xước nhẹ, không ảnh hưởng chức năng." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 3,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Ố màu / vết bẩn (Stain / discoloration)", "Vết bẩn hoặc đổi màu nhỏ, có thể làm sạch." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 4,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Móp méo / biến dạng (Dent / deformation)", "Sản phẩm bị móp méo hoặc biến dạng đáng kể." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 5,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Nứt vỡ (Crack / break)", "Sản phẩm bị nứt, vỡ hoặc gãy." });

            migrationBuilder.UpdateData(
                table: "DefectCategories",
                keyColumn: "DefectCategoryId",
                keyValue: 6,
                columns: new[] { "CategoryName", "Description" },
                values: new object[] { "Thiếu / hỏng linh kiện (Missing / damaged part)", "Thiếu hoặc hư hỏng bộ phận quan trọng." });

            migrationBuilder.UpdateData(
                table: "Inspectors",
                keyColumn: "InspectorId",
                keyValue: 1,
                column: "InspectorName",
                value: "Nguyễn Văn An");

            migrationBuilder.UpdateData(
                table: "Inspectors",
                keyColumn: "InspectorId",
                keyValue: 2,
                column: "InspectorName",
                value: "Trần Thị Bình");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 1,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Đồ gia dụng", "Bình giữ nhiệt 500ml" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 2,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Điện tử", "Tai nghe Bluetooth X1" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductId",
                keyValue: 3,
                columns: new[] { "Category", "ProductName" },
                values: new object[] { "Bao bì", "Hộp carton 30x20x15" });
        }
    }
}
