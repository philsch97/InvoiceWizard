using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace InvoiceWizard;

public partial class Datenimport : Page
{
    private static readonly XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    private static readonly XNamespace udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
    private static readonly Regex MetalSurchargeRegex = new(@"(?i)\b(metall(?:zuschlag)?|cu(?:-?zuschlag)?|kupfer(?:zuschlag)?)\b", RegexOptions.Compiled);
    private static readonly Regex CableRegex = new(@"(?i)\b(kabel|leitung|nym|nyy|h07|nhx|erdkabel|mantelleitung)\b", RegexOptions.Compiled);

    private readonly ObservableCollection<ManualInvoiceLineInput> _manualLines = new();
    private readonly ObservableCollection<InvoiceEntity> _storedInvoices = new();
    private readonly List<KeyValuePair<string, string>> _invoiceDirections =
    [
        new("Ausgaberechnung", "Expense"),
        new("Einnahmerechnung", "Revenue")
    ];
    private readonly List<KeyValuePair<string, string>> _accountingCategories =
    [
        new("Material und Waren", "MaterialAndGoods"),
        new("Werkzeug", "Tools"),
        new("Dienstleistungen", "Services"),
        new("Buero", "Office"),
        new("Fahrzeug", "Vehicle"),
        new("Sonstiges", "Other")
    ];
    private string _currentSourcePdfPath = "";
    private string _currentContentHash = "";
    private byte[]? _currentPdfBytes;
    private string _currentOriginalPdfFileName = "";

    public Datenimport()
    {
        InitializeComponent();
        ManualLinesGrid.ItemsSource = _manualLines;
        InvoicesGrid.ItemsSource = _storedInvoices;
        InvoiceDirectionCombo.ItemsSource = _invoiceDirections;
        InvoiceDirectionCombo.DisplayMemberPath = "Key";
        InvoiceDirectionCombo.SelectedValuePath = "Value";
        InvoiceDirectionCombo.SelectedValue = "Expense";
        AccountingCategoryCombo.ItemsSource = _accountingCategories;
        AccountingCategoryCombo.DisplayMemberPath = "Key";
        AccountingCategoryCombo.SelectedValuePath = "Value";
        AccountingCategoryCombo.SelectedValue = "MaterialAndGoods";
        InvoiceDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadStoredInvoicesAsync();
        UpdateInvoiceModeUi();
        SetStatus("Bereit fuer den Import.", StatusMessageType.Info);
    }

    private void ChoosePdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF Dateien (*.pdf)|*.pdf", Multiselect = false };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _currentPdfBytes = File.ReadAllBytes(dlg.FileName);
            _currentOriginalPdfFileName = Path.GetFileName(dlg.FileName);
            var xmlBytes = ZugferdExtractor.ExtractEmbeddedXml(dlg.FileName);
            if (xmlBytes == null)
            {
                _currentPdfBytes = null;
                _currentOriginalPdfFileName = "";
                _currentContentHash = "";
                _currentSourcePdfPath = "";
                SetStatus("Kein eingebettetes ZUGFeRD-XML gefunden.", StatusMessageType.Error);
                return;
            }

            var doc = XDocument.Load(new MemoryStream(xmlBytes));
            _currentContentHash = Sha256(_currentPdfBytes);
            InvoiceNumberText.Text = ExtractInvoiceNumberFromXml(doc);
            InvoiceDatePicker.SelectedDate = ExtractInvoiceDateFromXml(doc);
            SupplierNameText.Text = ExtractSupplierNameFromXml(doc);
            SourceInfoText.Text = dlg.FileName;
            _currentSourcePdfPath = dlg.FileName;
            NoSupplierInvoiceCheckBox.IsChecked = false;
            AccountingCategoryCombo.SelectedValue = "MaterialAndGoods";

