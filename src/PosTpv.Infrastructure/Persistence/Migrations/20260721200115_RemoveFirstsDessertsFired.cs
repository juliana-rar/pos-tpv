using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirstsDessertsFired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DessertsFiredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DessertsSentAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FirstsFiredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FirstsSentAt",
                table: "Orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DessertsFiredAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DessertsSentAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstsFiredAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstsSentAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }
    }
}
