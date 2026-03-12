using InvoiceWizard.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    [DbContext(typeof(InvoiceWizardDbContext))]
    [Migration("20260311094500_AddCustomerAndProjectContactData")]
    public partial class AddCustomerAndProjectContactData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "City", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "EmailAddress", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "FirstName", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "HouseNumber", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "LastName", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PhoneNumber", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PostalCode", table: "Customers", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Street", table: "Customers", type: "text", nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<bool>(name: "ConnectionUserSameAsCustomer", table: "Projects", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "ConnectionUserFirstName", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserLastName", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserStreet", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserHouseNumber", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserPostalCode", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserCity", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserEmailAddress", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConnectionUserPhoneNumber", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<bool>(name: "PropertyOwnerSameAsCustomer", table: "Projects", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerFirstName", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerLastName", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerStreet", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerHouseNumber", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerPostalCode", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerCity", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerEmailAddress", table: "Projects", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PropertyOwnerPhoneNumber", table: "Projects", type: "text", nullable: false, defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "City", "EmailAddress", "FirstName", "HouseNumber", "LastName", "PhoneNumber", "PostalCode", "Street" })
            {
                migrationBuilder.DropColumn(name: column, table: "Customers");
            }

            foreach (var column in new[]
            {
                "ConnectionUserSameAsCustomer", "ConnectionUserFirstName", "ConnectionUserLastName", "ConnectionUserStreet", "ConnectionUserHouseNumber", "ConnectionUserPostalCode", "ConnectionUserCity", "ConnectionUserEmailAddress", "ConnectionUserPhoneNumber",
                "PropertyOwnerSameAsCustomer", "PropertyOwnerFirstName", "PropertyOwnerLastName", "PropertyOwnerStreet", "PropertyOwnerHouseNumber", "PropertyOwnerPostalCode", "PropertyOwnerCity", "PropertyOwnerEmailAddress", "PropertyOwnerPhoneNumber"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "Projects");
            }
        }
    }
}
