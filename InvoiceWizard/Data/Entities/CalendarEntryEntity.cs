using System.Collections.Generic;

namespace InvoiceWizard.Data.Entities;

public class CalendarUserEntity
{
    public int AppUserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool IsCurrentUser { get; set; }
    public override string ToString() => DisplayName;
}

public class CalendarEntryEntity
{
    public int CalendarEntryId { get; set; }
    public int AppUserId { get; set; }
    public string UserDisplayName { get; set; } = "";
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerStreet { get; set; } = "";
    public string CustomerHouseNumber { get; set; } = "";
    public string CustomerPostalCode { get; set; } = "";
    public string CustomerCity { get; set; } = "";
    public DateTime EntryDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public bool CanEdit { get; set; }
    public string TimeRange => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    public string DayLabel => EntryDate.ToString("ddd, dd.MM.yyyy");
    public string CustomerAddress
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var street = $"{CustomerStreet} {CustomerHouseNumber}".Trim();
            var city = $"{CustomerPostalCode} {CustomerCity}".Trim();
            if (!string.IsNullOrWhiteSpace(street))
            {
                parts.Add(street);
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                parts.Add(city);
            }

            return string.Join(", ", parts);
        }
    }
}
