namespace InvoiceWizard.Data.ViewModels;

public class ProjectAnalyticsRow
{
    public string CustomerName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public decimal PaidRevenue { get; set; }
    public decimal OpenRevenue { get; set; }
    public decimal LoggedHours { get; set; }
    public int OpenItemCount { get; set; }
}
