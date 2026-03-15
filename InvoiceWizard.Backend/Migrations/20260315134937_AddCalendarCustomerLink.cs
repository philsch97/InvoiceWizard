using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarCustomerLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "CalendarEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_CustomerId",
                table: "CalendarEntries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_TenantId_CustomerId",
                table: "CalendarEntries",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEntries_Customers_CustomerId",
                table: "CalendarEntries",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEntries_Customers_CustomerId",
                table: "CalendarEntries");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEntries_CustomerId",
                table: "CalendarEntries");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEntries_TenantId_CustomerId",
                table: "CalendarEntries");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CalendarEntries");
        }
    }
}
