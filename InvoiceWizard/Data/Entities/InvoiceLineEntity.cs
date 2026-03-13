namespace InvoiceWizard.Data.Entities;

public class InvoiceLineEntity
{
    public int InvoiceLineId { get; set; }
    public int InvoiceId { get; set; }
    public InvoiceEntity Invoice { get; set; } = null!;
    public int Position { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal PriceBasisQuantity { get; set; } = 1;
    public decimal LineTotal { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public List<LineAllocationEntity> Allocations { get; set; } = new();
}


