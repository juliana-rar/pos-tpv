using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraCategoryAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtraCategory",
                columns: table => new
                {
                    CategoriesId = table.Column<int>(type: "int", nullable: false),
                    ExtraId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraCategory", x => new { x.CategoriesId, x.ExtraId });
                    table.ForeignKey(
                        name: "FK_ExtraCategory_Categories_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraCategory_Extras_ExtraId",
                        column: x => x.ExtraId,
                        principalTable: "Extras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtraProductException",
                columns: table => new
                {
                    ExcludedProductsId = table.Column<int>(type: "int", nullable: false),
                    ExtraId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraProductException", x => new { x.ExcludedProductsId, x.ExtraId });
                    table.ForeignKey(
                        name: "FK_ExtraProductException_Extras_ExtraId",
                        column: x => x.ExtraId,
                        principalTable: "Extras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraProductException_Products_ExcludedProductsId",
                        column: x => x.ExcludedProductsId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraCategory_ExtraId",
                table: "ExtraCategory",
                column: "ExtraId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraProductException_ExtraId",
                table: "ExtraProductException",
                column: "ExtraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtraCategory");

            migrationBuilder.DropTable(
                name: "ExtraProductException");
        }
    }
}
