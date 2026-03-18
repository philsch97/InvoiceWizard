using InvoiceWizard.Services;

namespace InvoiceWizard.Data.ViewModels;

public class ManualInvoiceLineInput
{
    public int Position { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal VatPercent { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal GrossUnitPrice { get; set; }
    public decimal PriceBasisQuantity { get; set; } = 1m;
    public decimal EffectiveNetUnitPrice => PricingHelper.NormalizeUnitPrice(NetUnitPrice, MetalSurcharge, PriceBasisQuantity);
    public decimal LineTotal => Quantity * ((NetUnitPrice + MetalSurcharge) / (PriceBasisQuantity <= 0m ? 1m : PriceBasisQuantity));
    public decimal GrossLineTotal { get; set; }
}


