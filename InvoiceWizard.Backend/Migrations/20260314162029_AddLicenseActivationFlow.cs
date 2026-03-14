using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseActivationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludesMobileAccessOverride",
                table: "TenantLicenses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCustomersOverride",
                table: "TenantLicenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxProjectsOverride",
                table: "TenantLicenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsersOverride",
                table: "TenantLicenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "AppUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LicenseActivations",
                columns: table => new
                {
                    LicenseActivationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivationCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxUsersOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxProjectsOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxCustomersOverride = table.Column<int>(type: "integer", nullable: true),
                    IncludesMobileAccessOverride = table.Column<bool>(type: "boolean", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedByAppUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByAppUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseActivations", x => x.LicenseActivationId);
                    table.ForeignKey(
                        name: "FK_LicenseActivations_AppUsers_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LicenseActivations_AppUsers_UsedByAppUserId",
                        column: x => x.UsedByAppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LicenseActivations_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseActivations_ActivationCode",
                table: "LicenseActivations",
                column: "ActivationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseActivations_CreatedByAppUserId",
                table: "LicenseActivations",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseActivations_SubscriptionPlanId",
                table: "LicenseActivations",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseActivations_UsedByAppUserId",
                table: "LicenseActivations",
                column: "UsedByAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseActivations");

            migrationBuilder.DropColumn(
                name: "IncludesMobileAccessOverride",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "MaxCustomersOverride",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "MaxProjectsOverride",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "MaxUsersOverride",
                table: "TenantLicenses");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "AppUsers");
        }
    }
}
