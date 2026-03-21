using InvoiceWizard.Data.Entities;
using InvoiceWizard.Services;

namespace InvoiceWizard.Data.ViewModels;

public class InvoiceLineRow
{
    public InvoiceLineRow(InvoiceLineEntity line)
    {
        Line = line;
    }

    public InvoiceLineEntity Line { get; }
    public string InvoiceNumber => Line.InvoiceDisplayNumber;
    public string ExpenseStatus => Line.ExpenseStatus;
    public int Position => Line.Position;
    public string ArticleNumber => Line.ArticleNumber;
    public string Ean => Line.Ean;
    public string Description => Line.Description;
    public string AccountingCategory => Line.AccountingCategoryLabel;
    public decimal Quantity => Line.Quantity;
    public string Unit => Line.Unit;
    public decimal GrossListPrice => Line.GrossListPrice;
    public decimal NetUnitPrice => Line.NetUnitPrice;
    public decimal GrossUnitPrice => Line.GrossUnitPrice;
    public decimal MetalSurcharge => Line.MetalSurcharge;
    public decimal PriceBasisQuantity => Line.PriceBasisQuantity;
    public decimal LineTotal => Line.LineTotal;
    public decimal GrossLineTotal => Line.GrossLineTotal;
    public decimal EffectiveNetUnitPrice => Quantity <= 0m
        ? PricingHelper.NormalizeUnitPrice(Line.NetUnitPrice, Line.MetalSurcharge, Line.PriceBasisQuantity)
        : PricingHelper.RoundUnitPrice((Line.LineTotal > 0m ? Line.LineTotal : (Quantity * PricingHelper.NormalizeUnitPrice(Line.NetUnitPrice, Line.MetalSurcharge, Line.PriceBasisQuantity))) / Quantity);
    public decimal EffectivePurchaseUnitPrice => EffectiveNetUnitPrice;
    public decimal AllocatedQuantity => Line.Allocations?.Sum(a => a.AllocatedQuantity) ?? 0m;
    public decimal RemainingQuantity => Quantity - AllocatedQuantity;
    public bool IsProjectAllocatable => Line.IsProjectAllocatable;
    public string AllocationSummary =>
        Line.Allocations == null || Line.Allocations.Count == 0
            ? ""
            : string.Join(" | ", Line.Allocations
                .GroupBy(a => $"{a.Customer?.Name ?? "(ohne Kunde)"} / {a.Project?.Name ?? "Ohne Projekt"}")
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Sum(x => x.AllocatedQuantity):0.##}{(g.Any(x => x.IsSmallMaterial) ? " (KM)" : "")}"));
    public string PaidStatus => Line.IsPaid ? $"Bezahlt am {Line.PaidAt:dd.MM.yyyy}" : "Offen";
}
