using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosTpv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTablePreJoinGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PreJoinHeight",
                table: "Tables",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreJoinIsLocked",
                table: "Tables",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PreJoinPositionX",
                table: "Tables",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PreJoinPositionY",
                table: "Tables",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PreJoinRotation",
                table: "Tables",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreJoinHeight",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PreJoinIsLocked",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PreJoinPositionX",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PreJoinPositionY",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PreJoinRotation",
                table: "Tables");
        }
    }
}
