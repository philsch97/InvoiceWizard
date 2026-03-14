using InvoiceWizard.Data.Entities;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class CalendarPage : Page
{
    private List<CalendarUserEntity> _users = new();
    private List<CalendarEntryEntity> _selectedUserEntries = new();
    private CalendarEntryEntity? _selectedEntry;
    private bool _isLoading;

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
            EntryDatePicker.SelectedDate ??= SelectedDatePicker.SelectedDate;
            _users = await App.Api.GetCalendarUsersAsync();
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
        if (_isLoading)
        {
            return;
        }

        await ReloadAllAsync();
    }

    private async void SelectedDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || !IsLoaded)
        {
            return;
        }

        EntryDatePicker.SelectedDate ??= SelectedDatePicker.SelectedDate;
        await ReloadAllAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAllAsync();
    }

    private async void PreviousWeek_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(-7);
        await ReloadAllAsync();
    }

    private async void NextWeek_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).AddDays(7);
        await ReloadAllAsync();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        SelectedDatePicker.SelectedDate = DateTime.Today;
        await ReloadAllAsync();
    }

    private async void SaveEntry_Click(object sender, RoutedEventArgs e)
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        if (selectedUser == null)
        {
            SetStatus("Bitte zuerst einen Benutzer auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!selectedUser.CanEdit)
        {
            SetStatus("Du kannst nur deinen eigenen Kalender bearbeiten.", StatusMessageType.Warning);
            return;
        }

        if (!TryBuildEntry(selectedUser, out var entry, out var error))
        {
            SetStatus(error, StatusMessageType.Error);
            return;
        }

        try
        {
            var saved = await App.Api.SaveCalendarEntryAsync(entry);
            _selectedEntry = saved;
            await ReloadAllAsync();
            SelectEntryById(saved.CalendarEntryId);
            SetStatus("Termin gespeichert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Termin konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void NewEntry_Click(object sender, RoutedEventArgs e)
    {
        _selectedEntry = null;
        PopulateEditor(null);
        SetStatus("Neuer Termin vorbereitet.", StatusMessageType.Info);
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry == null || _selectedEntry.CalendarEntryId <= 0)
        {
            SetStatus("Bitte zuerst einen eigenen Termin auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!_selectedEntry.CanEdit)
        {
            SetStatus("Fremde Termine koennen nicht geloescht werden.", StatusMessageType.Warning);
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
            PopulateEditor(null);
            SetStatus("Termin geloescht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Termin konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void UserEntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = UserEntriesGrid.SelectedItem as CalendarEntryEntity;
        PopulateEditor(_selectedEntry);
    }

    private async Task ReloadAllAsync()
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        var selectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).Date;
        EntryDatePicker.SelectedDate ??= selectedDate;

        if (selectedUser == null)
        {
            UserEntriesGrid.ItemsSource = null;
            WeekOverviewGrid.ItemsSource = null;
            UpdateEditorState();
            return;
        }

        var weekStart = GetWeekStart(selectedDate);
        _selectedUserEntries = await App.Api.GetCalendarEntriesAsync(selectedUser.AppUserId, selectedDate, selectedDate);
        var weeklyOverview = await App.Api.GetCalendarWeeklyOverviewAsync(weekStart);

        UserEntriesGrid.ItemsSource = _selectedUserEntries;
        WeekOverviewGrid.ItemsSource = weeklyOverview;
        WeekOverviewTitleText.Text = $"Wochenuebersicht aller Benutzer ({weekStart:dd.MM.yyyy} - {weekStart.AddDays(6):dd.MM.yyyy})";
        SelectedCalendarInfoText.Text = selectedUser.CanEdit
            ? $"Du bearbeitest deinen Kalender fuer den {selectedDate:dd.MM.yyyy}."
            : $"Du siehst den Kalender von {selectedUser.DisplayName} fuer den {selectedDate:dd.MM.yyyy} im Lesemodus.";

        if (_selectedEntry != null)
        {
            SelectEntryById(_selectedEntry.CalendarEntryId);
        }
        else if (_selectedUserEntries.Count > 0)
        {
            UserEntriesGrid.SelectedIndex = 0;
        }
        else
        {
            PopulateEditor(null);
        }

        UpdateEditorState();
    }

    private void SelectEntryById(int calendarEntryId)
    {
        var match = _selectedUserEntries.FirstOrDefault(x => x.CalendarEntryId == calendarEntryId);
        if (match == null)
        {
            UserEntriesGrid.SelectedItem = null;
            PopulateEditor(null);
            return;
        }

        UserEntriesGrid.SelectedItem = match;
        UserEntriesGrid.ScrollIntoView(match);
    }

    private bool TryBuildEntry(CalendarUserEntity selectedUser, out CalendarEntryEntity entry, out string error)
    {
        entry = new CalendarEntryEntity();
        error = string.Empty;

        var title = (TitleText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "Bitte einen Titel eingeben.";
            return false;
        }

        if (EntryDatePicker.SelectedDate == null)
        {
            error = "Bitte ein Datum auswaehlen.";
            return false;
        }

        if (!TimeSpan.TryParseExact((StartTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime))
        {
            error = "Bitte eine gueltige Startzeit im Format HH:mm eingeben.";
            return false;
        }

        if (!TimeSpan.TryParseExact((EndTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            error = "Bitte eine gueltige Endzeit im Format HH:mm eingeben.";
            return false;
        }

        if (endTime <= startTime)
        {
            error = "Die Endzeit muss nach der Startzeit liegen.";
            return false;
        }

        entry = new CalendarEntryEntity
        {
            CalendarEntryId = _selectedEntry?.CalendarEntryId ?? 0,
            AppUserId = selectedUser.AppUserId,
            UserDisplayName = selectedUser.DisplayName,
            EntryDate = EntryDatePicker.SelectedDate.Value.Date,
            StartTime = startTime,
            EndTime = endTime,
            Title = title,
            Description = (DescriptionText.Text ?? string.Empty).Trim(),
            Location = (LocationText.Text ?? string.Empty).Trim(),
            CanEdit = true
        };

        return true;
    }

    private void PopulateEditor(CalendarEntryEntity? entry)
    {
        var selectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).Date;
        EntryDatePicker.SelectedDate = entry?.EntryDate ?? selectedDate;
        TitleText.Text = entry?.Title ?? string.Empty;
        LocationText.Text = entry?.Location ?? string.Empty;
        StartTimeText.Text = (entry?.StartTime ?? TimeSpan.FromHours(8)).ToString("hh\\:mm");
        EndTimeText.Text = (entry?.EndTime ?? TimeSpan.FromHours(9)).ToString("hh\\:mm");
        DescriptionText.Text = entry?.Description ?? string.Empty;
    }

    private void UpdateEditorState()
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        var canEdit = selectedUser?.CanEdit == true;
        EditorInfoText.Text = canEdit
            ? "Du kannst hier deine eigenen Termine anlegen, aendern und loeschen."
            : "Der ausgewaehlte Kalender ist schreibgeschuetzt. Zum Bearbeiten bitte deinen eigenen Benutzer waehlen.";

        foreach (var control in new Control[] { TitleText, EntryDatePicker, LocationText, StartTimeText, EndTimeText, DescriptionText })
        {
            control.IsEnabled = canEdit;
        }
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff).Date;
    }

    private void SetStatus(string message, StatusMessageType type)
    {
        StatusText.Text = message;
        StatusBorder.Background = GetBrush(type, "Background");
        StatusBorder.BorderBrush = GetBrush(type, "Border");
        StatusText.Foreground = GetBrush(type, "Text");
    }

    private Brush GetBrush(StatusMessageType type, string part)
    {
        var key = $"Status{type}{part}Brush";
        return (Brush)FindResource(key);
    }
}
