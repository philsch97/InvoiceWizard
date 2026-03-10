namespace InvoiceWizard.Services;

public static class PricingHelper
{
    public static decimal NormalizeUnitPrice(decimal unitPrice, decimal basisQuantity)
    {
        var divisor = basisQuantity <= 0m ? 1m : basisQuantity;
        return unitPrice / divisor;
    }

    public static decimal ApplyMarkup(decimal baseUnitPrice, decimal markupPercent)
    {
        return baseUnitPrice * (1m + (markupPercent / 100m));
    }
}
