using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "TenantLicenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "TenantLicenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GraceUntil",
                table: "TenantLicenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextBillingDate",
                table: "TenantLicenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceNet",
                table: "TenantLicenses",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RenewsAutomatically",
                table: "TenantLicenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "LicenseActivations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PriceNet",
                table: "LicenseActivations",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RenewsAutomatically",
                table: "LicenseActivations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "GraceUntil",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "NextBillingDate",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "PriceNet",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "RenewsAutomatically",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "LicenseActivations");

            migrationBuilder.DropColumn(
                name: "PriceNet",
                table: "LicenseActivations");

            migrationBuilder.DropColumn(
                name: "RenewsAutomatically",
                table: "LicenseActivations");
        }
    }
}
