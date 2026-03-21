using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
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
    private static readonly XNamespace rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
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
    private int? _loadedReviewInvoiceId;

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
        var dlg = new OpenFileDialog { Filter = "PDF Dateien (*.pdf)|*.pdf", Multiselect = true };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        _ = ImportFilesForReviewAsync(dlg.FileNames, forceClassic: false);
    }

    private void EditManualLines_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ManualInvoiceLinesDialog(_manualLines, NoSupplierInvoiceCheckBox.IsChecked == true)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _manualLines.Clear();
        foreach (var line in dialog.ResultLines)
        {
            _manualLines.Add(CloneLine(line));
        }
        if (_currentPdfBytes is null)
        {
            _currentSourcePdfPath = "";
            _currentContentHash = "";
            SourceInfoText.Text = NoSupplierInvoiceCheckBox.IsChecked == true ? "Manuelle Erfassung ohne Rechnung" : "Manuelle Erfassung";
        }

        RenumberLines();
        ManualLinesGrid.Items.Refresh();
        SetStatus($"{_manualLines.Count} Position(en) im Dialog aktualisiert.", StatusMessageType.Success);
    }

    private void RemoveSelectedLine_Click(object sender, RoutedEventArgs e)
    {
        var selectedLines = ManualLinesGrid.SelectedItems.OfType<ManualInvoiceLineInput>().ToList();
        if (selectedLines.Count == 0)
        {
            SetStatus("Bitte zuerst mindestens eine Position markieren.", StatusMessageType.Warning);
            return;
        }

        foreach (var selected in selectedLines)
        {
            _manualLines.Remove(selected);
        }

        RenumberLines();
        SetStatus($"{selectedLines.Count} Position(en) entfernt.", StatusMessageType.Success);
    }

    private void ManualLinesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _ = TryParseDecimal(InvoiceTotalAmountText.Text, out var invoiceTotalAmount);
            ApplyGrossAmountsFromInvoiceTotal(_manualLines, invoiceTotalAmount);
            ManualLinesGrid.Items.Refresh();
        });
    }

    private async void SaveInvoice_Click(object sender, RoutedEventArgs e)
    {
        var hasSupplierInvoice = NoSupplierInvoiceCheckBox.IsChecked != true;
        var invoiceDirection = InvoiceDirectionCombo.SelectedValue as string ?? "Expense";
        var invoiceNumber = (InvoiceNumberText.Text ?? "").Trim();
        var supplierName = (SupplierNameText.Text ?? "").Trim();
        var hasShippingCost = TryParseDecimal(ShippingCostText.Text, out var shippingCostInput) && shippingCostInput > 0m;

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
        var invoiceDate = InvoiceDatePicker.SelectedDate.GetValueOrDefault();

        if (hasSupplierInvoice && PaymentDueDatePicker.SelectedDate == null)
        {
            SetStatus("Bitte ein Zahlungsdatum / Faelligkeitsdatum auswaehlen.", StatusMessageType.Error);
            return;
        }

        var hasInvoiceTotal = TryParseDecimal(InvoiceTotalAmountText.Text, out var invoiceTotalAmount) && invoiceTotalAmount > 0m;
        if (hasSupplierInvoice && !hasInvoiceTotal)
        {
            SetStatus("Bitte einen gueltigen Rechnungsbetrag groesser als 0 eingeben.", StatusMessageType.Error);
            return;
        }

        if (!hasSupplierInvoice && !string.IsNullOrWhiteSpace(InvoiceTotalAmountText.Text) && !hasInvoiceTotal)
        {
            SetStatus("Bitte einen gueltigen Rechnungsbetrag eingeben oder das Feld leer lassen.", StatusMessageType.Error);
            return;
        }

        if (!hasInvoiceTotal)
        {
            invoiceTotalAmount = 0m;
        }

        if (!string.IsNullOrWhiteSpace(ShippingCostText.Text) && !hasShippingCost && !string.Equals((ShippingCostText.Text ?? "").Trim(), "0", StringComparison.Ordinal))
        {
            SetStatus("Bitte gueltige Versandkosten eingeben oder das Feld leer lassen.", StatusMessageType.Error);
            return;
        }

        if (!hasShippingCost)
        {
            shippingCostInput = 0m;
        }

        if (!hasSupplierInvoice && _manualLines.Count == 0)
        {
            SetStatus("Bei Erfassung ohne Rechnungsbeleg bitte mindestens eine manuelle Position anlegen.", StatusMessageType.Error);
            return;
        }

        var effectiveInvoiceNumber = hasSupplierInvoice ? invoiceNumber : BuildManualDocumentNumber();
        var paymentDueDate = PaymentDueDatePicker.SelectedDate?.Date;
        var shippingIsNet = ShippingAmountModeCombo.SelectedIndex == 1;
        var preparedLines = PrepareLinesForPersistence(_manualLines, invoiceTotalAmount, shippingCostInput, shippingIsNet, out var shippingCostNet, out var shippingCostGross);
        var hash = string.IsNullOrWhiteSpace(_currentContentHash)
            ? BuildManualHash(invoiceDirection, effectiveInvoiceNumber, invoiceDate, paymentDueDate, supplierName, invoiceTotalAmount, shippingCostGross, hasSupplierInvoice, preparedLines)
            : _currentContentHash;

        try
        {
            var isReviewSave = _loadedReviewInvoiceId.HasValue;
            if (isReviewSave)
            {
                var loadedReviewInvoiceId = _loadedReviewInvoiceId.GetValueOrDefault();
                await App.Api.UpdateInvoiceAsync(
                    loadedReviewInvoiceId,
                    invoiceDirection,
                    "Finalized",
                    effectiveInvoiceNumber,
                    invoiceDate,
                    null,
                    paymentDueDate,
                    null,
                    supplierName,
                    AccountingCategoryCombo.SelectedValue as string ?? "MaterialAndGoods",
                    string.Empty,
                    false,
                    invoiceTotalAmount,
                    shippingCostNet,
                    shippingCostGross,
                    _currentSourcePdfPath,
                    _currentOriginalPdfFileName,
                    _currentPdfBytes is null ? null : Convert.ToBase64String(_currentPdfBytes),
                    hash,
                    preparedLines,
                    hasSupplierInvoice);
                SetStatus($"Prüfrechnung {effectiveInvoiceNumber} wurde als Rechnung gespeichert.", StatusMessageType.Success);
            }
            else
            {
                await App.Api.SaveInvoiceAsync(
                    invoiceDirection,
                    "Finalized",
                    effectiveInvoiceNumber,
                    invoiceDate,
                    null,
                    paymentDueDate,
                    null,
                    supplierName,
                    AccountingCategoryCombo.SelectedValue as string ?? "MaterialAndGoods",
                    string.Empty,
                    false,
                    invoiceTotalAmount,
                    shippingCostNet,
                    shippingCostGross,
                    _currentSourcePdfPath,
                    _currentOriginalPdfFileName,
                    _currentPdfBytes is null ? null : Convert.ToBase64String(_currentPdfBytes),
                    hash,
                    preparedLines,
                    hasSupplierInvoice);
            }
            var importedLineCount = preparedLines.Count;
            ResetForm();
            await LoadStoredInvoicesAsync();
            if (!isReviewSave)
            {
                var summary = hasSupplierInvoice
                    ? importedLineCount > 0
                        ? $"Rechnung {invoiceNumber} mit {importedLineCount} Position(en) erfolgreich importiert."
                        : $"Rechnung {invoiceNumber} wurde ohne Positionen mit Rechnungsbetrag gespeichert."
                    : $"Manuell hinzugefuegte Positionen ohne Lieferantenrechnung wurden gespeichert.";
                SetStatus(summary, StatusMessageType.Success);
            }
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

        SaveInvoiceButton.Content = noSupplierInvoice ? "Positionen speichern" : "Rechnung speichern";
        InvoiceTotalAmountLabel.Text = noSupplierInvoice ? "Rechnungsbetrag optional" : "Rechnungsbetrag * Pflicht";
        InvoiceTotalAmountLabel.Style = (Style)FindResource(noSupplierInvoice ? "MutedLabelStyle" : "RequiredLabelStyle");
        PaymentDueDateLabel.Text = noSupplierInvoice ? "Zahlbar bis / Fällig am optional" : "Zahlbar bis / Fällig am * Pflicht";
        PaymentDueDateLabel.Style = (Style)FindResource(noSupplierInvoice ? "MutedLabelStyle" : "RequiredLabelStyle");
        ShippingInfoText.Text = noSupplierInvoice
            ? "Versand wird aus dem eingegebenen Betrag auf Netto und Brutto der Positionen verteilt."
            : "Versand wird anteilig in Netto und Brutto auf alle Positionen verteilt.";
        if (string.IsNullOrWhiteSpace(_currentSourcePdfPath))
        {
            SourceInfoText.Text = noSupplierInvoice ? "Manuelle Erfassung ohne Rechnung" : "Manuelle Erfassung";
        }
    }

    private void LoadImportedInvoiceIntoEditor(ImportedExpenseInvoice importedInvoice)
    {
        _loadedReviewInvoiceId = null;
        _currentPdfBytes = importedInvoice.PdfBytes;
        _currentOriginalPdfFileName = importedInvoice.OriginalPdfFileName;
        _currentContentHash = importedInvoice.ContentHash;
        _currentSourcePdfPath = importedInvoice.FilePath;
        SourceInfoText.Text = importedInvoice.FilePath;
        NoSupplierInvoiceCheckBox.IsChecked = false;
        InvoiceDirectionCombo.SelectedValue = "Expense";
        InvoiceNumberText.Text = importedInvoice.InvoiceNumber;
        InvoiceDatePicker.SelectedDate = importedInvoice.InvoiceDate;
        PaymentDueDatePicker.SelectedDate = importedInvoice.PaymentDueDate;
        SupplierNameText.Text = importedInvoice.SupplierName;
        AccountingCategoryCombo.SelectedValue = importedInvoice.AccountingCategory;
        InvoiceTotalAmountText.Text = importedInvoice.InvoiceTotalAmount > 0m
            ? importedInvoice.InvoiceTotalAmount.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))
            : string.Empty;
        ShippingCostText.Text = importedInvoice.ShippingCostGross > 0m
            ? importedInvoice.ShippingCostGross.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))
            : "0";
        ShippingAmountModeCombo.SelectedIndex = 0;

        _manualLines.Clear();
        foreach (var line in importedInvoice.Lines)
        {
            _manualLines.Add(CloneLine(line));
        }

        UpdateInvoiceModeUi();
        ManualLinesGrid.Items.Refresh();
    }

    private void ResetForm()
    {
        _loadedReviewInvoiceId = null;
        InvoiceNumberText.Clear();
        SupplierNameText.Clear();
        InvoiceDatePicker.SelectedDate = DateTime.Today;
        PaymentDueDatePicker.SelectedDate = null;
        NoSupplierInvoiceCheckBox.IsChecked = false;
        InvoiceDirectionCombo.SelectedValue = "Expense";
        _manualLines.Clear();
        _currentSourcePdfPath = "";
        _currentContentHash = "";
        _currentPdfBytes = null;
        _currentOriginalPdfFileName = "";
        AccountingCategoryCombo.SelectedValue = "MaterialAndGoods";
        InvoiceTotalAmountText.Clear();
        ShippingCostText.Text = "0";
        ShippingAmountModeCombo.SelectedIndex = 0;
        UpdateInvoiceModeUi();
    }

    private async Task LoadStoredInvoicesAsync()
    {
        _storedInvoices.Clear();
        foreach (var item in (await App.Api.GetInvoicesAsync())
                     .Where(x => x.CanLoadForReview)
                     .OrderByDescending(x => x.InvoiceDate)
                     .ThenByDescending(x => x.InvoiceId))
        {
            _storedInvoices.Add(item);
        }
    }

    private async void LoadReviewInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.CanLoadForReview)
        {
            SetStatus("Bitte zuerst eine Ausgaberechnung im Status 'Pruefen' auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var detail = await App.Api.GetInvoiceAsync(invoice.InvoiceId);
            byte[]? pdfBytes = null;
            if (detail.HasStoredPdf)
            {
                pdfBytes = await App.Api.DownloadInvoicePdfAsync(detail.InvoiceId);
            }

            LoadReviewInvoiceIntoEditor(detail, pdfBytes);
            SetStatus($"Prüfrechnung {detail.InvoiceNumber} wurde in den Rechnungsimport geladen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Prüfrechnung konnte nicht geladen werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void OpenStoredPdf_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.HasStoredPdf)
        {
            SetStatus("Bitte zuerst eine Prüfrechnung mit gespeicherter PDF auswählen.", StatusMessageType.Warning);
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

    private async void EditDraftInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.CanEditDraft)
        {
            SetStatus("Bitte zuerst einen Einnahme-Entwurf im Archiv auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var finalizationDate = DateTime.Today;
            var detail = await App.Api.GetInvoiceAsync(invoice.InvoiceId);
            var company = await App.Api.GetCompanyProfileAsync();
            var customer = (await App.Api.GetCustomersAsync()).FirstOrDefault(x => x.CustomerId == detail.CustomerId);
            if (customer is null)
            {
                SetStatus("Der zugehoerige Kunde wurde fuer diesen Entwurf nicht gefunden.", StatusMessageType.Error);
                return;
            }

            var lines = detail.Lines.Select(x => new ManualInvoiceLineInput
            {
                Position = x.Position,
                ArticleNumber = x.ArticleNumber,
                Ean = x.Ean,
                Description = x.Description,
                Quantity = x.Quantity,
                Unit = x.Unit,
                NetUnitPrice = x.NetUnitPrice,
                MetalSurcharge = x.MetalSurcharge,
                GrossListPrice = x.GrossListPrice,
                GrossUnitPrice = x.GrossUnitPrice,
                PriceBasisQuantity = x.PriceBasisQuantity,
                GrossLineTotal = x.GrossLineTotal
            }).ToList();

            var dialog = new DraftInvoiceEditorDialog(
                detail.InvoiceNumber,
                customer.CustomerNumber,
                customer.Name,
                new GeneratedInvoiceOptions
                {
                    InvoiceNumber = detail.InvoiceNumber,
                    CustomerNumber = customer.CustomerNumber,
                    InvoiceDate = detail.InvoiceDate,
                    DeliveryDate = detail.DeliveryDate ?? detail.InvoiceDate,
                    Subject = detail.Subject,
                    ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation
                },
                lines)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            lines = dialog.ResultLines;

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF-Datei (*.pdf)|*.pdf",
                FileName = $"{detail.InvoiceNumber}_{SanitizeFileName(customer.Name)}_Entwurf.pdf"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
            {
                Company = company,
                Customer = customer,
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = dialog.Result.InvoiceDate.Date,
                DeliveryDate = dialog.Result.DeliveryDate.Date,
                Subject = dialog.Result.Subject,
                ApplySmallBusinessRegulation = dialog.Result.ApplySmallBusinessRegulation,
                IsDraft = true,
                Lines = lines.Select(x => new CustomerInvoicePdfService.InvoiceLine
                {
                    Position = x.Position,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    Unit = x.Unit,
                    UnitPrice = PricingHelper.NormalizeUnitPrice(x.NetUnitPrice, x.MetalSurcharge, x.PriceBasisQuantity),
                    LineTotal = x.LineTotal
                }).ToList()
            });

            await File.WriteAllBytesAsync(saveDialog.FileName, pdfBytes);
            await App.Api.UpdateInvoiceAsync(
                detail.InvoiceId,
                "Revenue",
                "Draft",
                detail.InvoiceNumber,
                dialog.Result.InvoiceDate.Date,
                dialog.Result.DeliveryDate.Date,
                detail.PaymentDueDate,
                detail.CustomerId,
                detail.SupplierName,
                detail.AccountingCategory,
                dialog.Result.Subject,
                dialog.Result.ApplySmallBusinessRegulation,
                PricingHelper.CalculateRevenueGrossTotal(lines.Sum(x => x.LineTotal), dialog.Result.ApplySmallBusinessRegulation),
                0m,
                0m,
                saveDialog.FileName,
                Path.GetFileName(saveDialog.FileName),
                Convert.ToBase64String(pdfBytes),
                Sha256(pdfBytes),
                lines,
                hasSupplierInvoice: true);

            await LoadStoredInvoicesAsync();
            SetStatus($"Entwurf {detail.InvoiceNumber} wurde aktualisiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Entwurf konnte nicht bearbeitet werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void FinalizeDraftInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.CanFinalizeDraft)
        {
            SetStatus("Bitte zuerst einen Einnahme-Entwurf im Archiv auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var finalizationDate = DateTime.Today;
            var detail = await App.Api.GetInvoiceAsync(invoice.InvoiceId);
            var company = await App.Api.GetCompanyProfileAsync();
            var customer = (await App.Api.GetCustomersAsync()).FirstOrDefault(x => x.CustomerId == detail.CustomerId);
            if (customer is null)
            {
                SetStatus("Der zugehoerige Kunde wurde fuer diesen Entwurf nicht gefunden.", StatusMessageType.Error);
                return;
            }

            var dialog = new GenerateInvoiceDialog(
                detail.InvoiceNumber,
                customer.CustomerNumber,
                customer.Name,
                isDraftMode: false,
                initialOptions: new GeneratedInvoiceOptions
                {
                    InvoiceNumber = detail.InvoiceNumber,
                    CustomerNumber = customer.CustomerNumber,
                    InvoiceDate = finalizationDate,
                    DeliveryDate = detail.DeliveryDate ?? detail.InvoiceDate,
                    Subject = detail.Subject,
                    ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation
                })
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            var lines = detail.Lines.Select(x => new ManualInvoiceLineInput
            {
                Position = x.Position,
                ArticleNumber = x.ArticleNumber,
                Ean = x.Ean,
                Description = x.Description,
                Quantity = x.Quantity,
                Unit = x.Unit,
                NetUnitPrice = x.NetUnitPrice,
                MetalSurcharge = x.MetalSurcharge,
                GrossListPrice = x.GrossListPrice,
                GrossUnitPrice = x.GrossUnitPrice,
                PriceBasisQuantity = x.PriceBasisQuantity,
                GrossLineTotal = x.GrossLineTotal
            }).ToList();

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF-Datei (*.pdf)|*.pdf",
                FileName = $"{detail.InvoiceNumber}_{SanitizeFileName(customer.Name)}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
            {
                Company = company,
                Customer = customer,
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = finalizationDate,
                DeliveryDate = dialog.Result.DeliveryDate.Date,
                Subject = dialog.Result.Subject,
                ApplySmallBusinessRegulation = dialog.Result.ApplySmallBusinessRegulation,
                IsDraft = false,
                Lines = lines.Select(x => new CustomerInvoicePdfService.InvoiceLine
                {
                    Position = x.Position,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    Unit = x.Unit,
                    UnitPrice = PricingHelper.NormalizeUnitPrice(x.NetUnitPrice, x.MetalSurcharge, x.PriceBasisQuantity),
                    LineTotal = x.LineTotal
                }).ToList()
            });

            await File.WriteAllBytesAsync(saveDialog.FileName, pdfBytes);
            await App.Api.UpdateInvoiceAsync(
                detail.InvoiceId,
                "Revenue",
                "Draft",
                detail.InvoiceNumber,
                finalizationDate,
                dialog.Result.DeliveryDate.Date,
                detail.PaymentDueDate,
                detail.CustomerId,
                detail.SupplierName,
                detail.AccountingCategory,
                dialog.Result.Subject,
                dialog.Result.ApplySmallBusinessRegulation,
                PricingHelper.CalculateRevenueGrossTotal(lines.Sum(x => x.LineTotal), dialog.Result.ApplySmallBusinessRegulation),
                0m,
                0m,
                saveDialog.FileName,
                Path.GetFileName(saveDialog.FileName),
                Convert.ToBase64String(pdfBytes),
                Sha256(pdfBytes),
                lines,
                hasSupplierInvoice: true);
            await App.Api.FinalizeInvoiceAsync(detail.InvoiceId);

            await LoadStoredInvoicesAsync();
            SetStatus($"Entwurf {detail.InvoiceNumber} wurde finalisiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Entwurf konnte nicht finalisiert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void CancelInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is not InvoiceEntity invoice || !invoice.CanCancel)
        {
            SetStatus("Bitte zuerst eine Einnahmerechnung oder einen Entwurf im Archiv auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var dialog = new TextPromptDialog("Rechnung stornieren", "Bitte gib den Grund fuer die Stornierung ein.")
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await App.Api.CancelInvoiceAsync(invoice.InvoiceId, dialog.Result);
            await LoadStoredInvoicesAsync();
            SetStatus($"Rechnung {invoice.InvoiceNumber} wurde storniert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Rechnung konnte nicht storniert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void DeleteReviewInvoices_Click(object sender, RoutedEventArgs e)
    {
        var invoices = InvoicesGrid.SelectedItems.OfType<InvoiceEntity>().Where(x => x.CanLoadForReview).ToList();
        if (invoices.Count == 0)
        {
            SetStatus("Bitte zuerst mindestens eine Prüfrechnung auswählen.", StatusMessageType.Warning);
            return;
        }

        var confirmationText = invoices.Count == 1
            ? $"Soll die Prüfrechnung {invoices[0].DisplayNumber} wirklich gelöscht werden?"
            : $"Sollen {invoices.Count} Prüfrechnungen wirklich gelöscht werden?";
        if (MessageBox.Show(confirmationText, "Prüfrechnung löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var invoice in invoices)
            {
                await App.Api.DeleteInvoiceAsync(invoice.InvoiceId);
            }

            if (_loadedReviewInvoiceId.HasValue && invoices.Any(x => x.InvoiceId == _loadedReviewInvoiceId.Value))
            {
                ResetForm();
            }

            await LoadStoredInvoicesAsync();
            SetStatus(invoices.Count == 1
                ? "Prüfrechnung wurde gelöscht."
                : $"{invoices.Count} Prüfrechnungen wurden gelöscht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Prüfrechnungen konnten nicht gelöscht werden: {ex.Message}", StatusMessageType.Error);
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

    private void ImportRoot_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetDataPresent("FileGroupDescriptorW")
            || e.Data.GetDataPresent("FileGroupDescriptor"))
        {
            if (DropOverlay.Visibility != Visibility.Visible)
            {
                DropOverlay.Visibility = Visibility.Visible;
            }
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (DropOverlay.Visibility != Visibility.Collapsed)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void ImportRoot_DragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(ImportRoot);
        if (position.X < 0 || position.Y < 0 || position.X > ImportRoot.ActualWidth || position.Y > ImportRoot.ActualHeight)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void ImportRoot_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        var tempFiles = new List<string>();
        try
        {
            var files = await ExtractDroppedPdfFilesAsync(e.Data, tempFiles);
            if (files.Count == 0)
            {
                SetStatus("Bitte nur PDF-Dateien oder PDF-Anhänge auf den Rechnungsimport ziehen.", StatusMessageType.Warning);
                return;
            }

            await ImportFilesForReviewAsync(files, forceClassic: false);
        }
        finally
        {
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static async Task<List<string>> ExtractDroppedPdfFilesAsync(IDataObject dataObject, List<string> tempFiles)
    {
        if (dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            return ((string[])dataObject.GetData(DataFormats.FileDrop))
                .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var descriptorFormat = dataObject.GetDataPresent("FileGroupDescriptorW") ? "FileGroupDescriptorW" : "FileGroupDescriptor";
        if (!dataObject.GetDataPresent(descriptorFormat) || !dataObject.GetDataPresent("FileContents"))
        {
            return new List<string>();
        }

        using var descriptorStream = dataObject.GetData(descriptorFormat) as MemoryStream;
        if (descriptorStream is null)
        {
            return new List<string>();
        }

        var fileNames = ReadDescriptorFileNames(descriptorStream, descriptorFormat == "FileGroupDescriptorW");
        if (fileNames.Count == 0)
        {
            return new List<string>();
        }

        var fileContents = dataObject.GetData("FileContents");
        var extractedFiles = new List<string>();
        for (var i = 0; i < fileNames.Count; i++)
        {
            var fileName = fileNames[i];
            if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceStream = GetVirtualFileStream(fileContents, i);
            if (sourceStream is null)
            {
                continue;
            }

            await using (sourceStream.ConfigureAwait(false))
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}");
                await using var targetStream = File.Create(tempFile);
                await sourceStream.CopyToAsync(targetStream);
                tempFiles.Add(tempFile);
                extractedFiles.Add(tempFile);
            }
        }

        return extractedFiles;
    }

    private static Stream? GetVirtualFileStream(object fileContents, int index)
    {
        if (fileContents is MemoryStream memoryStream)
        {
            return new MemoryStream(memoryStream.ToArray());
        }

        if (fileContents is Stream directStream)
        {
            return directStream;
        }

        if (fileContents is object[] objectArray && index < objectArray.Length)
        {
            return objectArray[index] switch
            {
                MemoryStream ms => new MemoryStream(ms.ToArray()),
                Stream stream => stream,
                _ => null
            };
        }

        return null;
    }

    private static List<string> ReadDescriptorFileNames(Stream descriptorStream, bool unicode)
    {
        var fileNames = new List<string>();
        using var reader = new BinaryReader(descriptorStream, unicode ? Encoding.Unicode : Encoding.Default, leaveOpen: true);
        descriptorStream.Position = 0;
        var itemCount = reader.ReadInt32();
        var nameBufferLength = unicode ? 520 : 260;
        var skipLength = unicode ? 592 : 332;

        for (var i = 0; i < itemCount; i++)
        {
            reader.ReadBytes(4); // dwFlags
            reader.ReadBytes(16); // clsid
            reader.ReadBytes(8); // sizel
            reader.ReadBytes(8); // pointl
            reader.ReadBytes(4); // dwFileAttributes
            reader.ReadBytes(16); // FILETIME x2
            reader.ReadBytes(8); // nFileSizeHigh/Low
            var rawName = reader.ReadBytes(nameBufferLength);
            var fileName = unicode
                ? Encoding.Unicode.GetString(rawName).TrimEnd('\0')
                : Encoding.Default.GetString(rawName).TrimEnd('\0');
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fileNames.Add(fileName);
            }

            var consumed = 4 + 16 + 8 + 8 + 4 + 16 + 8 + nameBufferLength;
            var remaining = skipLength - consumed;
            if (remaining > 0)
            {
                reader.ReadBytes(remaining);
            }
        }

        return fileNames;
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

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static string BuildManualHash(string invoiceDirection, string invoiceNumber, DateTime invoiceDate, DateTime? paymentDueDate, string supplierName, decimal invoiceTotalAmount, decimal shippingCostGross, bool hasSupplierInvoice, IEnumerable<ManualInvoiceLineInput> lines)
    {
        var payload = string.Join("|", new[]
        {
            invoiceDirection,
            invoiceNumber,
            invoiceDate.ToString("yyyyMMdd"),
            paymentDueDate?.ToString("yyyyMMdd") ?? string.Empty,
            supplierName,
            invoiceTotalAmount.ToString(CultureInfo.InvariantCulture),
            shippingCostGross.ToString(CultureInfo.InvariantCulture),
            hasSupplierInvoice ? "WITH-INVOICE" : "WITHOUT-INVOICE",
            string.Join(";", lines.Select(l => $"{l.ArticleNumber}:{l.Description}:{l.Quantity}:{l.NetUnitPrice}:{l.MetalSurcharge}:{l.VatPercent}:{l.PriceBasisQuantity}"))
        });

        return Sha256(Encoding.UTF8.GetBytes(payload));
    }

    private static string BuildManualDocumentNumber() => $"MANUELL-{DateTime.Now:yyyyMMdd-HHmmss}";

    private async Task ImportFilesForReviewAsync(IEnumerable<string> filePaths, bool forceClassic)
    {
        var importedCount = 0;
        var failedImports = new List<string>();

        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var importedInvoice = ParseImportedInvoice(filePath, forceClassic);
                await App.Api.SaveInvoiceAsync(
                    "Expense",
                    "Review",
                    importedInvoice.InvoiceNumber,
                    importedInvoice.InvoiceDate,
                    null,
                    importedInvoice.PaymentDueDate,
                    null,
                    importedInvoice.SupplierName,
                    importedInvoice.AccountingCategory,
                    string.Empty,
                    false,
                    importedInvoice.InvoiceTotalAmount,
                    importedInvoice.ShippingCostNet,
                    importedInvoice.ShippingCostGross,
                    importedInvoice.FilePath,
                    importedInvoice.OriginalPdfFileName,
                    Convert.ToBase64String(importedInvoice.PdfBytes),
                    importedInvoice.ContentHash,
                    importedInvoice.Lines,
                    hasSupplierInvoice: true);
                importedCount++;
            }
            catch (Exception ex)
            {
                failedImports.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        await LoadStoredInvoicesAsync();
        ResetForm();

        if (failedImports.Count == 0)
        {
            SetStatus($"{importedCount} Rechnung(en) wurden im Status 'Pruefen' importiert.", StatusMessageType.Success);
            return;
        }

        var errorSummary = string.Join(" | ", failedImports.Take(3));
        if (failedImports.Count > 3)
        {
            errorSummary += $" | weitere {failedImports.Count - 3} Fehler";
        }

        SetStatus($"{importedCount} Rechnung(en) importiert, {failedImports.Count} fehlgeschlagen: {errorSummary}", importedCount > 0 ? StatusMessageType.Warning : StatusMessageType.Error);
    }

    private static List<ManualInvoiceLineInput> PrepareLinesForPersistence(IEnumerable<ManualInvoiceLineInput> lines, decimal invoiceTotalAmount, decimal shippingCostInput, bool shippingIsNet, out decimal shippingCostNet, out decimal shippingCostGross)
    {
        var preparedLines = lines.Select(CloneLine).ToList();
        foreach (var line in preparedLines)
        {
            line.ShippingNetShare = 0m;
            line.ShippingGrossShare = 0m;
            InitializeManualLineAmounts(line);
        }

        (shippingCostNet, shippingCostGross) = DetermineShippingAmounts(preparedLines, shippingCostInput, invoiceTotalAmount, shippingIsNet);
        shippingCostNet = DistributeShippingAcrossLines(preparedLines, shippingCostNet, shippingCostGross);
        ApplyGrossAmountsFromInvoiceTotal(preparedLines, invoiceTotalAmount);
        return preparedLines;
    }

    private ImportedExpenseInvoice ParseImportedInvoice(string filePath, bool forceClassic)
    {
        var pdfBytes = File.ReadAllBytes(filePath);
        var importedInvoice = new ImportedExpenseInvoice
        {
            FilePath = filePath,
            OriginalPdfFileName = Path.GetFileName(filePath),
            PdfBytes = pdfBytes,
            ContentHash = Sha256(pdfBytes),
            InvoiceDate = DateTime.Today,
            SupplierName = string.Empty,
            AccountingCategory = "MaterialAndGoods",
            InvoiceNumber = string.Empty,
            InvoiceTotalAmount = 0m,
            ShippingCostGross = 0m,
            ShippingCostNet = 0m,
            PaymentDueDate = null
        };

        if (!forceClassic)
        {
            var xmlBytes = ZugferdExtractor.ExtractEmbeddedXml(filePath);
            if (xmlBytes != null)
            {
                var doc = XDocument.Load(new MemoryStream(xmlBytes));
                importedInvoice.InvoiceNumber = ExtractInvoiceNumberFromXml(doc);
                importedInvoice.InvoiceDate = ExtractInvoiceDateFromXml(doc);
                importedInvoice.PaymentDueDate = ExtractPaymentDueDateFromXml(doc);
                importedInvoice.SupplierName = ExtractSupplierNameFromXml(doc);
                importedInvoice.InvoiceTotalAmount = ExtractInvoiceGrossTotalFromXml(doc);
                importedInvoice.Lines = ParseZugferdPositions(doc)
                    .Select(line => new ManualInvoiceLineInput
                    {
                        Position = line.Position,
                        ArticleNumber = line.ArticleNumber,
                        Ean = line.Ean,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        Unit = line.Unit,
                        NetUnitPrice = line.NetUnitPrice,
                        MetalSurcharge = line.MetalSurcharge,
                        VatPercent = 0m,
                        GrossListPrice = line.GrossListPrice,
                        GrossUnitPrice = line.GrossUnitPrice,
                        PriceBasisQuantity = line.PriceBasisQuantity,
                        GrossLineTotal = line.GrossLineTotal
                    })
                    .ToList();
                ApplyGrossAmountsFromInvoiceTotal(importedInvoice.Lines, importedInvoice.InvoiceTotalAmount);
                return importedInvoice;
            }
        }

        var parsed = PdfInvoiceImportService.Parse(filePath);
        importedInvoice.InvoiceNumber = parsed.InvoiceNumber;
        importedInvoice.InvoiceDate = parsed.InvoiceDate ?? DateTime.Today;
        importedInvoice.PaymentDueDate = parsed.PaymentDueDate;
        importedInvoice.SupplierName = parsed.SupplierName;
        importedInvoice.AccountingCategory = DetectAccountingCategory(parsed.RawText);
        importedInvoice.Lines = new List<ManualInvoiceLineInput>();
        return importedInvoice;
    }

    private void LoadReviewInvoiceIntoEditor(InvoiceEntity detail, byte[]? pdfBytes)
    {
        ImportedExpenseInvoice? parsedInvoice = null;
        if (pdfBytes is not null)
        {
            parsedInvoice = TryParseImportedInvoice(detail.OriginalPdfFileName, pdfBytes);
        }

        _loadedReviewInvoiceId = detail.InvoiceId;
        _currentPdfBytes = pdfBytes;
        _currentOriginalPdfFileName = detail.OriginalPdfFileName;
        _currentContentHash = detail.ContentHash;
        _currentSourcePdfPath = detail.SourcePdfPath;
        SourceInfoText.Text = string.IsNullOrWhiteSpace(detail.SourcePdfPath) ? detail.OriginalPdfFileName : detail.SourcePdfPath;
        NoSupplierInvoiceCheckBox.IsChecked = !detail.HasSupplierInvoice;
        InvoiceDirectionCombo.SelectedValue = detail.InvoiceDirection;
        InvoiceNumberText.Text = string.IsNullOrWhiteSpace(parsedInvoice?.InvoiceNumber) ? detail.InvoiceNumber : parsedInvoice.InvoiceNumber;
        InvoiceDatePicker.SelectedDate = parsedInvoice?.InvoiceDate ?? detail.InvoiceDate;
        PaymentDueDatePicker.SelectedDate = parsedInvoice?.PaymentDueDate ?? detail.PaymentDueDate;
        SupplierNameText.Text = string.IsNullOrWhiteSpace(parsedInvoice?.SupplierName) ? detail.SupplierName : parsedInvoice.SupplierName;
        AccountingCategoryCombo.SelectedValue = parsedInvoice?.AccountingCategory ?? detail.AccountingCategory;
        var effectiveInvoiceTotal = parsedInvoice?.InvoiceTotalAmount > 0m ? parsedInvoice.InvoiceTotalAmount : detail.InvoiceTotalAmount;
        InvoiceTotalAmountText.Text = effectiveInvoiceTotal > 0m
            ? effectiveInvoiceTotal.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))
            : string.Empty;
        var effectiveShippingGross = parsedInvoice?.ShippingCostGross > 0m ? parsedInvoice.ShippingCostGross : detail.ShippingCostGross;
        ShippingCostText.Text = effectiveShippingGross > 0m
            ? effectiveShippingGross.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))
            : "0";
        ShippingAmountModeCombo.SelectedIndex = detail.ShippingCostNet > 0m && Math.Abs(detail.ShippingCostGross - detail.ShippingCostNet) < 0.01m ? 1 : 0;

        _manualLines.Clear();
        var linesToLoad = parsedInvoice is not null && parsedInvoice.Lines.Count > 0
            ? parsedInvoice.Lines
            : detail.Lines.OrderBy(x => x.Position).Select(line => new ManualInvoiceLineInput
            {
                Position = line.Position,
                ArticleNumber = line.ArticleNumber,
                Ean = line.Ean,
                Description = line.Description,
                Quantity = line.Quantity,
                Unit = line.Unit,
                NetUnitPrice = line.NetUnitPrice,
                MetalSurcharge = line.MetalSurcharge,
                VatPercent = 0m,
                GrossListPrice = line.GrossListPrice,
                GrossUnitPrice = line.GrossUnitPrice,
                PriceBasisQuantity = line.PriceBasisQuantity,
                GrossLineTotal = line.GrossLineTotal
            }).ToList();

        foreach (var line in linesToLoad)
        {
            _manualLines.Add(CloneLine(line));
        }

        UpdateInvoiceModeUi();
        ManualLinesGrid.Items.Refresh();
    }

    private ImportedExpenseInvoice? TryParseImportedInvoice(string originalPdfFileName, byte[] pdfBytes)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{SanitizeFileName(string.IsNullOrWhiteSpace(originalPdfFileName) ? "rechnung.pdf" : originalPdfFileName)}");
        try
        {
            File.WriteAllBytes(tempFile, pdfBytes);
            return ParseImportedInvoice(tempFile, forceClassic: false);
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
            }
        }
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
            ShippingNetShare = line.ShippingNetShare,
            ShippingGrossShare = line.ShippingGrossShare,
            GrossLineTotal = line.GrossLineTotal
        };
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

    private static void ApplyGrossAmountsFromInvoiceTotal(IEnumerable<ManualInvoiceLineInput> lines, decimal invoiceTotalAmount)
    {
        var preparedLines = lines.ToList();
        if (preparedLines.Count == 0)
        {
            return;
        }

        var netTotal = PricingHelper.RoundCurrency(preparedLines.Sum(x => x.LineTotal));
        if (netTotal <= 0m)
        {
            foreach (var line in preparedLines)
            {
                InitializeManualLineAmounts(line);
            }

            return;
        }

        var fallbackGrossTotal = PricingHelper.RoundCurrency(preparedLines.Sum(x => x.GrossLineTotal));
        var effectiveGrossTotal = invoiceTotalAmount > 0m
            ? PricingHelper.RoundCurrency(invoiceTotalAmount)
            : (fallbackGrossTotal > 0m ? fallbackGrossTotal : netTotal);
        var grossFactor = effectiveGrossTotal / netTotal;
        if (grossFactor < 1m || grossFactor > 2m)
        {
            grossFactor = 1m;
            effectiveGrossTotal = netTotal;
        }

        var assignedGrossTotal = 0m;
        for (var i = 0; i < preparedLines.Count; i++)
        {
            var line = preparedLines[i];
            var grossLineTotal = i == preparedLines.Count - 1
                ? PricingHelper.RoundCurrency(effectiveGrossTotal - assignedGrossTotal)
                : PricingHelper.CalculateGrossLineTotal(line.LineTotal, grossFactor);
            line.GrossLineTotal = grossLineTotal;
            line.GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(grossLineTotal, line.Quantity);
            assignedGrossTotal += grossLineTotal;
        }
    }

    private static (decimal ShippingNet, decimal ShippingGross) DetermineShippingAmounts(List<ManualInvoiceLineInput> lines, decimal shippingCostInput, decimal invoiceTotalAmount, bool shippingIsNet)
    {
        if (lines.Count == 0 || shippingCostInput <= 0m)
        {
            return (0m, 0m);
        }

        var baseNetTotal = PricingHelper.RoundCurrency(lines.Sum(CalculateBaseLineTotal));
        var baseGrossTotal = PricingHelper.RoundCurrency(lines.Sum(x => x.GrossLineTotal));
        var grossFactor = DetermineGrossFactor(lines, invoiceTotalAmount, baseNetTotal, baseGrossTotal);
        return shippingIsNet
            ? (PricingHelper.RoundCurrency(shippingCostInput), PricingHelper.RoundCurrency(shippingCostInput * grossFactor))
            : (PricingHelper.RoundCurrency(shippingCostInput / grossFactor), PricingHelper.RoundCurrency(shippingCostInput));
    }

    private static decimal DistributeShippingAcrossLines(List<ManualInvoiceLineInput> lines, decimal shippingCostNet, decimal shippingCostGross)
    {
        if (lines.Count == 0 || shippingCostGross <= 0m)
        {
            return 0m;
        }

        var baseNetTotal = PricingHelper.RoundCurrency(lines.Sum(x => CalculateBaseLineTotal(x)));
        var baseGrossTotal = PricingHelper.RoundCurrency(lines.Sum(x => x.GrossLineTotal));
        var assignedNet = 0m;
        var assignedGross = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNetBase = CalculateBaseLineTotal(line);
            var lineGrossBase = line.GrossLineTotal;
            var netShare = i == lines.Count - 1
                ? PricingHelper.RoundCurrency(shippingCostNet - assignedNet)
                : PricingHelper.RoundCurrency(shippingCostNet * (baseNetTotal <= 0m ? (1m / lines.Count) : (lineNetBase / baseNetTotal)));
            var grossShare = i == lines.Count - 1
                ? PricingHelper.RoundCurrency(shippingCostGross - assignedGross)
                : PricingHelper.RoundCurrency(shippingCostGross * (baseGrossTotal <= 0m ? (1m / lines.Count) : (lineGrossBase / baseGrossTotal)));

            line.ShippingNetShare = netShare;
            line.ShippingGrossShare = grossShare;
            line.GrossLineTotal = PricingHelper.RoundCurrency(lineGrossBase + grossShare);
            line.GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(line.GrossLineTotal, line.Quantity);
            assignedNet += netShare;
            assignedGross += grossShare;
        }

        return shippingCostNet;
    }

    private static decimal DetermineGrossFactor(List<ManualInvoiceLineInput> lines, decimal invoiceTotalAmount, decimal baseNetTotal, decimal baseGrossTotal)
    {
        if (baseNetTotal > 0m && baseGrossTotal > 0m)
        {
            var factor = baseGrossTotal / baseNetTotal;
            if (factor >= 1m && factor <= 2m)
            {
                return factor;
            }
        }

        if (baseNetTotal > 0m && invoiceTotalAmount > 0m)
        {
            var factor = invoiceTotalAmount / baseNetTotal;
            if (factor >= 1m && factor <= 2m)
            {
                return factor;
            }
        }

        var vatFactors = lines
            .Where(x => x.VatPercent > 0m)
            .Select(x => 1m + (x.VatPercent / 100m))
            .ToList();
        if (vatFactors.Count > 0)
        {
            return vatFactors
                .GroupBy(x => Math.Round(x, 4))
                .OrderByDescending(g => g.Count())
                .Select(g => g.First())
                .First();
        }

        return 1m;
    }

    private static decimal CalculateBaseLineTotal(ManualInvoiceLineInput line)
        => line.Quantity * ((line.NetUnitPrice + line.MetalSurcharge) / (line.PriceBasisQuantity <= 0m ? 1m : line.PriceBasisQuantity));

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
    {
        var exchangedDocument = FindFirstByLocalName(doc.Root, "ExchangedDocument");
        var candidates = new List<string?>();

        if (exchangedDocument != null)
        {
            candidates.Add(FindChildValueByLocalName(exchangedDocument, "ID"));
            candidates.Add(FindChildValueByLocalName(exchangedDocument, "IssuerAssignedID"));
            candidates.AddRange(
                exchangedDocument
                    .Elements()
                    .Where(x => x.Name.LocalName.Contains("ID", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Value));
        }

        candidates.Add(FindFirstValueByLocalName(doc.Root, "InvoiceReferencedDocument"));
        candidates.Add(FindFirstValueByLocalName(doc.Root, "BuyerOrderReferencedDocument"));
        candidates.Add(FindFirstValueByLocalName(doc.Root, "IssuerAssignedID"));
        candidates.Add(FindFirstValueByLocalName(doc.Root, "ID"));

        foreach (var candidate in candidates)
        {
            var normalized = NormalizeInvoiceNumberCandidate(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static DateTime ExtractInvoiceDateFromXml(XDocument doc)
    {
        var exchangedDocument = FindFirstByLocalName(doc.Root, "ExchangedDocument");
        var issueDateTime = FindFirstByLocalName(exchangedDocument, "IssueDateTime")
            ?? FindFirstByLocalName(doc.Root, "IssueDateTime");
        var value = FindChildValueByLocalName(issueDateTime, "DateTimeString");
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.Today;
        }

        return DateTime.TryParseExact(value.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateTime.Today;
    }

    private static DateTime? ExtractPaymentDueDateFromXml(XDocument doc)
    {
        var settlement = FindFirstByLocalName(doc.Root, "ApplicableHeaderTradeSettlement");
        var paymentTerms = FindFirstByLocalName(settlement, "SpecifiedTradePaymentTerms")
            ?? FindFirstByLocalName(doc.Root, "SpecifiedTradePaymentTerms");
        var dueDate = FindFirstByLocalName(paymentTerms, "DueDateDateTime")
            ?? FindFirstByLocalName(doc.Root, "DueDateDateTime");
        var value = FindChildValueByLocalName(dueDate, "DateTimeString")
            ?? FindFirstValueByLocalName(paymentTerms, "DateTimeString");

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(trimmed, ["yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out date))
        {
            return date;
        }

        return null;
    }

    private static decimal ExtractInvoiceGrossTotalFromXml(XDocument doc)
    {
        var settlement = FindFirstByLocalName(doc.Root, "ApplicableHeaderTradeSettlement");
        var summation = FindFirstByLocalName(settlement, "SpecifiedTradeSettlementHeaderMonetarySummation")
            ?? FindFirstByLocalName(doc.Root, "SpecifiedTradeSettlementHeaderMonetarySummation");

        var candidates = new[]
        {
            FindChildValueByLocalName(summation, "DuePayableAmount"),
            FindChildValueByLocalName(summation, "GrandTotalAmount"),
            FindChildValueByLocalName(summation, "TaxInclusiveTotalAmount")
        };

        foreach (var candidate in candidates)
        {
            var value = ParseInvariant(candidate);
            if (value > 0m)
            {
                return value;
            }
        }

        var taxBasis = ParseInvariant(FindChildValueByLocalName(summation, "TaxBasisTotalAmount"));
        var taxTotal = ParseInvariant(FindChildValueByLocalName(summation, "TaxTotalAmount"));
        if (taxBasis > 0m || taxTotal > 0m)
        {
            return taxBasis + taxTotal;
        }

        return 0m;
    }

    private static string NormalizeInvoiceNumberCandidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var normalized = candidate.Trim();
        if (normalized.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalized.Contains("EN16931", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalized.Equals("Elektronische Rechnung nach EN16931", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalized.Length < 4)
        {
            return string.Empty;
        }

        return normalized;
    }

    private static XElement? FindFirstByLocalName(XContainer? container, string localName)
    {
        if (container == null)
        {
            return null;
        }

        return container.Descendants().FirstOrDefault(x => x.Name.LocalName == localName);
    }

    private static string? FindChildValueByLocalName(XElement? element, string localName)
    {
        return element?.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;
    }

    private static string? FindFirstValueByLocalName(XContainer? container, string localName)
    {
        return FindFirstByLocalName(container, localName)?.Value;
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
                GrossUnitPrice = grossPrice > 0m
                    ? PricingHelper.RoundUnitPrice(PricingHelper.NormalizeUnitPrice(grossPrice, basisQty))
                    : 0m,
                PriceBasisQuantity = basisQty,
                LineTotal = lineTotal > 0m ? lineTotal : PricingHelper.CalculateLineTotal(quantity, netPrice, metalSurcharge, basisQty),
                GrossLineTotal = grossPrice > 0m
                    ? PricingHelper.RoundCurrency(quantity * PricingHelper.NormalizeUnitPrice(grossPrice, basisQty))
                    : 0m
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

    private sealed class ImportedExpenseInvoice
    {
        public string FilePath { get; set; } = string.Empty;
        public string OriginalPdfFileName { get; set; } = string.Empty;
        public byte[] PdfBytes { get; set; } = [];
        public string ContentHash { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? PaymentDueDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string AccountingCategory { get; set; } = "MaterialAndGoods";
        public decimal InvoiceTotalAmount { get; set; }
        public decimal ShippingCostNet { get; set; }
        public decimal ShippingCostGross { get; set; }
        public List<ManualInvoiceLineInput> Lines { get; set; } = new();
    }
}
