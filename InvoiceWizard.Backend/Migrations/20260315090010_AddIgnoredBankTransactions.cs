using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIgnoredBankTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IgnoredAt",
                table: "BankTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IgnoredComment",
                table: "BankTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsIgnored",
                table: "BankTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoredAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "IgnoredComment",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "IsIgnored",
                table: "BankTransactions");
        }
    }
}
