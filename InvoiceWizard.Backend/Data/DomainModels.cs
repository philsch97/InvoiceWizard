namespace InvoiceWizard.Backend.Domain;

public class Customer
{
    public int CustomerId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public decimal DefaultMarkupPercent { get; set; }
    public List<Project> Projects { get; set; } = new();
    public List<LineAllocation> Allocations { get; set; } = new();
    public List<WorkTimeEntry> WorkTimeEntries { get; set; } = new();
    public List<TodoList> TodoLists { get; set; } = new();
}

public class Project
{
    public int ProjectId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool ConnectionUserSameAsCustomer { get; set; }
    public string ConnectionUserFirstName { get; set; } = "";
    public string ConnectionUserLastName { get; set; } = "";
    public string ConnectionUserStreet { get; set; } = "";
    public string ConnectionUserHouseNumber { get; set; } = "";
    public string ConnectionUserPostalCode { get; set; } = "";
    public string ConnectionUserCity { get; set; } = "";
    public string ConnectionUserParcelNumber { get; set; } = "";
    public string ConnectionUserEmailAddress { get; set; } = "";
    public string ConnectionUserPhoneNumber { get; set; } = "";
    public bool PropertyOwnerSameAsCustomer { get; set; }
    public string PropertyOwnerFirstName { get; set; } = "";
    public string PropertyOwnerLastName { get; set; } = "";
    public string PropertyOwnerStreet { get; set; } = "";
    public string PropertyOwnerHouseNumber { get; set; } = "";
    public string PropertyOwnerPostalCode { get; set; } = "";
    public string PropertyOwnerCity { get; set; } = "";
    public string PropertyOwnerEmailAddress { get; set; } = "";
    public string PropertyOwnerPhoneNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<LineAllocation> Allocations { get; set; } = new();
    public List<WorkTimeEntry> WorkTimeEntries { get; set; } = new();
    public List<TodoList> TodoLists { get; set; } = new();
}

public class Invoice
{
    public int InvoiceId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string SupplierName { get; set; } = "Sonepar";
    public string SourcePdfPath { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public int InvoiceLineId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int Position { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal PriceBasisQuantity { get; set; } = 1m;
    public decimal LineTotal { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public List<LineAllocation> Allocations { get; set; } = new();
}

public class LineAllocation
{
    public int LineAllocationId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int InvoiceLineId { get; set; }
    public InvoiceLine InvoiceLine { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal CustomerUnitPrice { get; set; }
    public bool IsSmallMaterial { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerInvoiceNumber { get; set; }
    public DateTime? CustomerInvoicedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal ExportedMarkupPercent { get; set; }
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
}

public class WorkTimeEntry
{
    public int WorkTimeEntryId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
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
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TodoList
{
    public int TodoListId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<TodoItem> Items { get; set; } = new();
    public List<TodoAttachment> Attachments { get; set; } = new();
}

public class TodoItem
{
    public int TodoItemId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int TodoListId { get; set; }
    public TodoList TodoList { get; set; } = null!;
    public int? ParentTodoItemId { get; set; }
    public TodoItem? ParentTodoItem { get; set; }
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TodoItem> Children { get; set; } = new();
}

public class TodoAttachment
{
    public int TodoAttachmentId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int TodoListId { get; set; }
    public TodoList TodoList { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Caption { get; set; } = "";
    public long FileSize { get; set; }
    public byte[] Data { get; set; } = [];
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
