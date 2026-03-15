using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDirectionAndRevenueAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceDirection",
                table: "Invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevenueInvoiceId",
                table: "BankTransactionAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_RevenueInvoiceId",
                table: "BankTransactionAssignments",
                column: "RevenueInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_TenantId_RevenueInvoiceId",
                table: "BankTransactionAssignments",
                columns: new[] { "TenantId", "RevenueInvoiceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactionAssignments_Invoices_RevenueInvoiceId",
                table: "BankTransactionAssignments",
                column: "RevenueInvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactionAssignments_Invoices_RevenueInvoiceId",
                table: "BankTransactionAssignments");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactionAssignments_RevenueInvoiceId",
                table: "BankTransactionAssignments");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactionAssignments_TenantId_RevenueInvoiceId",
                table: "BankTransactionAssignments");

            migrationBuilder.DropColumn(
                name: "InvoiceDirection",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RevenueInvoiceId",
                table: "BankTransactionAssignments");
        }
    }
}
