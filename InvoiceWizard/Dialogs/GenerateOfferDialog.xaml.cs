using System.Globalization;
using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class GenerateOfferDialog : Window
{
    public GenerateOfferDialog(string offerNumber, string customerNumber, string customerName, decimal markupPercent)
    {
        InitializeComponent();
        OfferNumberText.Text = offerNumber;
        CustomerNumberText.Text = customerNumber;
        CustomerNameText.Text = customerName;
        OfferDatePicker.SelectedDate = DateTime.Today;
        ValidUntilDatePicker.SelectedDate = DateTime.Today.AddDays(14);
        MarkupPercentText.Text = markupPercent.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        SubjectText.Text = "Gerne bieten wir Ihnen folgende Lieferungen/Leistungen an.";
        WithVatRadio.IsChecked = true;
    }

    public GeneratedOfferOptions? Result { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OfferNumberText.Text))
        {
            MessageBox.Show("Bitte eine Angebotsnummer eingeben.", "Angebot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (OfferDatePicker.SelectedDate == null || ValidUntilDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Bitte Angebotsdatum und Gültigkeit angeben.", "Angebot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(MarkupPercentText.Text, out var markupPercent) || markupPercent < 0m)
        {
            MessageBox.Show("Bitte einen gültigen Materialaufschlag größer oder gleich 0 eingeben.", "Angebot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new GeneratedOfferOptions
        {
            OfferNumber = OfferNumberText.Text.Trim(),
            OfferDate = OfferDatePicker.SelectedDate.Value.Date,
            ValidUntilDate = ValidUntilDatePicker.SelectedDate.Value.Date,
            Subject = (SubjectText.Text ?? string.Empty).Trim(),
            MarkupPercent = markupPercent,
            ApplySmallBusinessRegulation = WithoutVatRadio.IsChecked == true
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}

public sealed class GeneratedOfferOptions
{
    public string OfferNumber { get; set; } = "";
    public DateTime OfferDate { get; set; }
    public DateTime ValidUntilDate { get; set; }
    public string Subject { get; set; } = "";
    public decimal MarkupPercent { get; set; }
    public bool ApplySmallBusinessRegulation { get; set; }
}
