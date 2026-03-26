using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard.Dialogs;

public partial class WorkTimeEditDialog : Window
{
    private readonly List<CustomerEntity> _customers;
    private readonly Dictionary<int, List<ProjectSelectionItem>> _projectsByCustomer;
    private readonly bool _isCreateMode;

    public WorkTimeEditDialog(
        WorkTimeEntryEntity entry,
        List<CustomerEntity> customers,
        Dictionary<int, List<ProjectSelectionItem>> projectsByCustomer,
        bool isCreateMode = false)
    {
        InitializeComponent();
        Entry = entry;
        _customers = customers;
        _projectsByCustomer = projectsByCustomer;
        _isCreateMode = isCreateMode;

        CustomerCombo.ItemsSource = _customers;
        CustomerCombo.SelectedItem = _customers.FirstOrDefault(x => x.CustomerId == entry.CustomerId);
        WorkDatePicker.SelectedDate = entry.WorkDate.Date;
        DescriptionText.Text = entry.Description;
        StartTimeText.Text = entry.StartTime.ToString("hh\\:mm");
        EndTimeText.Text = entry.EndTime.ToString("hh\\:mm");
        BreakMinutesText.Text = entry.BreakMinutes.ToString(CultureInfo.InvariantCulture);
        HourlyRateText.Text = entry.HourlyRate.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        TravelKilometersText.Text = entry.TravelKilometers.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        TravelRateText.Text = entry.TravelRatePerKilometer.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        CommentText.Text = entry.Comment;

        LoadProjects(entry.ProjectId);
    }

    public WorkTimeEntryEntity Entry { get; }
    public WorkTimeEntryEntity? Result { get; private set; }
    public string WindowTitle => _isCreateMode ? "Arbeitszeit nachtragen" : "Arbeitszeit bearbeiten";
    public string DialogHeadline => _isCreateMode ? "Arbeitszeit nachtraeglich erfassen" : "Arbeitszeit bearbeiten";
    public string DialogDescription => _isCreateMode
        ? "Hier kannst du Arbeitszeit rueckwirkend manuell eintragen."
        : "Admins koennen abgeschlossene Arbeitszeiten hier nachtraeglich korrigieren.";
    public string ConfirmButtonText => _isCreateMode ? "Arbeitszeit speichern" : "Aenderungen speichern";

    private void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadProjects(null);
    }

    private void LoadProjects(int? selectedProjectId)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            ProjectCombo.ItemsSource = null;
            return;
        }

        _projectsByCustomer.TryGetValue(customer.CustomerId, out var items);
        items ??= [];
        ProjectCombo.ItemsSource = items;
        ProjectCombo.SelectedItem = selectedProjectId.HasValue
            ? items.FirstOrDefault(x => x.ProjectId == selectedProjectId.Value)
            : items.FirstOrDefault();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            MessageBox.Show("Bitte einen Kunden waehlen.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (WorkDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Bitte ein Datum waehlen.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact((StartTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime)
            || !TimeSpan.TryParseExact((EndTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            MessageBox.Show("Bitte Beginn und Ende im Format HH:mm eingeben.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(BreakMinutesText.Text, out var breakMinutes) || breakMinutes < 0)
        {
            MessageBox.Show("Bitte eine gueltige Pausenzeit eingeben.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(HourlyRateText.Text, out var hourlyRate) || hourlyRate <= 0m
            || !TryParseDecimal(TravelKilometersText.Text, out var travelKilometers) || travelKilometers < 0m
            || !TryParseDecimal(TravelRateText.Text, out var travelRate) || travelRate < 0m)
        {
            MessageBox.Show("Bitte die Preis- und Kilometerfelder gueltig ausfuellen.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var duration = endTime - startTime - TimeSpan.FromMinutes(breakMinutes);
        if (duration <= TimeSpan.Zero)
        {
            MessageBox.Show("Die berechnete Arbeitszeit muss groesser als 0 sein.", "Arbeitszeit bearbeiten", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new WorkTimeEntryEntity
        {
            WorkTimeEntryId = Entry.WorkTimeEntryId,
            CustomerId = customer.CustomerId,
            ProjectId = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId,
            WorkDate = WorkDatePicker.SelectedDate.Value.Date,
            StartTime = startTime,
            EndTime = endTime,
            BreakMinutes = breakMinutes,
            HourlyRate = hourlyRate,
            TravelKilometers = travelKilometers,
            TravelRatePerKilometer = travelRate,
            Description = string.IsNullOrWhiteSpace(DescriptionText.Text) ? "Arbeitszeit" : DescriptionText.Text.Trim(),
            Comment = (CommentText.Text ?? string.Empty).Trim()
        };

        DialogResult = true;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
