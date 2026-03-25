using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineAccountingCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingCategory",
                table: "InvoiceLines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingCategory",
                table: "InvoiceLines");
        }
    }
}
