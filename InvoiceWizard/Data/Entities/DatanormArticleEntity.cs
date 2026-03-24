namespace InvoiceWizard.Data.Entities;

public class DatanormArticleEntity
{
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "ST";
    public decimal NetPrice { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal PriceBasisQuantity { get; set; } = 1m;
    public string SourceFileName { get; set; } = "";

    public string SearchText => string.Join(" ", new[] { ArticleNumber, Ean, Description, Unit })
        .Trim()
        .ToLowerInvariant();
}