            _manualLines.Clear();
            foreach (var line in ParseZugferdPositions(doc))
            {
                _manualLines.Add(new ManualInvoiceLineInput
                {
                    Position = _manualLines.Count + 1,
                    ArticleNumber = line.ArticleNumber,
                    Ean = line.Ean,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    NetUnitPrice = line.NetUnitPrice,
                    MetalSurcharge = line.MetalSurcharge,
                    GrossListPrice = line.GrossListPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity
                });
            }

            UpdateInvoiceModeUi();
            SetStatus($"{_manualLines.Count} Position(en) aus ZUGFeRD geladen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Import der PDF fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void ChooseClassicPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF Dateien (*.pdf)|*.pdf", Multiselect = false };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _currentPdfBytes = File.ReadAllBytes(dlg.FileName);
            _currentOriginalPdfFileName = Path.GetFileName(dlg.FileName);
            _currentContentHash = Sha256(_currentPdfBytes);
            _currentSourcePdfPath = dlg.FileName;
            SourceInfoText.Text = dlg.FileName;
            NoSupplierInvoiceCheckBox.IsChecked = false;

            var parsed = PdfInvoiceImportService.Parse(dlg.FileName);
            InvoiceNumberText.Text = string.IsNullOrWhiteSpace(parsed.InvoiceNumber) ? InvoiceNumberText.Text : parsed.InvoiceNumber;
            InvoiceDatePicker.SelectedDate = parsed.InvoiceDate ?? InvoiceDatePicker.SelectedDate ?? DateTime.Today;
            SupplierNameText.Text = string.IsNullOrWhiteSpace(parsed.SupplierName) ? SupplierNameText.Text : parsed.SupplierName;
            AccountingCategoryCombo.SelectedValue = DetectAccountingCategory(parsed.RawText);
            _manualLines.Clear();

            UpdateInvoiceModeUi();
            SetStatus("PDF geladen. Erkannte Daten wurden eingetragen und koennen jetzt noch bearbeitet werden. Positionen bitte bei Bedarf manuell ergaenzen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"PDF-Import fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void AddManualLine_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadLine(out var line, out var error))
        {
            SetStatus(error, StatusMessageType.Error);
            return;
        }

