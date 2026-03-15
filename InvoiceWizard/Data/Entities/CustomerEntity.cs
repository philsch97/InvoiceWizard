namespace InvoiceWizard.Data.Entities;

public class CustomerEntity
{
    public int CustomerId { get; set; }
    public string CustomerNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
    public List<ProjectEntity> Projects { get; set; } = new();
    public List<LineAllocationEntity> Allocations { get; set; } = new();
}
