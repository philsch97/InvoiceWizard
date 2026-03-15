using System.Globalization;
using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class WorkTimeStopDialog : Window
{
    public WorkTimeStopDialog(decimal travelKilometers)
    {
        InitializeComponent();
        TravelKilometersText.Text = travelKilometers.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
    }

    public decimal TravelKilometers { get; private set; }
    public string Comment { get; private set; } = "";

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TravelKilometersText.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var travelKilometers)
            && !decimal.TryParse(TravelKilometersText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out travelKilometers))
        {
            MessageBox.Show("Bitte gueltige Anfahrtskilometer eingeben.", "Projektstopp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (travelKilometers < 0m)
        {
            MessageBox.Show("Anfahrtskilometer duerfen nicht negativ sein.", "Projektstopp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var comment = (CommentText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(comment))
        {
            MessageBox.Show("Bitte einen Kommentar eingeben.", "Projektstopp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TravelKilometers = travelKilometers;
        Comment = comment;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