        line.Position = _manualLines.Count + 1;
        _manualLines.Add(line);
        ClearLineInputs();
        if (_currentPdfBytes is null)
        {
            _currentSourcePdfPath = "";
            _currentContentHash = "";
            SourceInfoText.Text = NoSupplierInvoiceCheckBox.IsChecked == true ? "Manuelle Erfassung ohne Rechnung" : "Manuelle Erfassung";
        }
        SetStatus("Position erfolgreich hinzugefuegt.", StatusMessageType.Success);
    }

    private void RemoveSelectedLine_Click(object sender, RoutedEventArgs e)
    {
        if (ManualLinesGrid.SelectedItem is not ManualInvoiceLineInput selected)
        {
            SetStatus("Bitte zuerst eine Position markieren.", StatusMessageType.Warning);
            return;
        }

        _manualLines.Remove(selected);
        RenumberLines();
        SetStatus("Position entfernt.", StatusMessageType.Success);
    }

    private async void SaveInvoice_Click(object sender, RoutedEventArgs e)
    {
        var hasSupplierInvoice = NoSupplierInvoiceCheckBox.IsChecked != true;
        var invoiceDirection = InvoiceDirectionCombo.SelectedValue as string ?? "Expense";
        var invoiceNumber = (InvoiceNumberText.Text ?? "").Trim();
        var supplierName = (SupplierNameText.Text ?? "").Trim();

        if (hasSupplierInvoice && string.IsNullOrWhiteSpace(invoiceNumber))
        {
            SetStatus("Bitte die Rechnungsnummer als Pflichtfeld ausfuellen.", StatusMessageType.Error);
            return;
        }

        if (hasSupplierInvoice && _currentPdfBytes is null)
        {
            SetStatus("Bitte fuer Lieferantenrechnungen zuerst die PDF hochladen.", StatusMessageType.Error);
            return;
        }

        if (InvoiceDatePicker.SelectedDate == null)
        {
            SetStatus("Bitte ein Datum auswaehlen.", StatusMessageType.Error);
            return;
        }

        if (!TryParseOptionalDecimal(InvoiceTotalAmountText.Text, out var invoiceTotalAmount))
        {
            SetStatus("Bitte einen gueltigen Rechnungsbetrag eingeben oder das Feld leer lassen.", StatusMessageType.Error);
            return;
        }

        if (_manualLines.Count == 0 && invoiceTotalAmount <= 0m)
        {
            SetStatus("Bitte entweder Positionen erfassen oder einen Rechnungsbetrag ohne Positionen angeben.", StatusMessageType.Error);
            return;
        }

        var effectiveInvoiceNumber = hasSupplierInvoice ? invoiceNumber : BuildManualDocumentNumber();
        var invoiceDate = InvoiceDatePicker.SelectedDate.Value;
        var hash = string.IsNullOrWhiteSpace(_currentContentHash)
            ? BuildManualHash(invoiceDirection, effectiveInvoiceNumber, invoiceDate, supplierName, invoiceTotalAmount, hasSupplierInvoice, _manualLines)
            : _currentContentHash;

        try
        {
            await App.Api.SaveInvoiceAsync(
                invoiceDirection,
                effectiveInvoiceNumber,
                invoiceDate,
                supplierName,
                AccountingCategoryCombo.SelectedValue as string ?? "MaterialAndGoods",
                invoiceTotalAmount,
                _currentSourcePdfPath,
                _currentOriginalPdfFileName,
                _currentPdfBytes is null ? null : Convert.ToBase64String(_currentPdfBytes),
                hash,
                _manualLines,
                hasSupplierInvoice);
            var importedLineCount = _manualLines.Count;
            ResetForm();
            await LoadStoredInvoicesAsync();
            var summary = hasSupplierInvoice
                ? importedLineCount > 0
                    ? $"Rechnung {invoiceNumber} mit {importedLineCount} Position(en) erfolgreich importiert."
                    : $"Rechnung {invoiceNumber} wurde ohne Positionen mit Rechnungsbetrag gespeichert."
                : $"Manuell hinzugefuegte Positionen ohne Lieferantenrechnung wurden gespeichert.";
            SetStatus(summary, StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Speichern fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void NoSupplierInvoiceCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (NoSupplierInvoiceCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(_currentSourcePdfPath))
        {
            _currentSourcePdfPath = "";
            _currentContentHash = "";
            _currentPdfBytes = null;
            _currentOriginalPdfFileName = "";
            SourceInfoText.Text = "Manuelle Erfassung ohne Rechnung";
        }

        UpdateInvoiceModeUi();
    }

    private void InvoiceDirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateInvoiceModeUi();
    }

    private void UpdateInvoiceModeUi()
    {
        var noSupplierInvoice = NoSupplierInvoiceCheckBox.IsChecked == true;
        var invoiceDirection = InvoiceDirectionCombo.SelectedValue as string ?? "Expense";
        var isRevenue = string.Equals(invoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase);

        InvoiceNumberLabel.Text = noSupplierInvoice ? "Interne Referenz optional" : "Rechnungsnummer * Pflicht";
        InvoiceDateLabel.Text = noSupplierInvoice ? "Erfassungsdatum * Pflicht" : "Rechnungsdatum * Pflicht";
        PartyLabelText.Text = isRevenue ? "Kunde / Auftraggeber optional" : "Lieferant optional";
        NoSupplierInvoiceCheckBox.Content = isRevenue
            ? "Ohne Rechnungsbeleg erfassen"
            : "Ohne Rechnungsbeleg erfassen (Altbestand / manuell hinzugefuegt)";

        AccountingCategoryCombo.IsEnabled = true;
        InvoiceDirectionHintText.Text = isRevenue
            ? "Einnahmerechnungen werden fuer Zahlungseingang und Archiv getrennt von Material-Einkaeufen gespeichert."
            : "Ausgaberechnungen koennen je nach Kategorie in Material- und Kostenprozessen weiterverarbeitet werden.";

        ImportModeInfoText.Text = noSupplierInvoice
            ? "Fuer manuelle Erfassungen reicht ein Datum sowie entweder Positionen oder ein Rechnungsbetrag. Der EK bleibt fuer die Kalkulation sichtbar, auch wenn aktuell kein Rechnungsbeleg vorliegt."
            : "Rechnungsnummer, Rechnungsdatum, PDF und entweder Positionen oder ein Rechnungsbetrag sind erforderlich. Pro Position sind Beschreibung, Menge, Einheit, Netto-Preis und Preisbasis Pflicht.";

        if (string.IsNullOrWhiteSpace(_currentSourcePdfPath))
        {
            SourceInfoText.Text = noSupplierInvoice ? "Manuelle Erfassung ohne Rechnung" : "Manuelle Erfassung";
        }
    }

    private bool TryReadLine(out ManualInvoiceLineInput line, out string error)
    {
        line = new ManualInvoiceLineInput();
        error = "";
        var description = (DescriptionText.Text ?? "").Trim();
        var unit = (UnitText.Text ?? "").Trim();

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

        if (!TryParseDecimal(QuantityText.Text, out var quantity) || quantity <= 0m)
        {
            error = "Bitte eine gueltige Menge eingeben.";
            return false;
        }

        if (!TryParseDecimal(NetPriceText.Text, out var netPrice) || netPrice < 0m)
        {
            error = "Bitte einen gueltigen Netto-Preis eingeben.";
            return false;
        }

        var metalSurcharge = 0m;
        if (!string.IsNullOrWhiteSpace(MetalSurchargeText.Text) && !TryParseDecimal(MetalSurchargeText.Text, out metalSurcharge))
        {
            error = "Bitte einen gueltigen Metallzuschlag eingeben oder das Feld leer lassen.";
            return false;
        }

        if (metalSurcharge < 0m)
        {
            error = "Der Metallzuschlag darf nicht negativ sein.";
            return false;
        }

        var grossPrice = 0m;
        if (!string.IsNullOrWhiteSpace(GrossPriceText.Text) && !TryParseDecimal(GrossPriceText.Text, out grossPrice))
        {
            error = "Bitte einen gueltigen Brutto-Listenpreis eingeben oder das Feld leer lassen.";
            return false;
        }

        if (!TryParseDecimal(PriceBasisText.Text, out var priceBasis) || priceBasis <= 0m)
        {
            error = "Bitte eine gueltige Preisbasis eingeben.";
            return false;
        }

        line = new ManualInvoiceLineInput
        {
            ArticleNumber = (ArticleNumberText.Text ?? "").Trim(),
            Ean = (EanText.Text ?? "").Trim(),
            Description = description,
            Quantity = quantity,
            Unit = unit,
            NetUnitPrice = netPrice,
            MetalSurcharge = metalSurcharge,
            GrossListPrice = grossPrice,
            PriceBasisQuantity = priceBasis
        };

        return true;
    }

    private void ClearLineInputs()
    {
        ArticleNumberText.Clear();
        EanText.Clear();
        DescriptionText.Clear();
        QuantityText.Text = "1";
        UnitText.Text = "ST";
        NetPriceText.Text = "0";
        MetalSurchargeText.Text = "0";
        GrossPriceText.Text = "0";
        PriceBasisText.Text = "1";
    }

    private void ResetForm()
    {
        InvoiceNumberText.Clear();
        SupplierNameText.Clear();
        InvoiceDatePicker.SelectedDate = DateTime.Today;
        NoSupplierInvoiceCheckBox.IsChecked = false;
        InvoiceDirectionCombo.SelectedValue = "Expense";
        _manualLines.Clear();
        _currentSourcePdfPath = "";
        _currentContentHash = "";
        _currentPdfBytes = null;
        _currentOriginalPdfFileName = "";
        AccountingCategoryCombo.SelectedValue = "MaterialAndGoods";
        InvoiceTotalAmountText.Clear();
        ClearLineInputs();
        UpdateInvoiceModeUi();
    }

    private async Task LoadStoredInvoicesAsync()
    {
        _storedInvoices.Clear();
        foreach (var item in await App.Api.GetInvoicesAsync())
        {
            _storedInvoices.Add(item);
        }
    }

    private async void OpenStoredPdf_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.HasStoredPdf)
        {
            SetStatus("Bitte zuerst eine Rechnung mit gespeicherter PDF auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var bytes = await App.Api.DownloadInvoicePdfAsync(invoice.InvoiceId);
            var tempFile = Path.Combine(Path.GetTempPath(), string.IsNullOrWhiteSpace(invoice.OriginalPdfFileName) ? $"rechnung_{invoice.InvoiceId}.pdf" : invoice.OriginalPdfFileName);
            await File.WriteAllBytesAsync(tempFile, bytes);
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
            SetStatus($"PDF fuer {invoice.DisplayNumber} geoeffnet.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"PDF konnte nicht geoeffnet werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void UploadStoredPdf_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice)
        {
            SetStatus("Bitte zuerst eine Rechnung im Archiv auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var dlg = new OpenFileDialog { Filter = "PDF Dateien (*.pdf)|*.pdf", Multiselect = false };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var pdfBytes = await File.ReadAllBytesAsync(dlg.FileName);
            await App.Api.UploadInvoicePdfAsync(invoice.InvoiceId, Path.GetFileName(dlg.FileName), pdfBytes);
            await LoadStoredInvoicesAsync();
            SetStatus($"PDF fuer {invoice.DisplayNumber} wurde erfolgreich hinterlegt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"PDF konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void ExportStoredPdfs_Click(object sender, RoutedEventArgs e)
    {
        var invoices = InvoicesGrid.SelectedItems.OfType<InvoiceEntity>().Where(x => x.HasStoredPdf).ToList();
        if (invoices.Count == 0)
        {
            invoices = _storedInvoices.Where(x => x.HasStoredPdf).ToList();
        }

        if (invoices.Count == 0)
        {
            SetStatus("Es sind keine gespeicherten PDFs zum Export vorhanden.", StatusMessageType.Warning);
            return;
        }

        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var invoice in invoices)
        {
            var bytes = await App.Api.DownloadInvoicePdfAsync(invoice.InvoiceId);
            var fileName = string.IsNullOrWhiteSpace(invoice.OriginalPdfFileName)
                ? $"{invoice.InvoiceDate:yyyyMMdd}_{invoice.DisplayNumber}.pdf"
                : invoice.OriginalPdfFileName;
            var safeName = SanitizeFileName(fileName);
            await File.WriteAllBytesAsync(Path.Combine(dialog.FolderName, safeName), bytes);
        }

        SetStatus($"{invoices.Count} PDF(s) wurden in den ausgewaehlten Ordner exportiert.", StatusMessageType.Success);
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _manualLines.Count; i++)
        {
            _manualLines[i].Position = i + 1;
        }

        ManualLinesGrid.Items.Refresh();
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseOptionalDecimal(string? text, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0m;
            return true;
        }

        return TryParseDecimal(text, out value);
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static string BuildManualHash(string invoiceDirection, string invoiceNumber, DateTime invoiceDate, string supplierName, decimal invoiceTotalAmount, bool hasSupplierInvoice, IEnumerable<ManualInvoiceLineInput> lines)
    {
        var payload = string.Join("|", new[]
        {
            invoiceDirection,
            invoiceNumber,
            invoiceDate.ToString("yyyyMMdd"),
            supplierName,
            invoiceTotalAmount.ToString(CultureInfo.InvariantCulture),
            hasSupplierInvoice ? "WITH-INVOICE" : "WITHOUT-INVOICE",
            string.Join(";", lines.Select(l => $"{l.ArticleNumber}:{l.Description}:{l.Quantity}:{l.NetUnitPrice}:{l.MetalSurcharge}:{l.PriceBasisQuantity}"))
        });

        return Sha256(Encoding.UTF8.GetBytes(payload));
    }

    private static string BuildManualDocumentNumber() => $"MANUELL-{DateTime.Now:yyyyMMdd-HHmmss}";

    private static string DetectAccountingCategory(string rawText)
    {
        var text = rawText.ToLowerInvariant();
        if (text.Contains("werkzeug") || text.Contains("maschine"))
        {
            return "Tools";
        }

        if (text.Contains("dienstleistung") || text.Contains("service"))
        {
            return "Services";
        }

        if (text.Contains("buero"))
        {
            return "Office";
        }

        if (text.Contains("fahrzeug") || text.Contains("kraftstoff"))
        {
            return "Vehicle";
        }

        return "MaterialAndGoods";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "rechnung.pdf" : sanitized;
    }

    private static string ExtractInvoiceNumberFromXml(XDocument doc)
        => doc.Descendants(ram + "ID").FirstOrDefault()?.Value ?? "";

    private static DateTime ExtractInvoiceDateFromXml(XDocument doc)
    {
        var value = doc.Descendants(udt + "DateTimeString").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.Today;
        }

        return DateTime.TryParseExact(value.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateTime.Today;
    }

    private static string ExtractSupplierNameFromXml(XDocument doc)
        => doc.Descendants(ram + "SellerTradeParty").Descendants(ram + "Name").FirstOrDefault()?.Value ?? "Lieferant";

    private static string MapUnitCode(string unitCode)
    {
        return unitCode switch
        {
            "C62" => "ST",
            "H87" => "ST",
            "MTR" => "M",
            "KGM" => "KG",
            "LTR" => "L",
            "MTK" => "m2",
            "MTQ" => "m3",
            _ => unitCode
        };
    }

    private static List<InvoiceLine> ParseZugferdPositions(XDocument doc)
    {
        var rawLines = new List<InvoiceLine>();
        var items = doc.Descendants(ram + "IncludedSupplyChainTradeLineItem");
        var position = 1;
        foreach (var item in items)
        {
            var product = item.Descendants(ram + "SpecifiedTradeProduct").FirstOrDefault();
            var qtyElement = item.Descendants(ram + "BilledQuantity").FirstOrDefault();
            var basisQtyElement = item.Descendants(ram + "NetPriceProductTradePrice").Elements(ram + "BasisQuantity").FirstOrDefault();
            var quantity = ParseInvariant(qtyElement?.Value);
            var netPrice = ParseInvariant(item.Descendants(ram + "NetPriceProductTradePrice").Elements(ram + "ChargeAmount").FirstOrDefault()?.Value);
            var grossPrice = ParseInvariant(item.Descendants(ram + "GrossPriceProductTradePrice").Elements(ram + "ChargeAmount").FirstOrDefault()?.Value);
            var lineTotal = ParseInvariant(item.Descendants(ram + "LineTotalAmount").FirstOrDefault()?.Value);
            var basisQty = ParseInvariant(basisQtyElement?.Value);
            if (basisQty <= 0m) basisQty = 1m;
            var metalSurcharge = ExtractMetalSurchargeFromAllowanceCharges(item, quantity, basisQty);

            rawLines.Add(new InvoiceLine
            {
                Position = position++,
                ArticleNumber = product?.Descendants(ram + "SellerAssignedID").FirstOrDefault()?.Value ?? product?.Descendants(ram + "BuyerAssignedID").FirstOrDefault()?.Value ?? "",
                Ean = ExtractEan(product),
                Description = product?.Descendants(ram + "Name").FirstOrDefault()?.Value ?? "",
                Quantity = quantity,
                Unit = MapUnitCode(qtyElement?.Attribute("unitCode")?.Value ?? "ST"),
                NetUnitPrice = netPrice,
                MetalSurcharge = metalSurcharge,
                GrossListPrice = grossPrice,
                PriceBasisQuantity = basisQty,
                LineTotal = lineTotal > 0m ? lineTotal : PricingHelper.CalculateLineTotal(quantity, netPrice, metalSurcharge, basisQty)
            });
        }

        return MergeMetalSurchargeLines(rawLines);
    }

    private static List<InvoiceLine> MergeMetalSurchargeLines(List<InvoiceLine> rawLines)
    {
        var result = new List<InvoiceLine>();
        foreach (var line in rawLines)
        {
            if (!IsMetalSurchargeLine(line))
            {
                line.Position = result.Count + 1;
                result.Add(line);
                continue;
            }

            var target = FindBestMetalSurchargeTarget(result, line);
            if (target == null)
            {
                line.Position = result.Count + 1;
                result.Add(line);
                continue;
            }

            var surchargeTotal = line.LineTotal > 0m
                ? line.LineTotal
                : PricingHelper.CalculateLineTotal(line.Quantity, line.NetUnitPrice, line.MetalSurcharge, line.PriceBasisQuantity);
            var basisQuantity = target.PriceBasisQuantity <= 0m ? 1m : target.PriceBasisQuantity;
            if (target.Quantity > 0m)
            {
                target.MetalSurcharge += surchargeTotal * basisQuantity / target.Quantity;
            }
            target.LineTotal += surchargeTotal;
        }

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Position = i + 1;
        }

        return result;
    }

    private static InvoiceLine? FindBestMetalSurchargeTarget(IEnumerable<InvoiceLine> candidates, InvoiceLine surchargeLine)
    {
        return candidates
            .Where(line => !IsMetalSurchargeLine(line))
            .Select(line => new { Line = line, Score = ScoreMetalSurchargeTarget(line, surchargeLine) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Line)
            .FirstOrDefault();
    }

    private static int ScoreMetalSurchargeTarget(InvoiceLine candidate, InvoiceLine surchargeLine)
    {
        var score = 0;
        if (candidate.Quantity > 0m && surchargeLine.Quantity > 0m && candidate.Quantity == surchargeLine.Quantity)
        {
            score += 50;
        }

        if (string.Equals(candidate.Unit, surchargeLine.Unit, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (string.Equals(candidate.Unit, "M", StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (LooksLikeCable(candidate))
        {
            score += 20;
        }

        return score;
    }

    private static bool LooksLikeCable(InvoiceLine line)
    {
        var text = $"{line.ArticleNumber} {line.Description}";
        return CableRegex.IsMatch(text);
    }

    private static bool IsMetalSurchargeLine(InvoiceLine line)
    {
        var text = $"{line.ArticleNumber} {line.Description}";
        return MetalSurchargeRegex.IsMatch(text);
    }

    private static decimal ExtractMetalSurchargeFromAllowanceCharges(XElement item, decimal quantity, decimal basisQuantity)
    {
        var surchargeTotal = item.Descendants(ram + "SpecifiedTradeAllowanceCharge")
            .Where(x => ParseBooleanIndicator(x.Element(ram + "ChargeIndicator")?.Element(udt + "Indicator")?.Value))
            .Where(x => MetalSurchargeRegex.IsMatch((x.Element(ram + "Reason")?.Value ?? string.Empty).Trim()))
            .Sum(x => ParseInvariant(x.Element(ram + "ActualAmount")?.Value));

        if (surchargeTotal <= 0m || quantity <= 0m)
        {
            return 0m;
        }

        var divisor = basisQuantity <= 0m ? 1m : basisQuantity;
        return surchargeTotal * divisor / quantity;
    }

    private static bool ParseBooleanIndicator(string? value)
    {
        return string.Equals((value ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals((value ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractEan(XElement? product)
    {
        if (product == null) return "";
        var exact = product.Descendants(ram + "GlobalID").FirstOrDefault(x => (string?)x.Attribute("schemeID") == "0160")?.Value;
        if (!string.IsNullOrWhiteSpace(exact)) return exact.Trim();
        return product.Descendants(ram + "GlobalID").Select(x => (x.Value ?? "").Trim()).FirstOrDefault(x => x.Length is >= 8 and <= 14 && x.All(char.IsDigit)) ?? "";
    }

    private static decimal ParseInvariant(string? value) => string.IsNullOrWhiteSpace(value) ? 0m : decimal.Parse(value, CultureInfo.InvariantCulture);

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
