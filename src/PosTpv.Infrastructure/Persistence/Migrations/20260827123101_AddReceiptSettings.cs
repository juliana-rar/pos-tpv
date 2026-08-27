using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptAddress",
                table: "AppSettings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooter",
                table: "AppSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptLegalName",
                table: "AppSettings",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPaperWidth",
                table: "AppSettings",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowTaxBreakdown",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptTaxId",
                table: "AppSettings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptAddress",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptFooter",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptLegalName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptPaperWidth",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowTaxBreakdown",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptTaxId",
                table: "AppSettings");
        }
    }
}
