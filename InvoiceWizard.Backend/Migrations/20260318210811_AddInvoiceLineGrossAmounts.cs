using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineGrossAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossLineTotal",
                table: "InvoiceLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossUnitPrice",
                table: "InvoiceLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                WITH invoice_net_totals AS (
                    SELECT il."InvoiceId",
                           COALESCE(SUM(il."LineTotal"), 0) AS "NetTotal"
                    FROM "InvoiceLines" il
                    GROUP BY il."InvoiceId"
                ),
                calculated_lines AS (
                    SELECT il."InvoiceLineId",
                           CASE
                               WHEN totals."NetTotal" > 0 AND i."InvoiceTotalAmount" > 0
                                   THEN ROUND(il."LineTotal" * (i."InvoiceTotalAmount" / totals."NetTotal"), 2)
                               ELSE il."LineTotal"
                           END AS "CalculatedGrossLineTotal",
                           il."Quantity"
                    FROM "InvoiceLines" il
                    INNER JOIN "Invoices" i ON i."InvoiceId" = il."InvoiceId"
                    LEFT JOIN invoice_net_totals totals ON totals."InvoiceId" = il."InvoiceId"
                )
                UPDATE "InvoiceLines" il
                SET "GrossLineTotal" = calc."CalculatedGrossLineTotal",
                    "GrossUnitPrice" = CASE
                        WHEN calc."Quantity" > 0
                            THEN ROUND(calc."CalculatedGrossLineTotal" / calc."Quantity", 4)
                        ELSE 0
                    END
                FROM calculated_lines calc
                WHERE calc."InvoiceLineId" = il."InvoiceLineId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossLineTotal",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "GrossUnitPrice",
                table: "InvoiceLines");
        }
    }
}
