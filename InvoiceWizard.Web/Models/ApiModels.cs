namespace InvoiceWizard.Web.Models;

public class DashboardSummary
{
    public int CustomerCount { get; set; }
    public int ProjectCount { get; set; }
    public int OpenMaterialItemCount { get; set; }
    public int OpenWorkItemCount { get; set; }
    public decimal LoggedHoursCurrentMonth { get; set; }
    public decimal PaidRevenue { get; set; }
    public decimal OpenRevenue { get; set; }
}

public class CustomerItem
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
    public int ProjectCount { get; set; }
    public int OpenWorkItems { get; set; }
}

public class SaveCustomerModel
{
    public string Name { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
}

public class ProjectItem
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Name { get; set; } = "";
    public int OpenWorkItems { get; set; }
    public decimal LoggedHours { get; set; }
}

public class SaveProjectModel
{
    public string Name { get; set; } = "";
}

public class WorkTimeItem
{
    public int WorkTimeEntryId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public decimal HoursWorked { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TravelKilometers { get; set; }
    public decimal TravelRatePerKilometer { get; set; }
    public string Description { get; set; } = "";
    public string Comment { get; set; } = "";
    public string? CustomerInvoiceNumber { get; set; }
    public DateTime? CustomerInvoicedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal LineTotal { get; set; }
}

public class SaveWorkTimeModel
{
    public int CustomerId { get; set; }
    public int? ProjectId { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(8, 0, 0);
    public TimeSpan EndTime { get; set; } = new(16, 30, 0);
    public int BreakMinutes { get; set; } = 30;
    public decimal HourlyRate { get; set; } = 65m;
    public decimal TravelKilometers { get; set; }
    public decimal TravelRatePerKilometer { get; set; }
    public string Description { get; set; } = "Arbeitszeit";
    public string Comment { get; set; } = "";
}

public class UpdateWorkTimeStatusModel
{
    public string? CustomerInvoiceNumber { get; set; }
    public bool MarkInvoiced { get; set; }
    public bool MarkPaid { get; set; }
}
