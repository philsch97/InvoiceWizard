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
    private List<CalendarEntryEntity> _weekEntries = new();
    private List<CalendarEntryEntity> _dayEntries = new();
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

    private async void NewEntry_Click(object sender, RoutedEventArgs e)
    {
        await OpenEntryDialogAsync((SelectedDatePicker.SelectedDate ?? DateTime.Today).Date, null);
    }

    private async void WeekDayCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not DateTime date)
        {
            return;
        }

        SelectedDatePicker.SelectedDate = date.Date;
        await ReloadAllAsync();

        if ((UserCombo.SelectedItem as CalendarUserEntity)?.CanEdit == true)
        {
            await OpenEntryDialogAsync(date.Date, null);
        }
    }

    private async void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CalendarEntryEntity entry)
        {
            await OpenEntryDialogAsync(entry.EntryDate, entry);
        }
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not CalendarEntryEntity entry)
        {
            return;
        }

        if (!entry.CanEdit)
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
            await App.Api.DeleteCalendarEntryAsync(entry.CalendarEntryId);
            await ReloadAllAsync();
            SetStatus("Termin geloescht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Termin konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async Task OpenEntryDialogAsync(DateTime date, CalendarEntryEntity? existingEntry)
    {
        var selectedUser = UserCombo.SelectedItem as CalendarUserEntity;
        if (selectedUser == null || !selectedUser.CanEdit)
        {
            SetStatus("Du kannst nur deinen eigenen Kalender bearbeiten.", StatusMessageType.Warning);
            return;
        }

        var dialog = new CalendarEntryDialog(date, existingEntry)
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

        DayEntriesGrid.ItemsSource = _dayEntries;
        DayEntriesTitleText.Text = $"Termine am {selectedDate:dd.MM.yyyy}";
        WeekRangeText.Text = $"Woche {weekStart:dd.MM.yyyy} - {weekStart.AddDays(6):dd.MM.yyyy}";
        CalendarModeInfoText.Text = _viewMode == CalendarViewMode.Week ? "Wochenansicht" : "Tagesansicht";
        SelectedCalendarInfoText.Text = selectedUser.CanEdit
            ? $"Du bearbeitest deinen Kalender fuer den {selectedDate:dd.MM.yyyy}."
            : $"Du siehst den Kalender von {selectedUser.DisplayName} im Lesemodus.";

        WeekCardsBorder.Visibility = _viewMode == CalendarViewMode.Week ? Visibility.Visible : Visibility.Collapsed;
        NewEntryButton.Visibility = selectedUser.CanEdit ? Visibility.Visible : Visibility.Collapsed;

        BuildWeekCards(weekStart, selectedDate);
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
                PreviewLines = entries.Take(3).Select(x => new CalendarPreviewLine
                {
                    Primary = $"{x.StartTime:hh\\:mm} {x.Title}",
                    Secondary = string.IsNullOrWhiteSpace(x.Location) ? x.Description : x.Location
                }).ToList(),
                IsSelected = day == selectedDate,
                EntryCount = entries.Count
            });
        }

        WeekDaysItemsControl.ItemsSource = null;
        WeekDaysItemsControl.ItemsSource = _weekCards;
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
        public int EntryCount { get; set; }
        public bool IsSelected { get; set; }
        public List<CalendarPreviewLine> PreviewLines { get; set; } = new();
    }

    private sealed class CalendarPreviewLine
    {
        public string Primary { get; set; } = "";
        public string Secondary { get; set; } = "";
    }
}
