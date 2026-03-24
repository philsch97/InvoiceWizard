using InvoiceWizard.Data.Entities;
using InvoiceWizard.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class CalendarPage : Page
{
    private readonly List<CalendarDayCardViewModel> _weekCards = new();
    private List<CalendarUserEntity> _users = new();
    private List<CustomerEntity> _customers = new();
    private List<CalendarEntryEntity> _weekEntries = new();
    private List<CalendarEntryEntity> _dayEntries = new();
    private CalendarEntryEntity? _selectedEntry;
    private bool _isLoading;
    private CalendarViewMode _viewMode = CalendarViewMode.Week;

    public CalendarPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            _isLoading = true;
            SelectedDatePicker.SelectedDate ??= DateTime.Today;
            _users = await App.Api.GetCalendarUsersAsync();
            _customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
            UserCombo.ItemsSource = _users;
            UserCombo.SelectedItem = _users.FirstOrDefault(x => x.IsCurrentUser) ?? _users.FirstOrDefault();
            await ReloadAllAsync();
            SetStatus("Kalenderdaten geladen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Kalender konnte nicht geladen werden: {ex.Message}", StatusMessageType.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void UserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading)
        {
            await ReloadAllAsync();
        }
    }

    private async void SelectedDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading && IsLoaded)
        {
            await ReloadAllAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAllAsync();

    private async void PreviousRange_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = (_viewMode == CalendarViewMode.Week
            ? (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(-7)
            : (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(-1)).Date;
        await ReloadAllAsync();
    }

    private async void NextRange_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = (_viewMode == CalendarViewMode.Week
            ? (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(7)
            : (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(1)).Date;
        await ReloadAllAsync();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = DateTime.Today;
        await ReloadAllAsync();
    }

    private async void DayView_Click(object sender, RoutedEventArgs e)
    {
        _viewMode = CalendarViewMode.Day;
        await ReloadAllAsync();
    }

    private async void WeekView_Click(object sender, RoutedEventArgs e)
    {
        _viewMode = CalendarViewMode.Week;
        await ReloadAllAsync();
    }

    private async void WeekDayCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not DateTime date)
        {
            return;
        }

        SelectedDatePicker.SelectedDate = date.Date;
        await ReloadAllAsync();
    }

    private async void NewEntryForDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not DateTime date)
        {
            return;
        }

        await OpenEntryDialogAsync(date.Date, null);
    }

    private void CalendarEntryCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not CalendarEntryEntity entry)
        {
            return;
        }

        _selectedEntry = entry;
        UpdateEntryDetails(entry);
    }

    private async Task OpenEntryDialogAsync(DateTime date, CalendarEntryEntity? existingEntry)
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        if (selectedUser == null || !selectedUser.CanEdit)
        {
            SetStatus("Du kannst nur deinen eigenen Kalender bearbeiten.", StatusMessageType.Warning);
            return;
        }

        var dialog = new CalendarEntryDialog(date, _customers, existingEntry)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        try
        {
            var payload = dialog.Result;
            payload.AppUserId = selectedUser.AppUserId;
            payload.UserDisplayName = selectedUser.DisplayName;
            await App.Api.SaveCalendarEntryAsync(payload);
            SelectedDatePicker.SelectedDate = payload.EntryDate.Date;
            await ReloadAllAsync();
            _selectedEntry = existingEntry is null
                ? _dayEntries.OrderByDescending(x => x.CalendarEntryId).FirstOrDefault(x => x.EntryDate.Date == payload.EntryDate.Date)
                : _dayEntries.FirstOrDefault(x => x.CalendarEntryId == existingEntry.CalendarEntryId);
            UpdateEntryDetails(_selectedEntry);
            SetStatus(existingEntry is null ? "Termin erstellt." : "Termin gespeichert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Termin konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async Task ReloadAllAsync()
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        var selectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).Date;
        if (selectedUser == null)
        {
            return;
        }

        var weekStart = GetWeekStart(selectedDate);
        _weekEntries = await App.Api.GetCalendarEntriesAsync(selectedUser.AppUserId, weekStart, weekStart.AddDays(6));
        _dayEntries = _weekEntries.Where(x => x.EntryDate.Date == selectedDate).OrderBy(x => x.StartTime).ToList();
        WeekRangeText.Text = $"Woche {weekStart:dd.MM.yyyy} - {weekStart.AddDays(6):dd.MM.yyyy}";
        CalendarModeInfoText.Text = _viewMode == CalendarViewMode.Week ? "Wochenansicht" : "Tagesansicht";
        SelectedCalendarInfoText.Text = selectedUser.CanEdit
            ? $"Du bearbeitest deinen Kalender fuer den {selectedDate:dd.MM.yyyy}."
            : $"Du siehst den Kalender von {selectedUser.DisplayName} im Lesemodus.";
        CalendarCardsHintText.Text = _viewMode == CalendarViewMode.Week
            ? "Termine werden direkt in den Tageskarten angezeigt. Ein Klick auf einen Termin zeigt die Details."
            : "Die Tagesansicht zeigt alle Termine direkt im ausgewaehlten Tag. Mit dem Plus legst du neue Termine sofort am richtigen Datum an.";
        WeekDaysItemsControl.Visibility = _viewMode == CalendarViewMode.Week ? Visibility.Visible : Visibility.Collapsed;
        DayCardScrollViewer.Visibility = _viewMode == CalendarViewMode.Day ? Visibility.Visible : Visibility.Collapsed;

        BuildWeekCards(weekStart, selectedDate);
        BuildDayCard(selectedDate);

        _selectedEntry = _selectedEntry is not null
            ? _weekEntries.FirstOrDefault(x => x.CalendarEntryId == _selectedEntry.CalendarEntryId)
            : null;

        if (_selectedEntry is null)
        {
            _selectedEntry = _dayEntries.FirstOrDefault();
        }

        UpdateEntryDetails(_selectedEntry);
    }

    private void BuildWeekCards(DateTime weekStart, DateTime selectedDate)
    {
        _weekCards.Clear();
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var day = weekStart.AddDays(dayOffset).Date;
            var entries = _weekEntries.Where(x => x.EntryDate.Date == day).OrderBy(x => x.StartTime).ToList();
            _weekCards.Add(new CalendarDayCardViewModel
            {
                Date = day,
                Title = day.ToString("dddd"),
                Subtitle = day.ToString("dd.MM.yyyy"),
                Entries = entries.Select(CreateEntryCard).ToList(),
                EmptyText = day == selectedDate
                    ? "Noch keine Termine fuer den ausgewaehlten Tag."
                    : "Keine Termine.",
                EmptyVisibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
                NewEntryButtonVisibility = (UserCombo.SelectedItem as CalendarUserEntity)?.CanEdit == true ? Visibility.Visible : Visibility.Collapsed
            });
        }

        WeekDaysItemsControl.ItemsSource = null;
        WeekDaysItemsControl.ItemsSource = _weekCards;
    }

    private void BuildDayCard(DateTime selectedDate)
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        var entries = _dayEntries.OrderBy(x => x.StartTime).ToList();
        var dayCards = new List<CalendarDayCardViewModel>
        {
            new()
            {
                Date = selectedDate,
                Title = selectedDate.ToString("dddd"),
                Subtitle = selectedDate.ToString("dd.MM.yyyy"),
                Entries = entries.Select(CreateEntryCard).ToList(),
                EmptyText = "Noch keine Termine fuer diesen Tag.",
                EmptyVisibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
                NewEntryButtonVisibility = selectedUser?.CanEdit == true ? Visibility.Visible : Visibility.Collapsed
            }
        };

        DayCardsItemsControl.ItemsSource = null;
        DayCardsItemsControl.ItemsSource = dayCards;
    }

    private static CalendarEntryCardViewModel CreateEntryCard(CalendarEntryEntity entry)
    {
        var secondaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Location))
        {
            secondaryParts.Add(entry.Location);
        }

        if (!string.IsNullOrWhiteSpace(entry.CustomerName))
        {
            secondaryParts.Add(entry.CustomerName);
        }

        var tertiary = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : entry.Description;

        return new CalendarEntryCardViewModel
        {
            Entry = entry,
            Primary = $"{entry.TimeRange} {entry.Title}".Trim(),
            Secondary = secondaryParts.Count == 0 ? "Ohne weiteren Hinweis" : string.Join(" | ", secondaryParts),
            Tertiary = tertiary,
            TertiaryVisibility = string.IsNullOrWhiteSpace(tertiary) ? Visibility.Collapsed : Visibility.Visible
        };
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff).Date;
    }

    private void UpdateEntryDetails(CalendarEntryEntity? entry)
    {
        if (entry is null)
        {
            EntryDetailsTitleText.Text = "Noch kein Termin ausgewaehlt.";
            EntryDetailsMetaText.Text = "Waehle einen Termin direkt in der Tageskarte aus, um die Details zu sehen.";
            EntryDetailsCustomerText.Text = string.Empty;
            EntryDetailsAddressText.Text = string.Empty;
            EntryDetailsDescriptionText.Text = string.Empty;
            DetailEditButton.IsEnabled = false;
            DetailDeleteButton.IsEnabled = false;
            return;
        }

        EntryDetailsTitleText.Text = entry.Title;
        EntryDetailsMetaText.Text = $"{entry.DayLabel} | {entry.TimeRange} | {entry.UserDisplayName}";
        EntryDetailsCustomerText.Text = string.IsNullOrWhiteSpace(entry.CustomerName)
            ? "Kein Kunde verknuepft."
            : $"Kunde: {entry.CustomerName}";
        EntryDetailsAddressText.Text = string.IsNullOrWhiteSpace(entry.CustomerName)
            ? string.Empty
            : $"Adresse: {entry.CustomerAddress}";
        EntryDetailsDescriptionText.Text = string.IsNullOrWhiteSpace(entry.Description)
            ? (string.IsNullOrWhiteSpace(entry.Location) ? string.Empty : $"Ort: {entry.Location}")
            : $"{(string.IsNullOrWhiteSpace(entry.Location) ? string.Empty : $"Ort: {entry.Location}\n")}{entry.Description}";
        DetailEditButton.IsEnabled = entry.CanEdit;
        DetailDeleteButton.IsEnabled = entry.CanEdit;
    }

    private async void EditSelectedEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null)
        {
            SetStatus("Bitte zuerst einen Termin auswaehlen.", StatusMessageType.Warning);
            return;
        }

        await OpenEntryDialogAsync(_selectedEntry.EntryDate, _selectedEntry);
    }

    private async void DeleteSelectedEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null)
        {
            SetStatus("Bitte zuerst einen Termin auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!_selectedEntry.CanEdit)
        {
            SetStatus("Dieser Termin ist schreibgeschuetzt.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show("Soll der Termin wirklich geloescht werden?", "Termin loeschen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await App.Api.DeleteCalendarEntryAsync(_selectedEntry.CalendarEntryId);
            _selectedEntry = null;
            await ReloadAllAsync();
            SetStatus("Termin geloescht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Termin konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void SetStatus(string message, StatusMessageType type)
    {
        StatusText.Text = message;
        StatusBorder.Background = GetBrush(type, "Background");
        StatusBorder.BorderBrush = GetBrush(type, "Border");
        StatusText.Foreground = GetBrush(type, "Text");
    }

    private Brush GetBrush(StatusMessageType type, string variant)
    {
        var key = type switch
        {
            StatusMessageType.Success => $"StatusSuccess{variant}Brush",
            StatusMessageType.Warning => $"StatusWarning{variant}Brush",
            StatusMessageType.Error => $"StatusError{variant}Brush",
            _ => $"StatusInfo{variant}Brush"
        };

        return (Brush)FindResource(key);
    }

    private enum CalendarViewMode
    {
        Day,
        Week
    }

    private sealed class CalendarDayCardViewModel
    {
        public DateTime Date { get; set; }
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string EmptyText { get; set; } = "";
        public Visibility EmptyVisibility { get; set; } = Visibility.Collapsed;
        public Visibility NewEntryButtonVisibility { get; set; } = Visibility.Collapsed;
        public List<CalendarEntryCardViewModel> Entries { get; set; } = new();
    }

    private sealed class CalendarEntryCardViewModel
    {
        public CalendarEntryEntity Entry { get; set; } = new();
        public string Primary { get; set; } = "";
        public string Secondary { get; set; } = "";
        public string Tertiary { get; set; } = "";
        public Visibility TertiaryVisibility { get; set; } = Visibility.Collapsed;
    }
}
