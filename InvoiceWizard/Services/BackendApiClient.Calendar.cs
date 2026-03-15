using System.Net.Http;
using System.Net.Http.Json;
using InvoiceWizard.Data.Entities;

namespace InvoiceWizard.Services;

public partial class BackendApiClient
{
    public async Task<List<CalendarUserEntity>> GetCalendarUsersAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<CalendarUserDto>>("api/calendar/users", _jsonOptions) ?? [];
        return items.OrderByDescending(x => x.IsCurrentUser).ThenBy(x => x.DisplayName).Select(MapCalendarUser).ToList();
    }

    public async Task<List<CalendarEntryEntity>> GetCalendarEntriesAsync(int appUserId, DateTime fromDate, DateTime toDate)
    {
        var url = $"api/calendar/entries?appUserId={appUserId}&fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}";
        var items = await _httpClient.GetFromJsonAsync<List<CalendarEntryDto>>(url, _jsonOptions) ?? [];
        return items.Select(MapCalendarEntry).ToList();
    }

    public async Task<List<CalendarEntryEntity>> GetCalendarWeeklyOverviewAsync(DateTime weekStart)
    {
        var items = await _httpClient.GetFromJsonAsync<List<CalendarEntryDto>>($"api/calendar/weekly-overview?weekStart={weekStart:yyyy-MM-dd}", _jsonOptions) ?? [];
        return items.Select(MapCalendarEntry).ToList();
    }

    public async Task<CalendarEntryEntity> SaveCalendarEntryAsync(CalendarEntryEntity entry)
    {
        var payload = new
        {
            entryDate = entry.EntryDate,
            customerId = entry.CustomerId,
            startTime = entry.StartTime,
            endTime = entry.EndTime,
            title = entry.Title,
            description = entry.Description,
            location = entry.Location
        };

        HttpResponseMessage response = entry.CalendarEntryId > 0
            ? await _httpClient.PutAsJsonAsync($"api/calendar/entries/{entry.CalendarEntryId}", payload)
            : await _httpClient.PostAsJsonAsync("api/calendar/entries", payload);

        response.EnsureSuccessStatusCode();
        return MapCalendarEntry((await response.Content.ReadFromJsonAsync<CalendarEntryDto>(_jsonOptions)) ?? new CalendarEntryDto());
    }

    public async Task DeleteCalendarEntryAsync(int calendarEntryId)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar/entries/{calendarEntryId}");
        response.EnsureSuccessStatusCode();
    }

    private static CalendarUserEntity MapCalendarUser(CalendarUserDto item)
    {
        return new CalendarUserEntity
        {
            AppUserId = item.AppUserId,
            DisplayName = item.DisplayName,
            Role = item.Role,
            CanEdit = item.CanEdit,
            IsCurrentUser = item.IsCurrentUser
        };
    }

    private static CalendarEntryEntity MapCalendarEntry(CalendarEntryDto item)
    {
        return new CalendarEntryEntity
        {
            CalendarEntryId = item.CalendarEntryId,
            AppUserId = item.AppUserId,
            UserDisplayName = item.UserDisplayName,
            CustomerId = item.CustomerId,
            CustomerName = item.CustomerName ?? "",
            CustomerStreet = item.CustomerStreet ?? "",
            CustomerHouseNumber = item.CustomerHouseNumber ?? "",
            CustomerPostalCode = item.CustomerPostalCode ?? "",
            CustomerCity = item.CustomerCity ?? "",
            EntryDate = item.EntryDate,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            Title = item.Title,
            Description = item.Description,
            Location = item.Location,
            UpdatedAt = item.UpdatedAt,
            CanEdit = item.CanEdit
        };
    }

    private class CalendarUserDto
    {
        public int AppUserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool CanEdit { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    private class CalendarEntryDto
    {
        public int CalendarEntryId { get; set; }
        public int AppUserId { get; set; }
        public string UserDisplayName { get; set; } = "";
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerStreet { get; set; }
        public string? CustomerHouseNumber { get; set; }
        public string? CustomerPostalCode { get; set; }
        public string? CustomerCity { get; set; }
        public DateTime EntryDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public bool CanEdit { get; set; }
    }
}
