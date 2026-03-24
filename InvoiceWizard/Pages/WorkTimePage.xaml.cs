using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
using InvoiceWizard.Services;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class WorkTimePage : Page
{
    private WorkTimeEntryEntity? _activeClock;

    public WorkTimePage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await ReloadAsync();
            SetStatus("Bereit fuer die Projektzeiterfassung.", StatusMessageType.Info);
        };
    }

    private bool IsAdmin => string.Equals(App.Session?.User.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private async void StartProject_Click(object sender, RoutedEventArgs e)
    {
        if (_activeClock is not null)
        {
            SetStatus("Es laeuft bereits eine aktive Projektzeit.", StatusMessageType.Warning);
            return;
        }

        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId == null)
        {
            SetStatus("Bitte ein konkretes Projekt auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseDecimal(HourlyRateText.Text, out var hourlyRate) || hourlyRate <= 0m)
        {
            SetStatus("Bitte einen gueltigen Stundensatz eingeben.", StatusMessageType.Error);
            return;
        }

        if (!TryParseDecimal(TravelRateText.Text, out var travelRatePerKilometer) || travelRatePerKilometer < 0m)
        {
            SetStatus("Bitte einen gueltigen Kilometerpreis eingeben.", StatusMessageType.Error);
            return;
        }

        var description = string.IsNullOrWhiteSpace(DescriptionText.Text) ? "Arbeitszeit" : DescriptionText.Text.Trim();

        try
        {
            _activeClock = await App.Api.StartWorkTimeClockAsync(customer.CustomerId, projectSelection.ProjectId, hourlyRate, travelRatePerKilometer, description, DateTimeOffset.Now);
            await ReloadAsync();
            SetStatus($"Projektzeit fuer {projectSelection.Name} gestartet.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Projektstart fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void PauseProject_Click(object sender, RoutedEventArgs e)
    {
        if (_activeClock is null)
        {
            SetStatus("Es laeuft aktuell keine Projektzeit.", StatusMessageType.Warning);
            return;
        }

        try
        {
            _activeClock = _activeClock.PauseStartedAtUtc.HasValue
                ? await App.Api.StopWorkTimePauseAsync(DateTimeOffset.Now)
                : await App.Api.StartWorkTimePauseAsync(DateTimeOffset.Now);

            await ReloadAsync();
            SetStatus(_activeClock.PauseStartedAtUtc.HasValue ? "Pause gestartet." : $"Pause beendet. Gesamtpause: {_activeClock.BreakMinutes} Minuten.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Pause konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void StopProject_Click(object sender, RoutedEventArgs e)
    {
        if (_activeClock is null)
        {
            SetStatus("Es laeuft aktuell keine Projektzeit.", StatusMessageType.Warning);
            return;
        }

        var dialog = new WorkTimeStopDialog(_activeClock.TravelKilometers)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await App.Api.StopWorkTimeClockAsync(DateTimeOffset.Now, dialog.TravelKilometers, dialog.Comment);
            _activeClock = null;
            await ReloadAsync();
            SetStatus("Projektzeit beendet und gespeichert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Projektstopp fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var selectedProject = ProjectCombo.SelectedItem as ProjectSelectionItem;
        var entries = await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProject?.ProjectId);
        entries = entries.OrderBy(w => w.WorkDate).ThenBy(w => w.StartTime).ToList();

        if (entries.Count == 0)
        {
            SetStatus("Fuer die aktuelle Auswahl gibt es keine Arbeitszeiten fuer den PDF-Export.", StatusMessageType.Warning);
            return;
        }

        var projectSuffix = selectedProject?.ProjectId.HasValue == true ? $"_{selectedProject.Name}" : "";
        var saveDialog = new SaveFileDialog
        {
            Filter = "PDF-Datei (*.pdf)|*.pdf",
            FileName = $"{customer.Name}{projectSuffix}_arbeitszeiten_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        WorkTimePdfExportService.Export(saveDialog.FileName, customer.Name, selectedProject?.ProjectId.HasValue == true ? selectedProject.Name : null,
            entries.Select(entry => new WorkTimePdfExportService.ExportRow
            {
                DateText = entry.WorkDate.ToString("dd.MM.yyyy"),
                ProjectName = entry.Project?.Name ?? "Ohne Projekt",
                TimeRange = entry.TimeRange,
                HoursWorked = entry.HoursWorked,
                Description = entry.TravelKilometers > 0m
                    ? $"{entry.Description} (Anfahrt: {entry.TravelKilometers:0.##} km x {entry.TravelRatePerKilometer:0.00} EUR)"
                    : entry.Description,
                Comment = entry.Comment
            }));

        SetStatus($"PDF-Datei erstellt: {saveDialog.FileName}", StatusMessageType.Success);
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = WorkEntriesGrid.SelectedItems.OfType<WorkTimeEntryEntity>().ToList();
        if (selectedEntries.Count == 0)
        {
            SetStatus("Bitte mindestens einen Zeiteintrag markieren.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Sollen {selectedEntries.Count} markierte Zeiteintraege wirklich geloescht werden?", "Zeiteintraege loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var entry in selectedEntries)
        {
            await App.Api.DeleteWorkTimeAsync(entry.WorkTimeEntryId);
        }

        await ReloadAsync();
        SetStatus($"{selectedEntries.Count} Zeiteintraege wurden geloescht.", StatusMessageType.Success);
    }

    private async void EditSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdmin)
        {
            SetStatus("Nur Admins duerfen Arbeitszeiten nachtraeglich bearbeiten.", StatusMessageType.Warning);
            return;
        }

        if (WorkEntriesGrid.SelectedItem is not WorkTimeEntryEntity entry)
        {
            SetStatus("Bitte zuerst genau einen Zeiteintrag markieren.", StatusMessageType.Warning);
            return;
        }

        if (entry.IsClockActive)
        {
            SetStatus("Laufende Projektzeiten koennen nicht manuell bearbeitet werden.", StatusMessageType.Warning);
            return;
        }

        var customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
        var projectMap = new Dictionary<int, List<ProjectSelectionItem>>();
        foreach (var customer in customers)
        {
            projectMap[customer.CustomerId] = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: false);
        }

        var dialog = new WorkTimeEditDialog(entry, customers, projectMap)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        try
        {
            await App.Api.SaveWorkTimeAsync(entry.WorkTimeEntryId, dialog.Result);
            await ReloadAsync();
            SetStatus("Arbeitszeit wurde aktualisiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Arbeitszeit konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        App.SetSelectedCustomer((CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId);
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
        await LoadEntriesAsync();
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadEntriesAsync();
    }

    private async Task ReloadAsync()
    {
        _activeClock = await App.Api.GetActiveWorkTimeClockAsync();
        await LoadEntriesAsync();
        UpdateClockUi();
    }

    private async Task LoadCustomersAsync(int? selectedCustomerId = null, int? selectedProjectId = null)
    {
        var customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            UpdateClockUi();
            return;
        }

        var preferredCustomerId = App.SelectedCustomerId ?? selectedCustomerId;
        var customer = preferredCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == preferredCustomerId.Value) ?? customers[0] : customers[0];
        CustomerCombo.SelectedItem = customer;
        App.SetSelectedCustomer(customer.CustomerId);
        await LoadProjectsAsync(customer, selectedProjectId);
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer, int? selectedProjectId = null)
    {
        if (customer == null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            return;
        }

        var items = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: false);
        ProjectCombo.ItemsSource = items;
        ProjectCombo.SelectedItem = selectedProjectId.HasValue ? items.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) : items.FirstOrDefault();
    }

    private async Task LoadEntriesAsync()
    {
        int? customerId = (CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId;
        int? projectId = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var entries = await App.Api.GetWorkTimeEntriesAsync(customerId, projectId);
        WorkEntriesGrid.ItemsSource = entries.OrderByDescending(w => w.WorkDate).ThenByDescending(w => w.StartTime).ToList();
    }

    private void UpdateClockUi()
    {
        var hasActiveClock = _activeClock is not null;
        CustomerCombo.IsEnabled = !hasActiveClock;
        ProjectCombo.IsEnabled = !hasActiveClock;
        HourlyRateText.IsEnabled = !hasActiveClock;
        TravelRateText.IsEnabled = !hasActiveClock;
        DescriptionText.IsEnabled = !hasActiveClock;
        PauseButton.IsEnabled = hasActiveClock;
        StopButton.IsEnabled = hasActiveClock;
        EditSelectedButton.IsEnabled = IsAdmin;

        if (_activeClock is null)
        {
            ActiveClockText.Text = "Zurzeit laeuft keine Projektzeit.";
            PauseInfoText.Visibility = Visibility.Collapsed;
            PauseButton.Content = "Pause starten";
            return;
        }

        ActiveClockText.Text = $"{_activeClock.Customer.Name} / {_activeClock.Project?.Name ?? "Ohne Projekt"} seit {_activeClock.StartTime:hh\\:mm} Uhr am {_activeClock.WorkDate:dd.MM.yyyy}.";
        if (_activeClock.PauseStartedAtUtc.HasValue)
        {
            PauseInfoText.Text = $"Pause laeuft seit {_activeClock.PauseStartedAtUtc.Value.ToLocalTime():HH:mm}. Bereits gespeichert: {_activeClock.BreakMinutes} Minuten.";
            PauseInfoText.Visibility = Visibility.Visible;
            PauseButton.Content = "Pause beenden";
        }
        else
        {
            PauseInfoText.Text = $"Aktuelle Pause gesamt: {_activeClock.BreakMinutes} Minuten.";
            PauseInfoText.Visibility = Visibility.Visible;
            PauseButton.Content = "Pause starten";
        }
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
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
}
