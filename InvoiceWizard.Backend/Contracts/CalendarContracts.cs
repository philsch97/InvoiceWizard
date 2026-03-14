using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class CalendarUserDto
{
    public int AppUserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class CalendarEntryDto
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

public class SaveCalendarEntryRequest
{
    [Required]
    public DateTime EntryDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    [Required]
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
}
