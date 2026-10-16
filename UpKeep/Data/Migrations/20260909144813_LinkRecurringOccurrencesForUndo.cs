using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpKeep.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkRecurringOccurrencesForUndo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeneratedFromTaskId",
                table: "MaintenanceTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBeenEdited",
                table: "MaintenanceTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedFromTaskId",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "HasBeenEdited",
                table: "MaintenanceTasks");
        }
    }
}
