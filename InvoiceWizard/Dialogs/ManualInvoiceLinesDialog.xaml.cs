using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;

namespace InvoiceWizard.Dialogs;

public partial class ManualInvoiceLinesDialog : Window
{
    private readonly ObservableCollection<ManualInvoiceLineInput> _lines;
    private readonly bool _requireVatPerLine;

    public ManualInvoiceLinesDialog(IEnumerable<ManualInvoiceLineInput> initialLines, bool requireVatPerLine)
    {
        InitializeComponent();
        _requireVatPerLine = requireVatPerLine;
        _lines = new ObservableCollection<ManualInvoiceLineInput>(initialLines.Select(CloneLine));
        LinesGrid.ItemsSource = _lines;
        LinesGrid.CellEditEnding += LinesGrid_CellEditEnding;
        VatPercentLabel.Text = requireVatPerLine ? "MwSt. in % * Pflicht" : "MwSt. in % optional";
        VatPercentLabel.Style = (Style)FindResource(requireVatPerLine ? "RequiredLabelStyle" : "MutedLabelStyle");
        if (requireVatPerLine)
        {
            InfoText.Text = "Ohne Rechnungsbeleg muss jede Position einen MwSt.-Satz größer als 0 haben. Änderungen werden erst nach dem Speichern in den Rechnungsimport übernommen.";
        }

        RefreshLines();
    }

