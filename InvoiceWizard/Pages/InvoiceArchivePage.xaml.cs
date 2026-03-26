using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
using InvoiceWizard.Services;
using Microsoft.Win32;

namespace InvoiceWizard;

public partial class InvoiceArchivePage : Page
{
    private readonly ObservableCollection<InvoiceEntity> _filteredInvoices = new();
    private readonly ObservableCollection<InvoiceEntity> _storedInvoices = new();

    public InvoiceArchivePage()
    {
        InitializeComponent();
        InvoicesGrid.ItemsSource = _filteredInvoices;
        Loaded += async (_, _) =>
        {
            await LoadFiltersAndInvoicesAsync();
            SetStatus("Rechnungsarchiv geladen.", StatusMessageType.Info);
        };
    }

    private async Task LoadFiltersAndInvoicesAsync()
    {
        await LoadStoredInvoicesAsync();
        await LoadCustomerFiltersAsync();
        ApplyFilters();
    }

    private async Task LoadStoredInvoicesAsync()
    {
        _storedInvoices.Clear();
        foreach (var item in await App.Api.GetInvoicesAsync())
        {
            _storedInvoices.Add(item);
        }

        ApplyFilters();
    }

    private async Task LoadCustomerFiltersAsync()
    {
        InvoiceDirectionFilterCombo.ItemsSource = new[]
        {
            new ArchiveDirectionFilterItem { Value = string.Empty, Label = "Alle Rechnungsformen" },
            new ArchiveDirectionFilterItem { Value = "Expense", Label = "Ausgaberechnung" },
            new ArchiveDirectionFilterItem { Value = "ExpenseReduction", Label = "Ausgabenminderung" },
            new ArchiveDirectionFilterItem { Value = "Revenue", Label = "Einnahmerechnung" },
            new ArchiveDirectionFilterItem { Value = "RevenueReduction", Label = "Einnahmenminderung" }
        };
        InvoiceDirectionFilterCombo.SelectedIndex = 0;

        AccountingCategoryFilterCombo.ItemsSource = new[]
        {
            new ArchiveAccountingCategoryFilterItem { Value = string.Empty, Label = "Alle Kategorien" },
            new ArchiveAccountingCategoryFilterItem { Value = "MaterialAndGoods", Label = "Material und Waren" },
            new ArchiveAccountingCategoryFilterItem { Value = "Tools", Label = "Werkzeug" },
            new ArchiveAccountingCategoryFilterItem { Value = "Services", Label = "Dienstleistungen" },
            new ArchiveAccountingCategoryFilterItem { Value = "Office", Label = "Buero" },
            new ArchiveAccountingCategoryFilterItem { Value = "Vehicle", Label = "Fahrzeug" },
            new ArchiveAccountingCategoryFilterItem { Value = "Other", Label = "Sonstiges" }
        };
        AccountingCategoryFilterCombo.SelectedIndex = 0;

        var customers = new List<CustomerFilterItem> { new() { CustomerId = null, Name = "Alle Kunden" } };
        customers.AddRange((await App.Api.GetCustomersAsync())
            .OrderBy(x => x.Name)
            .Select(x => new CustomerFilterItem { CustomerId = x.CustomerId, Name = x.Name }));
        CustomerFilterCombo.ItemsSource = customers;
        CustomerFilterCombo.SelectedIndex = 0;

        await UpdateProjectFilterItemsAsync();
    }

