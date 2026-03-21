using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        InfoText.Text = requireVatPerLine
            ? "Ohne Rechnungsbeleg muss jede Position einen MwSt.-Satz groesser als 0 haben. Aenderungen werden erst nach dem Speichern in den Rechnungsimport uebernommen."
            : "Pro Position muss entweder ein Netto-Preis oder ein Brutto-Preis angegeben sein. Wenn nur der Brutto-Preis eingetragen wird, wird der Netto-Preis ueber die MwSt. berechnet.";

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
        FinishGridEdit();

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
        FinishGridEdit();

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

        var view = CollectionViewSource.GetDefaultView(LinesGrid.ItemsSource);
        var editableView = view as IEditableCollectionView;
        if (view is not null && (editableView is null || (!editableView.IsAddingNew && !editableView.IsEditingItem)))
        {
            view.Refresh();
        }
    }

    private void FinishGridEdit()
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private bool TryReadLine(out ManualInvoiceLineInput line, out string error)
    {
        line = new ManualInvoiceLineInput();
        error = string.Empty;

        var description = (DescriptionText.Text ?? string.Empty).Trim();
        var unit = (UnitText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            error = "Bitte die Beschreibung als Pflichtfeld ausfuellen.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            error = "Bitte die Einheit als Pflichtfeld ausfuellen.";
            return false;
        }

        if ((!decimal.TryParse(QuantityText.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var quantity)
            && !decimal.TryParse(QuantityText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity)) || quantity <= 0m)
        {
            error = "Bitte eine gueltige Menge eingeben.";
            return false;
        }

        if (!TryParseOptionalDecimal(NetPriceText.Text, out var netPrice))
        {
            error = "Bitte einen gueltigen Netto-Preis eingeben oder das Feld leer lassen.";
            return false;
        }

        if (netPrice < 0m)
        {
            error = "Der Netto-Preis darf nicht negativ sein.";
            return false;
        }

        if (!TryParseDecimal(MetalSurchargeText.Text, out var metalSurcharge) || metalSurcharge < 0m)
        {
            error = "Bitte einen gueltigen Materialzuschlag eingeben.";
            return false;
        }

        if (!TryParseDecimal(PriceBasisText.Text, out var priceBasis) || priceBasis <= 0m)
        {
            error = "Bitte eine gueltige Preisbasis eingeben.";
            return false;
        }

        if (!TryParseOptionalDecimal(GrossPriceText.Text, out var grossPrice))
        {
            error = "Bitte einen gueltigen Brutto-Preis eingeben oder das Feld leer lassen.";
            return false;
        }

        if (grossPrice < 0m)
        {
            error = "Der Brutto-Preis darf nicht negativ sein.";
            return false;
        }

        var vatPercent = 0m;
        if (!string.IsNullOrWhiteSpace(VatPercentText.Text) && !TryParseDecimal(VatPercentText.Text, out vatPercent))
        {
            error = "Bitte einen gueltigen MwSt.-Satz eingeben oder das Feld leer lassen.";
            return false;
        }

        if (_requireVatPerLine && vatPercent <= 0m)
        {
            error = "Ohne Rechnungsbeleg bitte pro Position einen MwSt.-Satz groesser als 0 angeben.";
            return false;
        }

        if (netPrice <= 0m && grossPrice <= 0m)
        {
            error = "Bitte pro Position entweder einen Netto-Preis oder einen Brutto-Preis eingeben.";
            return false;
        }

        if (netPrice <= 0m && grossPrice > 0m && vatPercent <= 0m)
        {
            error = "Wenn nur ein Brutto-Preis eingetragen wird, bitte auch einen MwSt.-Satz groesser als 0 angeben.";
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
        NetPriceText.Clear();
        MetalSurchargeText.Text = "0";
        GrossPriceText.Clear();
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
            return $"Position {position}: Die Menge muss groesser als 0 sein.";
        }

        if (line.NetUnitPrice < 0m)
        {
            return $"Position {position}: Der Netto-Preis darf nicht negativ sein.";
        }

        if (line.GrossListPrice < 0m)
        {
            return $"Position {position}: Der Brutto-Preis darf nicht negativ sein.";
        }

        if (line.MetalSurcharge < 0m)
        {
            return $"Position {position}: Der Materialzuschlag darf nicht negativ sein.";
        }

        if (line.PriceBasisQuantity <= 0m)
        {
            return $"Position {position}: Die Preisbasis muss groesser als 0 sein.";
        }

        if (line.NetUnitPrice <= 0m && line.GrossListPrice <= 0m)
        {
            return $"Position {position}: Bitte entweder einen Netto-Preis oder einen Brutto-Preis eingeben.";
        }

        if (requireVatPerLine && line.VatPercent <= 0m)
        {
            return $"Position {position}: Bitte einen MwSt.-Satz groesser als 0 eingeben.";
        }

        if (line.NetUnitPrice <= 0m && line.GrossListPrice > 0m && line.VatPercent <= 0m)
        {
            return $"Position {position}: Fuer einen reinen Brutto-Preis wird ein MwSt.-Satz benoetigt.";
        }

        return null;
    }

    private static void InitializeManualLineAmounts(ManualInvoiceLineInput line)
    {
        var normalizedSurcharge = PricingHelper.NormalizeUnitPrice(line.MetalSurcharge, line.PriceBasisQuantity);
        if (line.NetUnitPrice <= 0m && line.GrossListPrice > 0m && line.VatPercent > 0m)
        {
            var normalizedGrossPrice = PricingHelper.NormalizeUnitPrice(line.GrossListPrice, line.PriceBasisQuantity);
            var normalizedNetTotal = normalizedGrossPrice / (1m + (line.VatPercent / 100m));
            var normalizedNetBase = Math.Max(0m, normalizedNetTotal - normalizedSurcharge);
            line.NetUnitPrice = PricingHelper.RoundCurrency(normalizedNetBase * (line.PriceBasisQuantity <= 0m ? 1m : line.PriceBasisQuantity));
        }

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

    private static bool TryParseOptionalDecimal(string? input, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = 0m;
            return true;
        }

        return TryParseDecimal(input, out value);
    }
}
