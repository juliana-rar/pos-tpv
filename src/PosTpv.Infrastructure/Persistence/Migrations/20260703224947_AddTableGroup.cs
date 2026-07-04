using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Tables",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_GroupId",
                table: "Tables",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_GroupId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Tables");
        }
    }
}
