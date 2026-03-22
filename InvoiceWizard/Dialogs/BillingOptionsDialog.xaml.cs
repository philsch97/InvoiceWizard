using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard.Dialogs;

public partial class BillingOptionsDialog : Window
{
    public BillingOptionsDialog(decimal initialMarkupPercent, string initialSmallMaterialMode, decimal initialSmallMaterialFlatFee, bool showSmallMaterialMode)
    {
        InitializeComponent();
        MarkupPercentText.Text = initialMarkupPercent.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        SmallMaterialFlatFeeText.Text = initialSmallMaterialFlatFee.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        SmallMaterialModePanel.Visibility = showSmallMaterialMode ? Visibility.Visible : Visibility.Collapsed;
        DialogHintText.Text = showSmallMaterialMode
            ? "Bitte Zuschlag und Kleinmaterial-Einstellungen fuer diesen Vorgang festlegen. Die Kleinmaterial-Pauschale wird netto erfasst."
            : "Bitte Zuschlag und optional eine pauschale Kleinmaterial-Position fuer diesen Vorgang festlegen. Die Kleinmaterial-Pauschale wird netto erfasst.";

        foreach (var item in SmallMaterialModeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), initialSmallMaterialMode, StringComparison.Ordinal))
            {
                SmallMaterialModeCombo.SelectedItem = item;
                break;
            }
        }
    }

    public BillingOptionsResult? Result { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDecimal(MarkupPercentText.Text, out var markupPercent) || markupPercent < 0m)
        {
            MessageBox.Show("Bitte einen gueltigen Zuschlag groesser oder gleich 0 eingeben.", "Abrechnungsoptionen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(SmallMaterialFlatFeeText.Text, out var flatFee) || flatFee < 0m)
        {
            MessageBox.Show("Bitte eine gueltige Kleinmaterial-Pauschale netto groesser oder gleich 0 eingeben.", "Abrechnungsoptionen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new BillingOptionsResult
        {
            MarkupPercent = markupPercent,
            SmallMaterialMode = SmallMaterialModePanel.Visibility == Visibility.Visible
                ? (SmallMaterialModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Als Sammelposition"
                : "Nicht berechnen",
            SmallMaterialFlatFee = flatFee
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

public sealed class BillingOptionsResult
{
    public decimal MarkupPercent { get; set; }
    public string SmallMaterialMode { get; set; } = "Als Sammelposition";
    public decimal SmallMaterialFlatFee { get; set; }
}
