using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class DashboardSummaryDto
{
    public int CustomerCount { get; set; }
    public int ProjectCount { get; set; }
    public int OpenMaterialItemCount { get; set; }
    public int OpenWorkItemCount { get; set; }
    public decimal LoggedHoursCurrentMonth { get; set; }
    public decimal PaidRevenue { get; set; }
    public decimal OpenRevenue { get; set; }
}

public class CustomerListItemDto
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

public class SaveCustomerRequest
{
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(100)]
    public string FirstName { get; set; } = "";

    [MaxLength(100)]
    public string LastName { get; set; } = "";

    [MaxLength(200)]
    public string Street { get; set; } = "";

    [MaxLength(50)]
    public string HouseNumber { get; set; } = "";

    [MaxLength(20)]
    public string PostalCode { get; set; } = "";

    [MaxLength(120)]
    public string City { get; set; } = "";

    [MaxLength(200)]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    [MaxLength(50)]
    public string PhoneNumber { get; set; } = "";

    [Range(0, 1000)]
    public decimal DefaultMarkupPercent { get; set; }
}

public class ProjectListItemDto
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Name { get; set; } = "";
    public int OpenWorkItems { get; set; }
    public decimal LoggedHours { get; set; }
}

public class SaveProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";
}

public class ProjectDetailsDto
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Name { get; set; } = "";
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

public class SaveProjectDetailsRequest
{
    public bool ConnectionUserSameAsCustomer { get; set; }

    [MaxLength(100)]
    public string ConnectionUserFirstName { get; set; } = "";

    [MaxLength(100)]
    public string ConnectionUserLastName { get; set; } = "";

    [MaxLength(200)]
    public string ConnectionUserStreet { get; set; } = "";

    [MaxLength(50)]
    public string ConnectionUserHouseNumber { get; set; } = "";

    [MaxLength(20)]
    public string ConnectionUserPostalCode { get; set; } = "";

    [MaxLength(120)]
    public string ConnectionUserCity { get; set; } = "";

    [MaxLength(100)]
    public string ConnectionUserParcelNumber { get; set; } = "";

    [MaxLength(200)]
    [EmailAddress]
    public string? ConnectionUserEmailAddress { get; set; }

    [MaxLength(50)]
    public string ConnectionUserPhoneNumber { get; set; } = "";

    public bool PropertyOwnerSameAsCustomer { get; set; }

    [MaxLength(100)]
    public string PropertyOwnerFirstName { get; set; } = "";

    [MaxLength(100)]
    public string PropertyOwnerLastName { get; set; } = "";

    [MaxLength(200)]
    public string PropertyOwnerStreet { get; set; } = "";

    [MaxLength(50)]
    public string PropertyOwnerHouseNumber { get; set; } = "";

    [MaxLength(20)]
    public string PropertyOwnerPostalCode { get; set; } = "";

    [MaxLength(120)]
    public string PropertyOwnerCity { get; set; } = "";

    [MaxLength(200)]
    [EmailAddress]
    public string? PropertyOwnerEmailAddress { get; set; }

    [MaxLength(50)]
    public string PropertyOwnerPhoneNumber { get; set; } = "";
}

public class WorkTimeEntryListItemDto
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

public class SaveWorkTimeEntryRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }
    public int? ProjectId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    [Range(0, 1440)]
    public int BreakMinutes { get; set; }
    [Range(0.01, 10000)]
    public decimal HourlyRate { get; set; }
    [Range(0, 100000)]
    public decimal TravelKilometers { get; set; }
    [Range(0, 1000)]
    public decimal TravelRatePerKilometer { get; set; }
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = "Arbeitszeit";
    [MaxLength(2000)]
    public string Comment { get; set; } = "";
}

public class UpdateWorkTimeStatusRequest
{
    public string? CustomerInvoiceNumber { get; set; }
    public bool MarkInvoiced { get; set; }
    public bool MarkPaid { get; set; }
}

public class TodoListDto
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
    public List<TodoItemDto> Items { get; set; } = [];
    public List<TodoAttachmentDto> Attachments { get; set; } = [];
}

public class TodoItemDto
{
    public int TodoItemId { get; set; }
    public int TodoListId { get; set; }
    public int? ParentTodoItemId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public List<TodoItemDto> Children { get; set; } = [];
}

public class TodoAttachmentDto
{
    public int TodoAttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Caption { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = "";
}

public class SaveTodoListRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public int? ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";
}

public class SaveTodoItemRequest
{
    [Required]
    [MaxLength(300)]
    public string Text { get; set; } = "";

    public int? ParentTodoItemId { get; set; }
}

public class UpdateTodoItemStateRequest
{
    public bool IsCompleted { get; set; }
}



