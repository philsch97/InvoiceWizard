namespace InvoiceWizard.Data.Entities;

public class WorkTimeEntryEntity
{
    public int WorkTimeEntryId { get; set; }
    public int? AppUserId { get; set; }
    public string UserDisplayName { get; set; } = "";
    public int CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public decimal HoursWorked { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TravelKilometers { get; set; }
    public decimal TravelRatePerKilometer { get; set; }
    public string Description { get; set; } = "Arbeitszeit";
    public string Comment { get; set; } = "";
    public string? CustomerInvoiceNumber { get; set; }
    public DateTime? CustomerInvoicedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool IsClockActive { get; set; }
    public DateTime? PauseStartedAtUtc { get; set; }
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal CalculatedLineTotal => (HoursWorked * HourlyRate) + (TravelKilometers * TravelRatePerKilometer);
    public string TimeRange => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
}
