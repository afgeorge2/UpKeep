using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpKeep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "MaintenanceTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "MaintenanceTasks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "MaintenanceTasks");
        }
    }
}
