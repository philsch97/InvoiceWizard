using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSoneparConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantSoneparConnections",
                columns: table => new
                {
                    TenantSoneparConnectionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordCipherText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CustomerNumberCipherText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ClientIdCipherText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OmdVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokenUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OpenMasterDataBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSoneparConnections", x => x.TenantSoneparConnectionId);
                    table.ForeignKey(
                        name: "FK_TenantSoneparConnections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSoneparConnections_TenantId",
                table: "TenantSoneparConnections",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSoneparConnections");
        }
    }
}
