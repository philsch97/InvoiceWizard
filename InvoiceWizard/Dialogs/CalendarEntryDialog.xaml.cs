using InvoiceWizard.Data.Entities;
using System.Globalization;
using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class CalendarEntryDialog : Window
{
    public CalendarEntryDialog(DateTime date, CalendarEntryEntity? existingEntry = null)
    {
        InitializeComponent();
        DialogTitleText.Text = existingEntry is null ? "Termin erstellen" : "Termin bearbeiten";
        EntryDatePicker.SelectedDate = existingEntry?.EntryDate ?? date.Date;
        TitleText.Text = existingEntry?.Title ?? string.Empty;
        LocationText.Text = existingEntry?.Location ?? string.Empty;
        StartTimeText.Text = (existingEntry?.StartTime ?? TimeSpan.FromHours(8)).ToString("hh\\:mm");
        EndTimeText.Text = (existingEntry?.EndTime ?? TimeSpan.FromHours(9)).ToString("hh\\:mm");
        DescriptionText.Text = existingEntry?.Description ?? string.Empty;
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
            EntryDate = EntryDatePicker.SelectedDate.Value.Date,
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
