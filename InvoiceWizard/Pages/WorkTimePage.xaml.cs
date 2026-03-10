using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class WorkTimePage : Page
{
    public WorkTimePage()
    {
        InitializeComponent();
        WorkDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            SetStatus("Bereit fuer die Zeiterfassung.", StatusMessageType.Info);
        };
    }

    private async void SaveWorkTime_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId == null)
        {
            SetStatus("Bitte ein konkretes Projekt des Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (WorkDatePicker.SelectedDate == null)
        {
            SetStatus("Bitte ein Datum auswaehlen.", StatusMessageType.Error);
            return;
        }

        if (!TimeSpan.TryParse(StartTimeText.Text, out var startTime) || !TimeSpan.TryParse(EndTimeText.Text, out var endTime))
        {
            SetStatus("Bitte Beginn und Ende im Format HH:mm eingeben.", StatusMessageType.Error);
            return;
        }

        if (!int.TryParse(BreakMinutesText.Text, out var breakMinutes) || breakMinutes < 0)
        {
            SetStatus("Bitte eine gueltige Pausenzeit in Minuten eingeben.", StatusMessageType.Error);
            return;
        }

        if (!TryParseDecimal(HourlyRateText.Text, out var hourlyRate) || hourlyRate <= 0m)
        {
            SetStatus("Bitte einen gueltigen Stundensatz eingeben.", StatusMessageType.Error);
            return;
        }

        if (!TryParseDecimal(TravelKilometersText.Text, out var travelKilometers) || travelKilometers < 0m)
        {
            SetStatus("Bitte gueltige Anfahrtskilometer eingeben.", StatusMessageType.Error);
            return;
        }

        if (!TryParseDecimal(TravelRateText.Text, out var travelRatePerKilometer) || travelRatePerKilometer < 0m)
        {
            SetStatus("Bitte einen gueltigen Kilometerpreis eingeben.", StatusMessageType.Error);
            return;
        }

        var duration = endTime - startTime - TimeSpan.FromMinutes(breakMinutes);
        if (duration <= TimeSpan.Zero)
        {
            SetStatus("Die berechnete Arbeitszeit muss groesser als 0 sein.", StatusMessageType.Error);
            return;
        }

        try
        {
            await App.Api.SaveWorkTimeAsync(new WorkTimeEntryEntity
            {
                CustomerId = customer.CustomerId,
                ProjectId = projectSelection.ProjectId.Value,
                WorkDate = WorkDatePicker.SelectedDate.Value,
                StartTime = startTime,
                EndTime = endTime,
                BreakMinutes = breakMinutes,
                HourlyRate = hourlyRate,
                TravelKilometers = travelKilometers,
                TravelRatePerKilometer = travelRatePerKilometer,
                Description = string.IsNullOrWhiteSpace(DescriptionText.Text) ? "Arbeitszeit" : DescriptionText.Text.Trim(),
                Comment = (CommentText.Text ?? "").Trim()
            });

            CommentText.Clear();
            await LoadEntriesAsync();
            var hoursWorked = Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
            var travelTotal = travelKilometers * travelRatePerKilometer;
            SetStatus($"Arbeitszeit gespeichert: {hoursWorked:0.##} h und {travelKilometers:0.##} km fuer {customer.Name} / {projectSelection.Name}. Gesamt Anfahrt: {travelTotal:0.00} EUR.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Speichern fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
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

        await LoadEntriesAsync();
        SetStatus($"{selectedEntries.Count} Zeiteintraege wurden geloescht.", StatusMessageType.Success);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCustomersAsync(CustomerCombo.SelectedItem is CustomerEntity customer ? customer.CustomerId : null,
            ProjectCombo.SelectedItem is ProjectSelectionItem project ? project.ProjectId : null);
        await LoadEntriesAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
        await LoadEntriesAsync();
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadEntriesAsync();
    }

    private async Task LoadCustomersAsync(int? selectedCustomerId = null, int? selectedProjectId = null)
    {
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            return;
        }

        var customer = selectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value) ?? customers[0] : customers[0];
        CustomerCombo.SelectedItem = customer;
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

        var items = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: true);
        ProjectCombo.ItemsSource = items;
        if (items.Count == 1)
        {
            ProjectCombo.SelectedItem = items[0];
            SetStatus("Dieser Kunde hat noch keine Projekte. Bitte lege zuerst unter 'Kunden und Export' ein Projekt an.", StatusMessageType.Warning);
            return;
        }

        ProjectCombo.SelectedItem = selectedProjectId.HasValue ? items.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) ?? items[0] : items[0];
    }

    private async Task LoadEntriesAsync()
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            WorkEntriesGrid.ItemsSource = null;
            SetStatus("Kein Kunde ausgewaehlt.", StatusMessageType.Info);
            return;
        }

        var selectedProjectId = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var entries = await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId);
        entries = entries.OrderByDescending(w => w.WorkDate).ThenByDescending(w => w.StartTime).ToList();
        WorkEntriesGrid.ItemsSource = entries;
        SetStatus($"{entries.Count} Zeiteintraege geladen.", StatusMessageType.Info);
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

    private Brush GetBrush(StatusMessageType type, string part)
    {
        var key = $"Status{type}{part}Brush";
        return (Brush)FindResource(key);
    }
}
