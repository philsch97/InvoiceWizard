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
    private readonly ObservableCollection<InvoiceEntity> _storedInvoices = new();

    public InvoiceArchivePage()
    {
        InitializeComponent();
        InvoicesGrid.ItemsSource = _storedInvoices;
        Loaded += async (_, _) =>
        {
            await LoadStoredInvoicesAsync();
            SetStatus("Rechnungsarchiv geladen.", StatusMessageType.Info);
        };
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
                ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation
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
                    GrossListPrice = line.GrossListPrice,
                    GrossUnitPrice = line.GrossUnitPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity,
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
                ApplySmallBusinessRegulation = detail.ApplySmallBusinessRegulation
            };

            var dialog = new GenerateInvoiceDialog(detail.InvoiceNumber, customer.CustomerNumber, customer.Name, false, options)
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
                    GrossListPrice = line.GrossListPrice,
                    GrossUnitPrice = line.GrossUnitPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity,
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
}
