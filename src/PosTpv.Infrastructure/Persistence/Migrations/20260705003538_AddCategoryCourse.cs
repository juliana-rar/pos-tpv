using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default existing (and future) rows to Main (1) = second course, matching the entity default.
            migrationBuilder.AddColumn<int>(
                name: "Course",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Back-fill the demo catalogue seeded before this column existed. CourseType: Starter=0, Dessert=2.
            migrationBuilder.Sql("UPDATE [Categories] SET [Course] = 0 WHERE [Name] = N'Starters';");
            migrationBuilder.Sql("UPDATE [Categories] SET [Course] = 2 WHERE [Name] = N'Desserts';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Course",
                table: "Categories");
        }
    }
}
