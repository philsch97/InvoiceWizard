using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileAndGeneratedInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankBic",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankIban",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyCity",
                table: "Tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyEmailAddress",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyHouseNumber",
                table: "Tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyPhoneNumber",
                table: "Tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyPostalCode",
                table: "Tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyStreet",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NextCustomerNumber",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextRevenueInvoiceNumber",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ApplySmallBusinessRegulation",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryDate",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerNumber",
                table: "Customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceDirection_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceDirection", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_CustomerNumber",
                table: "Customers",
                columns: new[] { "TenantId", "CustomerNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_InvoiceDirection_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_CustomerNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankBic",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BankIban",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyCity",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyEmailAddress",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyHouseNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyPhoneNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyPostalCode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyStreet",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NextCustomerNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NextRevenueInvoiceNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApplySmallBusinessRegulation",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerNumber",
                table: "Customers");
        }
    }
}
