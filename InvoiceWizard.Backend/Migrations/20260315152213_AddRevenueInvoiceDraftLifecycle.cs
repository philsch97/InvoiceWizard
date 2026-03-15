using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueInvoiceDraftLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevenueInvoiceId",
                table: "WorkTimeEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevenueInvoiceId",
                table: "LineAllocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DraftSavedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                table: "Invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimeEntries_RevenueInvoiceId",
                table: "WorkTimeEntries",
                column: "RevenueInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimeEntries_TenantId_RevenueInvoiceId",
                table: "WorkTimeEntries",
                columns: new[] { "TenantId", "RevenueInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_LineAllocations_RevenueInvoiceId",
                table: "LineAllocations",
                column: "RevenueInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LineAllocations_TenantId_RevenueInvoiceId",
                table: "LineAllocations",
                columns: new[] { "TenantId", "RevenueInvoiceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LineAllocations_Invoices_RevenueInvoiceId",
                table: "LineAllocations",
                column: "RevenueInvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTimeEntries_Invoices_RevenueInvoiceId",
                table: "WorkTimeEntries",
                column: "RevenueInvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LineAllocations_Invoices_RevenueInvoiceId",
                table: "LineAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTimeEntries_Invoices_RevenueInvoiceId",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_WorkTimeEntries_RevenueInvoiceId",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_WorkTimeEntries_TenantId_RevenueInvoiceId",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_LineAllocations_RevenueInvoiceId",
                table: "LineAllocations");

            migrationBuilder.DropIndex(
                name: "IX_LineAllocations_TenantId_RevenueInvoiceId",
                table: "LineAllocations");

            migrationBuilder.DropColumn(
                name: "RevenueInvoiceId",
                table: "WorkTimeEntries");

            migrationBuilder.DropColumn(
                name: "RevenueInvoiceId",
                table: "LineAllocations");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DraftSavedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceStatus",
                table: "Invoices");
        }
    }
}
