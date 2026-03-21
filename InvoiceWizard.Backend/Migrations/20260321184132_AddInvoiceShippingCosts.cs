using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceShippingCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCostGross",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCostNet",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingGrossShare",
                table: "InvoiceLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingNetShare",
                table: "InvoiceLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingCostGross",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ShippingCostNet",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ShippingGrossShare",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ShippingNetShare",
                table: "InvoiceLines");
        }
    }
}
