namespace InvoiceWizard.Data.Entities;

public class ProjectEntity
{
    public int ProjectId { get; set; }
    public int CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
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
    public int OpenTodoItemCount { get; set; }
    public int OpenPositionCount { get; set; }
    public int OpenDraftInvoiceCount { get; set; }
    public bool CanBeEnded { get; set; } = true;
    public string CannotEndReason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<LineAllocationEntity> Allocations { get; set; } = new();
    public List<WorkTimeEntryEntity> WorkTimeEntries { get; set; } = new();

    public string ProjectStatusLabel => ProjectStatus switch
    {
        "Paused" => "Pausiert",
        "Ended" => "Beendet",
        _ => "Aktiv"
    };
}


