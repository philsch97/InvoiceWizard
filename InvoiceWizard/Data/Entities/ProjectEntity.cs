namespace InvoiceWizard.Data.Entities;

public class ProjectEntity
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<LineAllocationEntity> Allocations { get; set; } = new();
    public List<WorkTimeEntryEntity> WorkTimeEntries { get; set; } = new();
}
