using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
using InvoiceWizard.Services;
using Microsoft.Win32;

namespace InvoiceWizard;

public partial class OfferPage : Page
{
    private readonly ObservableCollection<ManualInvoiceLineInput> _lines = new();

    public OfferPage()
    {
        InitializeComponent();
        LinesGrid.ItemsSource = _lines;
        Loaded += async (_, _) => await LoadCustomersAsync();
        UpdateSummary();
        SetStatus("Bereit für die Angebotserstellung.", StatusMessageType.Info);
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = CustomerCombo.SelectedItem as CustomerEntity;
        App.SetSelectedCustomer(customer?.CustomerId);
        await LoadProjectsAsync(customer);
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Warning);
            return;
        }

        var customer = App.SelectedCustomerId.HasValue
            ? customers.FirstOrDefault(c => c.CustomerId == App.SelectedCustomerId.Value) ?? customers[0]
            : customers[0];
        CustomerCombo.SelectedItem = customer;
        App.SetSelectedCustomer(customer.CustomerId);
        await LoadProjectsAsync(customer);
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer)
    {
        if (customer == null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: true);
        ProjectCombo.ItemsSource = projects;
        ProjectCombo.SelectedItem = projects.FirstOrDefault();
    }

    private async void AddFromDatanorm_Click(object sender, RoutedEventArgs e)
    {
        var state = await App.DatanormCatalog.GetStateAsync();
        if (state.ArticleCount == 0)
        {
            SetStatus("Bitte zuerst auf der Sonepar-Seite eine DATANORM-Datei importieren.", StatusMessageType.Warning);
            return;
        }

        var dialog = new DatanormSearchDialog([])
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var line = CloneLine(dialog.Result);
        line.Position = _lines.Count + 1;
        _lines.Add(line);
        RenumberLines();
        UpdateSummary();
        SetStatus($"DATANORM-Artikel {line.ArticleNumber} zum Angebot hinzugefügt.", StatusMessageType.Success);
    }

    private void EditLines_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ManualInvoiceLinesDialog(_lines, requireVatPerLine: false)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _lines.Clear();
        foreach (var line in dialog.ResultLines)
        {
            _lines.Add(CloneLine(line));
        }
        RenumberLines();
        UpdateSummary();
        SetStatus($"{_lines.Count} Angebotsposition(en) aktualisiert.", StatusMessageType.Success);
    }

    private void RemoveSelectedLines_Click(object sender, RoutedEventArgs e)
    {
        var selected = LinesGrid.SelectedItems.OfType<ManualInvoiceLineInput>().ToList();
        if (selected.Count == 0)
        {
            SetStatus("Bitte zuerst mindestens eine Angebotsposition markieren.", StatusMessageType.Warning);
            return;
        }

        foreach (var item in selected)
        {
            _lines.Remove(item);
        }

        RenumberLines();
        UpdateSummary();
        SetStatus($"{selected.Count} Angebotsposition(en) entfernt.", StatusMessageType.Success);
    }

    private async void GenerateOffer_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswählen.", StatusMessageType.Warning);
            return;
        }

        if (_lines.Count == 0)
        {
            SetStatus("Bitte zuerst mindestens eine Angebotsposition anlegen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var company = await App.Api.GetCompanyProfileAsync();
            if (string.IsNullOrWhiteSpace(company.CompanyName))
            {
                SetStatus("Bitte zuerst unter Admin > Firmendaten die Firmendaten pflegen.", StatusMessageType.Warning);
                return;
            }

            var dialog = new GenerateOfferDialog(BuildOfferNumber(customer), customer.CustomerNumber, customer.Name, customer.DefaultMarkupPercent)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            var generatedLines = BuildOfferLines(dialog.Result.MarkupPercent, dialog.Result.ApplySmallBusinessRegulation);
            var pdfBytes = OfferPdfService.Create(new OfferPdfService.OfferDocument
            {
                Company = company,
                Customer = customer,
                OfferNumber = dialog.Result.OfferNumber,
                CustomerNumber = customer.CustomerNumber,
                OfferDate = dialog.Result.OfferDate,
                ValidUntilDate = dialog.Result.ValidUntilDate,
                Subject = dialog.Result.Subject,
                ApplySmallBusinessRegulation = dialog.Result.ApplySmallBusinessRegulation,
                Lines = generatedLines
            });

            var selectedProjectName = (ProjectCombo.SelectedItem as ProjectSelectionItem)?.ProjectId is int
                ? (ProjectCombo.SelectedItem as ProjectSelectionItem)?.Name
                : null;
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF-Datei (*.pdf)|*.pdf",
                FileName = $"{dialog.Result.OfferNumber}_{SanitizeFileName(customer.Name)}{(string.IsNullOrWhiteSpace(selectedProjectName) ? string.Empty : "_" + SanitizeFileName(selectedProjectName))}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            await File.WriteAllBytesAsync(saveDialog.FileName, pdfBytes);
            SetStatus($"Angebot {dialog.Result.OfferNumber} wurde als PDF erzeugt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Angebot konnte nicht erzeugt werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private List<OfferPdfService.OfferLine> BuildOfferLines(decimal markupPercent, bool applySmallBusinessRegulation)
    {
        return _lines
            .OrderBy(x => x.Position)
            .Select((line, index) =>
            {
                var purchaseUnitPrice = line.Quantity > 0m && line.LineTotal > 0m
                    ? PricingHelper.RoundUnitPrice(line.LineTotal / line.Quantity)
                    : PricingHelper.NormalizeUnitPrice(line.NetUnitPrice, line.MetalSurcharge, line.PriceBasisQuantity);
                var salesUnitPrice = PricingHelper.CalculateRevenueUnitPrice(purchaseUnitPrice, markupPercent, applySmallBusinessRegulation);
                return new OfferPdfService.OfferLine
                {
                    Position = index + 1,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = string.IsNullOrWhiteSpace(line.Unit) ? "ST" : line.Unit,
                    UnitPrice = salesUnitPrice,
                    LineTotal = PricingHelper.RoundCurrency(salesUnitPrice * line.Quantity)
                };
            })
            .ToList();
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].Position = i + 1;
        }

        LinesGrid.Items.Refresh();
    }

    private void UpdateSummary()
    {
        SummaryText.Text = _lines.Count == 0
            ? "Noch keine Angebotspositionen vorhanden."
            : $"{_lines.Count} Angebotsposition(en) vorbereitet. Die DATANORM-Preise werden beim PDF-Export mit dem eingestellten Materialaufschlag in einen Angebotspreis umgerechnet.";
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

    private static string BuildOfferNumber(CustomerEntity customer)
    {
        var prefix = string.IsNullOrWhiteSpace(customer.CustomerNumber) ? "ANG" : $"ANG-{customer.CustomerNumber}";
        return $"{prefix}-{DateTime.Now:yyyyMMddHHmm}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
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
}
