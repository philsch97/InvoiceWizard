using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    [DbContext(typeof(InvoiceWizard.Backend.Data.InvoiceWizardDbContext))]
    [Migration("20260310153000_AddTravelToWorkTimeEntries")]
    public partial class AddTravelToWorkTimeEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TravelKilometers",
                table: "WorkTimeEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TravelRatePerKilometer",
                table: "WorkTimeEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TravelKilometers",
                table: "WorkTimeEntries");

            migrationBuilder.DropColumn(
                name: "TravelRatePerKilometer",
                table: "WorkTimeEntries");
        }
    }
}
