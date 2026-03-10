namespace InvoiceWizard.Data.Entities;

public class CustomerEntity
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
    public List<ProjectEntity> Projects { get; set; } = new();
    public List<LineAllocationEntity> Allocations { get; set; } = new();
}
