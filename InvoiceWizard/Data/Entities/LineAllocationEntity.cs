namespace InvoiceWizard.Data.Entities;

public class LineAllocationEntity
{
    public int LineAllocationId { get; set; }
    public int InvoiceLineId { get; set; }
    public InvoiceLineEntity InvoiceLine { get; set; } = null!;
    public int CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal CustomerUnitPrice { get; set; }
    public bool IsSmallMaterial { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerInvoiceNumber { get; set; }
    public DateTime? CustomerInvoicedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal ExportedMarkupPercent { get; set; }
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
}
