using InvoiceWizard.Services;

namespace InvoiceWizard.Data.ViewModels;

public class ManualInvoiceLineInput
{
    public int Position { get; set; }
    public string AccountingCategory { get; set; } = "MaterialAndGoods";
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
    public decimal ShippingNetShare { get; set; }
    public decimal ShippingGrossShare { get; set; }
    public decimal EffectiveNetUnitPrice => Quantity <= 0m
        ? PricingHelper.NormalizeUnitPrice(NetUnitPrice, MetalSurcharge, PriceBasisQuantity)
        : PricingHelper.RoundUnitPrice((Quantity * PricingHelper.NormalizeUnitPrice(NetUnitPrice, MetalSurcharge, PriceBasisQuantity) + ShippingNetShare) / Quantity);
    public decimal LineTotal => Quantity * ((NetUnitPrice + MetalSurcharge) / (PriceBasisQuantity <= 0m ? 1m : PriceBasisQuantity)) + ShippingNetShare;
    public decimal GrossLineTotal { get; set; }
}


