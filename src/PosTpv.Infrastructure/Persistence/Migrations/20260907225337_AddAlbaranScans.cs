using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbaranScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbaranScans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierNameRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlbaranNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AlbaranDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RawModelResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbaranScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbaranScans_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlbaranScans_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AlbaranScanLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AlbaranScanId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbaranScanLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbaranScanLines_AlbaranScans_AlbaranScanId",
                        column: x => x.AlbaranScanId,
                        principalTable: "AlbaranScans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbaranScanLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScanLines_AlbaranScanId",
                table: "AlbaranScanLines",
                column: "AlbaranScanId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScanLines_ProductId",
                table: "AlbaranScanLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScans_CreatedAt",
                table: "AlbaranScans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScans_PurchaseId",
                table: "AlbaranScans",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScans_Status",
                table: "AlbaranScans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AlbaranScans_SupplierId",
                table: "AlbaranScans",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbaranScanLines");

            migrationBuilder.DropTable(
                name: "AlbaranScans");
        }
    }
}