    public List<ManualInvoiceLineInput> ResultLines { get; private set; } = new();

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadLine(out var line, out var error))
        {
            MessageBox.Show(error, "Positionen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        line.Position = _lines.Count + 1;
        _lines.Add(line);
        ClearInputFields();
        RefreshLines();
    }

    private void RemoveSelectedLines_Click(object sender, RoutedEventArgs e)
    {
        var selected = LinesGrid.SelectedItems.OfType<ManualInvoiceLineInput>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Bitte zuerst mindestens eine Position markieren.", "Positionen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var line in selected)
        {
            _lines.Remove(line);
        }

        RefreshLines();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var normalized = new List<ManualInvoiceLineInput>();
        for (var i = 0; i < _lines.Count; i++)
        {
            var cloned = CloneLine(_lines[i]);
            cloned.Position = i + 1;
            InitializeManualLineAmounts(cloned);
            var validationError = ValidateLine(cloned, i + 1, _requireVatPerLine);
            if (validationError is not null)
            {
                MessageBox.Show(validationError, "Positionen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            normalized.Add(cloned);
        }

        ResultLines = normalized;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void LinesGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(RefreshLines);
    }

    private void RefreshLines()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].Position = i + 1;
            InitializeManualLineAmounts(_lines[i]);
        }

        LinesGrid.Items.Refresh();
    }

    private bool TryReadLine(out ManualInvoiceLineInput line, out string error)
    {
        line = new ManualInvoiceLineInput();
        error = string.Empty;

        var description = (DescriptionText.Text ?? string.Empty).Trim();
        var unit = (UnitText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            error = "Bitte die Beschreibung als Pflichtfeld ausfüllen.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            error = "Bitte die Einheit als Pflichtfeld ausfüllen.";
            return false;
        }

        if (!decimal.TryParse(QuantityText.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var quantity)
            && !decimal.TryParse(QuantityText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) || quantity <= 0m)
        {
            error = "Bitte eine gültige Menge eingeben.";
            return false;
        }

        if (!TryParseDecimal(NetPriceText.Text, out var netPrice) || netPrice < 0m)
        {
            error = "Bitte einen gültigen Netto-Preis eingeben.";
            return false;
        }

        if (!TryParseDecimal(MetalSurchargeText.Text, out var metalSurcharge) || metalSurcharge < 0m)
        {
            error = "Bitte einen gültigen Materialzuschlag eingeben.";
            return false;
        }

        if (!TryParseDecimal(PriceBasisText.Text, out var priceBasis) || priceBasis <= 0m)
        {
            error = "Bitte eine gültige Preisbasis eingeben.";
            return false;
        }

        var grossPrice = 0m;
        if (!string.IsNullOrWhiteSpace(GrossPriceText.Text) && !TryParseDecimal(GrossPriceText.Text, out grossPrice))
        {
            error = "Bitte einen gültigen Brutto-Listenpreis eingeben oder das Feld leer lassen.";
            return false;
        }

        var vatPercent = 0m;
        if (!string.IsNullOrWhiteSpace(VatPercentText.Text) && !TryParseDecimal(VatPercentText.Text, out vatPercent))
        {
            error = "Bitte einen gültigen MwSt.-Satz eingeben oder das Feld leer lassen.";
            return false;
        }

        if (_requireVatPerLine && vatPercent <= 0m)
        {
            error = "Ohne Rechnungsbeleg bitte pro Position einen MwSt.-Satz größer als 0 angeben.";
            return false;
        }

        line = new ManualInvoiceLineInput
        {
            ArticleNumber = (ArticleNumberText.Text ?? string.Empty).Trim(),
            Ean = (EanText.Text ?? string.Empty).Trim(),
            Description = description,
            Quantity = quantity,
            Unit = unit,
            NetUnitPrice = netPrice,
            MetalSurcharge = metalSurcharge,
            VatPercent = vatPercent,
            GrossListPrice = grossPrice,
            PriceBasisQuantity = priceBasis
        };
        InitializeManualLineAmounts(line);
        return true;
    }

    private void ClearInputFields()
    {
        ArticleNumberText.Clear();
        EanText.Clear();
        DescriptionText.Clear();
        QuantityText.Text = "1";
        UnitText.Text = "ST";
        NetPriceText.Text = "0";
        MetalSurchargeText.Text = "0";
        GrossPriceText.Text = "0";
        VatPercentText.Text = "19";
        PriceBasisText.Text = "1";
    }

    private static string? ValidateLine(ManualInvoiceLineInput line, int position, bool requireVatPerLine)
    {
        if (string.IsNullOrWhiteSpace(line.Description))
        {
            return $"Position {position}: Bitte eine Beschreibung eingeben.";
        }

        if (string.IsNullOrWhiteSpace(line.Unit))
        {
            return $"Position {position}: Bitte eine Einheit eingeben.";
        }

        if (line.Quantity <= 0m)
        {
            return $"Position {position}: Die Menge muss größer als 0 sein.";
        }

        if (line.NetUnitPrice < 0m)
        {
            return $"Position {position}: Der Netto-Preis darf nicht negativ sein.";
        }

        if (line.MetalSurcharge < 0m)
        {
            return $"Position {position}: Der Materialzuschlag darf nicht negativ sein.";
        }

        if (line.PriceBasisQuantity <= 0m)
        {
            return $"Position {position}: Die Preisbasis muss größer als 0 sein.";
        }

        if (requireVatPerLine && line.VatPercent <= 0m)
        {
            return $"Position {position}: Bitte einen MwSt.-Satz größer als 0 eingeben.";
        }

        return null;
    }

    private static void InitializeManualLineAmounts(ManualInvoiceLineInput line)
    {
        var normalizedNetUnitPrice = PricingHelper.NormalizeUnitPrice(line.NetUnitPrice, line.MetalSurcharge, line.PriceBasisQuantity);
        var normalizedGrossListPrice = line.GrossListPrice > 0m
            ? PricingHelper.NormalizeUnitPrice(line.GrossListPrice, line.PriceBasisQuantity)
            : 0m;
        var grossFromVat = line.VatPercent > 0m
            ? PricingHelper.RoundUnitPrice(normalizedNetUnitPrice * (1m + (line.VatPercent / 100m)))
            : 0m;

        line.GrossUnitPrice = normalizedGrossListPrice > 0m
            ? PricingHelper.RoundUnitPrice(normalizedGrossListPrice)
            : grossFromVat > 0m
                ? grossFromVat
                : PricingHelper.RoundUnitPrice(normalizedNetUnitPrice);
        line.GrossLineTotal = PricingHelper.RoundCurrency(line.Quantity * line.GrossUnitPrice);
    }

    private static ManualInvoiceLineInput CloneLine(ManualInvoiceLineInput line)
    {
        return new ManualInvoiceLineInput
        {
            Position = line.Position,
            ArticleNumber = line.ArticleNumber,
            Ean = line.Ean,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            NetUnitPrice = line.NetUnitPrice,
            MetalSurcharge = line.MetalSurcharge,
            VatPercent = line.VatPercent,
            GrossListPrice = line.GrossListPrice,
            GrossUnitPrice = line.GrossUnitPrice,
            PriceBasisQuantity = line.PriceBasisQuantity,
            GrossLineTotal = line.GrossLineTotal
        };
    }

    private static bool TryParseDecimal(string? input, out decimal value)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out value)
               || decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