    private void ArchiveFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyFilters();
    }

    private void ArchiveDateFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyFilters();
    }

    private async void CustomerFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await UpdateProjectFilterItemsAsync();
        ApplyFilters();
    }

    private async void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        InvoiceDirectionFilterCombo.SelectedIndex = 0;
        AccountingCategoryFilterCombo.SelectedIndex = 0;
        DateFromFilterPicker.SelectedDate = null;
        DateToFilterPicker.SelectedDate = null;
        CustomerFilterCombo.SelectedIndex = 0;
        await UpdateProjectFilterItemsAsync();
        ProjectFilterCombo.SelectedIndex = 0;
        ApplyFilters();
    }

    private async Task UpdateProjectFilterItemsAsync()
    {
        var selectedCustomerId = (CustomerFilterCombo.SelectedItem as CustomerFilterItem)?.CustomerId;
        var items = new List<ProjectSelectionItem> { new() { ProjectId = null, Name = "Alle Projekte", ProjectStatus = "Active" } };
        if (selectedCustomerId.HasValue)
        {
            items.AddRange(await App.Api.GetProjectSelectionsAsync(selectedCustomerId.Value, includeAll: false, includeInactive: true));
        }

        var currentProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        ProjectFilterCombo.ItemsSource = items;
        ProjectFilterCombo.SelectedItem = currentProjectId.HasValue
            ? items.FirstOrDefault(x => x.ProjectId == currentProjectId.Value) ?? items[0]
            : items[0];
    }

    private void ApplyFilters()
    {
        if (InvoiceDirectionFilterCombo is null || AccountingCategoryFilterCombo is null || CustomerFilterCombo is null || ProjectFilterCombo is null)
        {
            return;
        }

        var selectedDirection = (InvoiceDirectionFilterCombo.SelectedItem as ArchiveDirectionFilterItem)?.Value;
        var selectedAccountingCategory = (AccountingCategoryFilterCombo.SelectedItem as ArchiveAccountingCategoryFilterItem)?.Value;
        var selectedCustomerId = (CustomerFilterCombo.SelectedItem as CustomerFilterItem)?.CustomerId;
        var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var dateFrom = DateFromFilterPicker.SelectedDate?.Date;
        var dateTo = DateToFilterPicker.SelectedDate?.Date;

        var filtered = _storedInvoices
            .Where(x => string.IsNullOrWhiteSpace(selectedDirection) || string.Equals(x.InvoiceDirection, selectedDirection, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(selectedAccountingCategory) || string.Equals(x.AccountingCategory, selectedAccountingCategory, StringComparison.OrdinalIgnoreCase))
            .Where(x => !dateFrom.HasValue || x.InvoiceDate.Date >= dateFrom.Value)
            .Where(x => !dateTo.HasValue || x.InvoiceDate.Date <= dateTo.Value)
            .Where(x => !selectedCustomerId.HasValue || x.CustomerId == selectedCustomerId.Value || x.RelatedCustomerIds.Contains(selectedCustomerId.Value))
            .Where(x => !selectedProjectId.HasValue || x.RelatedProjectIds.Contains(selectedProjectId.Value))
            .ToList();

        _filteredInvoices.Clear();
        foreach (var item in filtered)
        {
            _filteredInvoices.Add(item);
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

            var options = new GeneratedInvoiceOptions
            {
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = finalizationDate,
                DeliveryDate = detail.DeliveryDate ?? detail.InvoiceDate,
                Subject = detail.Subject,
                ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation,
                InvoiceDirection = detail.InvoiceDirection
            };

            var lines = detail.Lines
                .OrderBy(x => x.Position)
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
                    AccountingCategory = line.AccountingCategory,
                    GrossListPrice = line.GrossListPrice,
                    GrossUnitPrice = line.GrossUnitPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity,
                    ShippingNetShare = line.ShippingNetShare,
                    ShippingGrossShare = line.ShippingGrossShare,
                    GrossLineTotal = line.GrossLineTotal
                })
                .ToList();

            var dialog = new DraftInvoiceEditorDialog(detail.InvoiceNumber, customer.CustomerNumber, customer.Name, options, lines)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            lines = dialog.ResultLines;
            UpdateRevenueLineGrossAmounts(lines, dialog.Result.ApplySmallBusinessRegulation);

            var pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
            {
                Company = company,
                Customer = customer,
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = finalizationDate,
                DeliveryDate = dialog.Result.DeliveryDate.Date,
                Subject = dialog.Result.Subject,
                InvoiceDirection = dialog.Result.InvoiceDirection,
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

            await App.Api.UpdateInvoiceAsync(
                detail.InvoiceId,
                dialog.Result.InvoiceDirection,
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
                detail.SourcePdfPath,
                detail.OriginalPdfFileName,
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

            var options = new GeneratedInvoiceOptions
            {
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = finalizationDate,
                DeliveryDate = detail.DeliveryDate ?? detail.InvoiceDate,
                Subject = detail.Subject,
                ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation,
                InvoiceDirection = detail.InvoiceDirection
            };

            var dialog = new GenerateInvoiceDialog(detail.InvoiceNumber, detail.InvoiceNumber, customer.CustomerNumber, customer.Name, false, options)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF-Datei (*.pdf)|*.pdf",
                FileName = $"{dialog.Result.InvoiceNumber}_{SanitizeFileName(customer.Name)}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var lines = detail.Lines
                .OrderBy(x => x.Position)
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
                    AccountingCategory = line.AccountingCategory,
                    GrossListPrice = line.GrossListPrice,
                    GrossUnitPrice = line.GrossUnitPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity,
                    ShippingNetShare = line.ShippingNetShare,
                    ShippingGrossShare = line.ShippingGrossShare,
                    GrossLineTotal = line.GrossLineTotal
                })
                .ToList();
            UpdateRevenueLineGrossAmounts(lines, dialog.Result.ApplySmallBusinessRegulation);

            var pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
            {
                Company = company,
                Customer = customer,
                InvoiceNumber = detail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = finalizationDate,
                DeliveryDate = dialog.Result.DeliveryDate.Date,
                Subject = dialog.Result.Subject,
                InvoiceDirection = dialog.Result.InvoiceDirection,
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
                dialog.Result.InvoiceDirection,
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

    private async void DeleteInvoices_Click(object sender, RoutedEventArgs e)
    {
        var invoices = InvoicesGrid.SelectedItems.OfType<InvoiceEntity>().Where(x => x.CanDelete).ToList();
        if (invoices.Count == 0)
        {
            SetStatus("Bitte zuerst einen Entwurf oder eine Ausgaberechnung im Archiv auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var containsExpenseInvoices = invoices.Any(x => string.Equals(x.InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase));
        var containsDrafts = invoices.Any(x => x.IsDraft && string.Equals(x.InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase));
        var confirmationText = invoices.Count == 1
            ? BuildDeleteConfirmationText(invoices[0])
            : BuildDeleteConfirmationTextForMultiple(invoices.Count, containsExpenseInvoices, containsDrafts);

        if (MessageBox.Show(confirmationText, "Rechnung loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var invoice in invoices)
            {
                await App.Api.DeleteInvoiceAsync(invoice.InvoiceId);
            }

            await LoadStoredInvoicesAsync();
            SetStatus(invoices.Count == 1
                ? "Rechnung wurde geloescht."
                : $"{invoices.Count} Rechnung(en) wurden geloescht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Rechnung konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
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

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

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

    private static void UpdateRevenueLineGrossAmounts(IEnumerable<ManualInvoiceLineInput> lines, bool applySmallBusinessRegulation)
    {
        foreach (var line in lines)
        {
            line.GrossLineTotal = PricingHelper.CalculateRevenueGrossTotal(line.LineTotal, applySmallBusinessRegulation);
            line.GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(line.GrossLineTotal, line.Quantity);
        }
    }

    private static string BuildDeleteConfirmationText(InvoiceEntity invoice)
    {
        if (string.Equals(invoice.InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase))
        {
            return $"Soll die Ausgaberechnung {invoice.DisplayNumber} wirklich geloescht werden?\n\nDabei werden die importierten Positionen dieser Rechnung ebenfalls geloescht. Eventuelle Projektzuweisungen zu diesen Positionen werden mit entfernt.";
        }

        return $"Soll der Entwurf {invoice.DisplayNumber} wirklich geloescht werden?\n\nDie Entwurfspositionen dieser Rechnung werden dabei ebenfalls geloescht.";
    }

    private static string BuildDeleteConfirmationTextForMultiple(int count, bool containsExpenseInvoices, bool containsDrafts)
    {
        if (containsExpenseInvoices && containsDrafts)
        {
            return $"Sollen {count} markierte Rechnungen wirklich geloescht werden?\n\nAusgaberechnungen werden mit ihren importierten Positionen und eventuellen Projektzuweisungen geloescht. Bei Entwuerfen werden die Entwurfspositionen geloescht.";
        }

        if (containsExpenseInvoices)
        {
            return $"Sollen {count} markierte Ausgaberechnungen wirklich geloescht werden?\n\nDie importierten Positionen dieser Rechnungen werden ebenfalls geloescht. Eventuelle Projektzuweisungen zu diesen Positionen werden mit entfernt.";
        }

        return $"Sollen {count} markierte Entwuerfe wirklich geloescht werden?\n\nDie Entwurfspositionen dieser Rechnungen werden dabei ebenfalls geloescht.";
    }
}

public sealed class ArchiveDirectionFilterItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class CustomerFilterItem
{
    public int? CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ArchiveAccountingCategoryFilterItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
