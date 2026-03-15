using InvoiceWizard.Data.Entities;
using System.Globalization;
using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class CalendarEntryDialog : Window
{
    private readonly DateTime _initialDate;

    public CalendarEntryDialog(DateTime date, IReadOnlyList<CustomerEntity> customers, CalendarEntryEntity? existingEntry = null)
    {
        InitializeComponent();
        _initialDate = date.Date;
        DialogTitleText.Text = existingEntry is null ? "Termin erstellen" : "Termin bearbeiten";
        CustomerCombo.ItemsSource = customers.OrderBy(x => x.Name).ToList();
        EntryDatePicker.SelectedDate = existingEntry?.EntryDate ?? date.Date;
        EntryDatePicker.IsEnabled = existingEntry is not null;
        TitleText.Text = existingEntry?.Title ?? string.Empty;
        LocationText.Text = existingEntry?.Location ?? string.Empty;
        StartTimeText.Text = (existingEntry?.StartTime ?? TimeSpan.FromHours(8)).ToString("hh\\:mm");
        EndTimeText.Text = (existingEntry?.EndTime ?? TimeSpan.FromHours(9)).ToString("hh\\:mm");
        DescriptionText.Text = existingEntry?.Description ?? string.Empty;
        CustomerCombo.SelectedValue = existingEntry?.CustomerId;
        ExistingEntryId = existingEntry?.CalendarEntryId ?? 0;
    }

    public int ExistingEntryId { get; }
    public CalendarEntryEntity? Result { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var title = (TitleText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Bitte einen Titel eingeben.", "Termin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (EntryDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Bitte ein Datum auswaehlen.", "Termin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact((StartTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime))
        {
            MessageBox.Show("Bitte eine gueltige Startzeit im Format HH:mm eingeben.", "Termin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact((EndTimeText.Text ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            MessageBox.Show("Bitte eine gueltige Endzeit im Format HH:mm eingeben.", "Termin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (endTime <= startTime)
        {
            MessageBox.Show("Die Endzeit muss nach der Startzeit liegen.", "Termin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new CalendarEntryEntity
        {
            CalendarEntryId = ExistingEntryId,
            CustomerId = CustomerCombo.SelectedValue is int customerId ? customerId : null,
            EntryDate = ExistingEntryId > 0 ? EntryDatePicker.SelectedDate.Value.Date : _initialDate,
            StartTime = startTime,
            EndTime = endTime,
            Title = title,
            Location = (LocationText.Text ?? string.Empty).Trim(),
            Description = (DescriptionText.Text ?? string.Empty).Trim()
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
