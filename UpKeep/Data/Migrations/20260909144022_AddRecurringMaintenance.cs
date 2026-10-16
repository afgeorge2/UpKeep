using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpKeep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCreatedNextOccurrence",
                table: "MaintenanceTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RepeatEveryMonths",
                table: "MaintenanceTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCreatedNextOccurrence",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "RepeatEveryMonths",
                table: "MaintenanceTasks");
        }
    }
}
