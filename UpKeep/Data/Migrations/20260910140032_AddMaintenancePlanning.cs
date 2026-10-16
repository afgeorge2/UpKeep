using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpKeep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenancePlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "MaintenanceTasks",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "MaintenanceTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "MaintenanceTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastPurchase",
                table: "MaintenanceTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RepeatFromCompletion",
                table: "MaintenanceTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleAnchor",
                table: "MaintenanceTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Supplies",
                table: "MaintenanceTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "LastPurchase",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "RepeatFromCompletion",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "ScheduleAnchor",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "Supplies",
                table: "MaintenanceTasks");
        }
    }
}
