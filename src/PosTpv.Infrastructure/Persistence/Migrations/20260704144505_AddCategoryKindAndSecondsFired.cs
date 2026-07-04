using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryKindAndSecondsFired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SecondsFiredAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Classify the seeded "Drinks" category as a bar/drink station on existing databases
            // (new databases get this from the seeder). Kind: 0 = Food, 1 = Drink.
            migrationBuilder.Sql("UPDATE [Categories] SET [Kind] = 1 WHERE [Name] = N'Drinks';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecondsFiredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Categories");
        }
    }
}
