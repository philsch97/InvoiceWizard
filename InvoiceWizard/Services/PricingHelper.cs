namespace InvoiceWizard.Services;
public static class PricingHelper
{
    public const decimal GermanVatRate = 0.19m;

    public static decimal NormalizeUnitPrice(decimal unitPrice, decimal basisQuantity)
    {
        var divisor = basisQuantity <= 0m ? 1m : basisQuantity;
        return unitPrice / divisor;
    }
    public static decimal NormalizeUnitPrice(decimal unitPrice, decimal metalSurcharge, decimal basisQuantity)
    {
        return NormalizeUnitPrice(unitPrice + metalSurcharge, basisQuantity);
    }
    public static decimal CalculateLineTotal(decimal quantity, decimal unitPrice, decimal metalSurcharge, decimal basisQuantity)
    {
        return quantity * NormalizeUnitPrice(unitPrice, metalSurcharge, basisQuantity);
    }
    public static decimal ApplyMarkup(decimal baseUnitPrice, decimal markupPercent)
    {
        return baseUnitPrice * (1m + (markupPercent / 100m));
    }

    public static decimal AddVat(decimal amount, decimal vatRate = GermanVatRate)
    {
        return amount * (1m + vatRate);
    }

    public static decimal CalculateRevenueUnitPrice(decimal purchaseUnitPriceNet, decimal markupPercent, bool applySmallBusinessRegulation)
    {
        var basePrice = applySmallBusinessRegulation
            ? AddVat(purchaseUnitPriceNet)
            : purchaseUnitPriceNet;

        return ApplyMarkup(basePrice, markupPercent);
    }

    public static decimal CalculateRevenueVatAmount(decimal netSubtotal, bool applySmallBusinessRegulation, decimal vatRate = GermanVatRate)
    {
        return applySmallBusinessRegulation ? 0m : netSubtotal * vatRate;
    }

    public static decimal CalculateRevenueGrossTotal(decimal netSubtotal, bool applySmallBusinessRegulation, decimal vatRate = GermanVatRate)
    {
        return netSubtotal + CalculateRevenueVatAmount(netSubtotal, applySmallBusinessRegulation, vatRate);
    }

    public static decimal CalculateExpenseGrossTotal(decimal netSubtotal, decimal vatRate = GermanVatRate)
    {
        return netSubtotal * (1m + vatRate);
    }
}
