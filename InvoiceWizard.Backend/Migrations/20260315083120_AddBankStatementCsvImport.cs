using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatementCsvImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatementImports",
                columns: table => new
                {
                    BankStatementImportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountIban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImports", x => x.BankStatementImportId);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    BankTransactionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BankStatementImportId = table.Column<int>(type: "integer", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "date", nullable: false),
                    ValueDate = table.Column<DateTime>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    BalanceAfterBooking = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CounterpartyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CounterpartyIban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AccountIban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.BankTransactionId);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankStatementImports_BankStatementImportId",
                        column: x => x.BankStatementImportId,
                        principalTable: "BankStatementImports",
                        principalColumn: "BankStatementImportId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactionAssignments",
                columns: table => new
                {
                    BankTransactionAssignmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BankTransactionId = table.Column<int>(type: "integer", nullable: false),
                    SupplierInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    CustomerInvoiceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    AssignedAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactionAssignments", x => x.BankTransactionAssignmentId);
                    table.ForeignKey(
                        name: "FK_BankTransactionAssignments_BankTransactions_BankTransaction~",
                        column: x => x.BankTransactionId,
                        principalTable: "BankTransactions",
                        principalColumn: "BankTransactionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactionAssignments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionAssignments_Invoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_TenantId",
                table: "BankStatementImports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_BankTransactionId",
                table: "BankTransactionAssignments",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_CustomerId",
                table: "BankTransactionAssignments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_SupplierInvoiceId",
                table: "BankTransactionAssignments",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_TenantId",
                table: "BankTransactionAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_TenantId_BankTransactionId",
                table: "BankTransactionAssignments",
                columns: new[] { "TenantId", "BankTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_TenantId_CustomerInvoiceNumber",
                table: "BankTransactionAssignments",
                columns: new[] { "TenantId", "CustomerInvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionAssignments_TenantId_SupplierInvoiceId",
                table: "BankTransactionAssignments",
                columns: new[] { "TenantId", "SupplierInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankStatementImportId",
                table: "BankTransactions",
                column: "BankStatementImportId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId",
                table: "BankTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_BookingDate_Amount",
                table: "BankTransactions",
                columns: new[] { "TenantId", "BookingDate", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_ContentHash",
                table: "BankTransactions",
                columns: new[] { "TenantId", "ContentHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransactionAssignments");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "BankStatementImports");
        }
    }
}
