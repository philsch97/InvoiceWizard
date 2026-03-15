using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkTimePunchClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "WorkTimeEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClockActive",
                table: "WorkTimeEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PauseStartedAtUtc",
                table: "WorkTimeEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimeEntries_AppUserId",
                table: "WorkTimeEntries",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimeEntries_TenantId_AppUserId_IsClockActive",
                table: "WorkTimeEntries",
                columns: new[] { "TenantId", "AppUserId", "IsClockActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTimeEntries_AppUsers_AppUserId",
                table: "WorkTimeEntries",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "AppUserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTimeEntries_AppUsers_AppUserId",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_WorkTimeEntries_AppUserId",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_WorkTimeEntries_TenantId_AppUserId_IsClockActive",
                table: "WorkTimeEntries");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "WorkTimeEntries");

            migrationBuilder.DropColumn(
                name: "IsClockActive",
                table: "WorkTimeEntries");

            migrationBuilder.DropColumn(
                name: "PauseStartedAtUtc",
                table: "WorkTimeEntries");
        }
    }
}
