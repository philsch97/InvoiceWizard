namespace InvoiceWizard.Data.ViewModels;

public class AnalyticsMonthViewModel
{
    public string Label { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit => Revenue - Expenses;
    public double RevenueHeight { get; set; }
    public double ExpenseHeight { get; set; }
}
