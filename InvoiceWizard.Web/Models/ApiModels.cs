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
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? EmailAddress { get; set; }
    public string PhoneNumber { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
    public int ProjectCount { get; set; }
    public int OpenWorkItems { get; set; }
}

public class SaveCustomerModel
{
    public string Name { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? EmailAddress { get; set; }
    public string PhoneNumber { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
}

public class ProjectItem
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProjectStatus { get; set; } = "Active";
    public int OpenWorkItems { get; set; }
    public decimal LoggedHours { get; set; }
}

public class SaveProjectModel
{
    public string Name { get; set; } = "";
    public string ProjectStatus { get; set; } = "Active";
    public bool ConnectionUserSameAsCustomer { get; set; }
    public string ConnectionUserFirstName { get; set; } = "";
    public string ConnectionUserLastName { get; set; } = "";
    public string ConnectionUserStreet { get; set; } = "";
    public string ConnectionUserHouseNumber { get; set; } = "";
    public string ConnectionUserPostalCode { get; set; } = "";
    public string ConnectionUserCity { get; set; } = "";
    public string ConnectionUserParcelNumber { get; set; } = "";
    public string? ConnectionUserEmailAddress { get; set; }
    public string ConnectionUserPhoneNumber { get; set; } = "";
    public bool PropertyOwnerSameAsCustomer { get; set; }
    public string PropertyOwnerFirstName { get; set; } = "";
    public string PropertyOwnerLastName { get; set; } = "";
    public string PropertyOwnerStreet { get; set; } = "";
    public string PropertyOwnerHouseNumber { get; set; } = "";
    public string PropertyOwnerPostalCode { get; set; } = "";
    public string PropertyOwnerCity { get; set; } = "";
    public string? PropertyOwnerEmailAddress { get; set; }
    public string PropertyOwnerPhoneNumber { get; set; } = "";
}

public class ProjectDetailsItem
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ProjectStatus { get; set; } = "Active";
    public bool ConnectionUserSameAsCustomer { get; set; }
    public string ConnectionUserFirstName { get; set; } = "";
    public string ConnectionUserLastName { get; set; } = "";
    public string ConnectionUserStreet { get; set; } = "";
    public string ConnectionUserHouseNumber { get; set; } = "";
    public string ConnectionUserPostalCode { get; set; } = "";
    public string ConnectionUserCity { get; set; } = "";
    public string ConnectionUserParcelNumber { get; set; } = "";
    public string? ConnectionUserEmailAddress { get; set; }
    public string ConnectionUserPhoneNumber { get; set; } = "";
    public bool PropertyOwnerSameAsCustomer { get; set; }
    public string PropertyOwnerFirstName { get; set; } = "";
    public string PropertyOwnerLastName { get; set; } = "";
    public string PropertyOwnerStreet { get; set; } = "";
    public string PropertyOwnerHouseNumber { get; set; } = "";
    public string PropertyOwnerPostalCode { get; set; } = "";
    public string PropertyOwnerCity { get; set; } = "";
    public string? PropertyOwnerEmailAddress { get; set; }
    public string PropertyOwnerPhoneNumber { get; set; } = "";
    public int OpenTodoItemCount { get; set; }
    public int OpenPositionCount { get; set; }
    public int OpenDraftInvoiceCount { get; set; }
    public bool CanBeEnded { get; set; }
    public string CannotEndReason { get; set; } = "";
}

public class SaveProjectDetailsModel
{
    public bool ConnectionUserSameAsCustomer { get; set; }
    public string ConnectionUserFirstName { get; set; } = "";
    public string ConnectionUserLastName { get; set; } = "";
    public string ConnectionUserStreet { get; set; } = "";
    public string ConnectionUserHouseNumber { get; set; } = "";
    public string ConnectionUserPostalCode { get; set; } = "";
    public string ConnectionUserCity { get; set; } = "";
    public string ConnectionUserParcelNumber { get; set; } = "";
    public string? ConnectionUserEmailAddress { get; set; }
    public string ConnectionUserPhoneNumber { get; set; } = "";
    public bool PropertyOwnerSameAsCustomer { get; set; }
    public string PropertyOwnerFirstName { get; set; } = "";
    public string PropertyOwnerLastName { get; set; } = "";
    public string PropertyOwnerStreet { get; set; } = "";
    public string PropertyOwnerHouseNumber { get; set; } = "";
    public string PropertyOwnerPostalCode { get; set; } = "";
    public string PropertyOwnerCity { get; set; } = "";
    public string? PropertyOwnerEmailAddress { get; set; }
    public string PropertyOwnerPhoneNumber { get; set; } = "";
}

public class WorkTimeItem
{
    public int WorkTimeEntryId { get; set; }
    public int? AppUserId { get; set; }
    public string UserDisplayName { get; set; } = "";
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
    public bool IsClockActive { get; set; }
    public DateTime? PauseStartedAtUtc { get; set; }
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

public class StartWorkTimeClockModel
{
    public int CustomerId { get; set; }
    public int? ProjectId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public decimal HourlyRate { get; set; } = 65m;
    public decimal TravelRatePerKilometer { get; set; }
    public string Description { get; set; } = "Arbeitszeit";
}

public class ChangeWorkTimePauseModel
{
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.Now;
}

public class StopWorkTimeClockModel
{
    public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.Now;
    public decimal TravelKilometers { get; set; }
    public string Comment { get; set; } = "";
}

public class TodoListItem
{
    public int TodoListId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int OpenItemCount { get; set; }
    public int CompletedItemCount { get; set; }
    public List<TodoItem> Items { get; set; } = [];
    public List<TodoAttachmentItem> Attachments { get; set; } = [];
}

public class TodoItem
{
    public int TodoItemId { get; set; }
    public int TodoListId { get; set; }
    public int? ParentTodoItemId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public List<TodoItem> Children { get; set; } = [];
}

public class TodoAttachmentItem
{
    public int TodoAttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Caption { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = "";
}

public class TodoAttachmentContentItem
{
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "";
}

public class CalendarUserItem
{
    public int AppUserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class CalendarEntryItem
{
    public int CalendarEntryId { get; set; }
    public int AppUserId { get; set; }
    public string UserDisplayName { get; set; } = "";
    public DateTime EntryDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public bool CanEdit { get; set; }
}

public class SaveCalendarEntryModel
{
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(8, 0, 0);
    public TimeSpan EndTime { get; set; } = new(9, 0, 0);
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
}

public class AnalyticsResponseItem
{
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
    public decimal OpenRevenue { get; set; }
    public List<AnalyticsMonthItem> Monthly { get; set; } = [];
    public List<ProjectAnalyticsItem> Projects { get; set; } = [];
    public List<ExpenseCategoryItem> ExpenseCategories { get; set; } = [];
}

public class AnalyticsMonthItem
{
    public string Label { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public double RevenueHeight { get; set; }
    public double ExpenseHeight { get; set; }
    public decimal Profit => Revenue - Expenses;
}

public class ProjectAnalyticsItem
{
    public string CustomerName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public decimal PaidRevenue { get; set; }
    public decimal OpenRevenue { get; set; }
    public decimal LoggedHours { get; set; }
    public int OpenItemCount { get; set; }
}

public class ExpenseCategoryItem
{
    public string AccountingCategory { get; set; } = "";
    public decimal Amount { get; set; }

    public string AccountingCategoryLabel => AccountingCategory switch
    {
        "Tools" => "Werkzeug",
        "Services" => "Dienstleistungen",
        "Office" => "Buero",
        "Vehicle" => "Fahrzeug",
        "Other" => "Sonstiges",
        _ => "Material und Waren"
    };
}
