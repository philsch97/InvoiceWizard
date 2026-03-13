using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class BillingExportPage : Page
{
    public BillingExportPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = CustomerCombo.SelectedItem as CustomerEntity;
        await LoadProjectsAsync(customer);
        await LoadCustomerDataAsync();
    }

    private async void ProjectFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadCustomerDataAsync();
    }

    private void AllocationsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AllocationsGrid.SelectedItems.Count > 0 && WorkEntriesGrid.SelectedItems.Count > 0)
        {
            WorkEntriesGrid.SelectedItems.Clear();
        }

        if (AllocationsGrid.SelectedItem is LineAllocationEntity allocation)
        {
            EditAllocationQtyText.Text = allocation.AllocatedQuantity.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
            CustomerInvoiceNumberText.Text = allocation.CustomerInvoiceNumber ?? string.Empty;
        }
    }

    private void WorkEntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkEntriesGrid.SelectedItems.Count > 0 && AllocationsGrid.SelectedItems.Count > 0)
        {
            AllocationsGrid.SelectedItems.Clear();
        }

        if (WorkEntriesGrid.SelectedItem is WorkTimeEntryEntity workEntry)
        {
            CustomerInvoiceNumberText.Text = workEntry.CustomerInvoiceNumber ?? string.Empty;
        }
    }

    private async void UpdateSelectedAllocation_Click(object sender, RoutedEventArgs e)
    {
        if (AllocationsGrid.SelectedItems.Count != 1 || AllocationsGrid.SelectedItem is not LineAllocationEntity selected)
        {
            SetStatus("Bitte genau eine Materialzuweisung markieren, um die Menge zu aendern.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseDecimal(EditAllocationQtyText.Text, out var newQuantity) || newQuantity <= 0m)
        {
            SetStatus("Bitte eine gueltige neue Menge eingeben.", StatusMessageType.Error);
            return;
        }

        try
        {
            await App.Api.UpdateAllocationQuantityAsync(selected.LineAllocationId, newQuantity);
            await LoadCustomerDataAsync();
            SetStatus($"Die Zuweisungsmenge fuer Artikel {selected.InvoiceLine.ArticleNumber} wurde auf {newQuantity:0.##} geaendert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Aenderung fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void MarkSelectedAsInvoiced_Click(object sender, RoutedEventArgs e)
    {
        var selectedAllocations = AllocationsGrid.SelectedItems.OfType<LineAllocationEntity>().ToList();
        var selectedWorkEntries = WorkEntriesGrid.SelectedItems.OfType<WorkTimeEntryEntity>().ToList();
        if (selectedAllocations.Count == 0 && selectedWorkEntries.Count == 0)
        {
            SetStatus("Bitte mindestens eine Material- oder Arbeitszeitposition markieren.", StatusMessageType.Warning);
            return;
        }

        var invoiceNumber = (CustomerInvoiceNumberText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            SetStatus("Bitte eine Kunden-Rechnungsnummer eingeben.", StatusMessageType.Error);
            return;
        }

        foreach (var allocation in selectedAllocations)
        {
            await App.Api.UpdateAllocationStatusAsync(allocation.LineAllocationId, invoiceNumber, true, false);
        }

        foreach (var workEntry in selectedWorkEntries)
        {
            await App.Api.UpdateWorkTimeStatusAsync(workEntry.WorkTimeEntryId, invoiceNumber, true, false);
        }

        await LoadCustomerDataAsync();
        SetStatus($"{selectedAllocations.Count} Materialposition(en) und {selectedWorkEntries.Count} Arbeitszeitposition(en) wurden der Kundenrechnung {invoiceNumber} zugeordnet.", StatusMessageType.Success);
    }

    private async void MarkSelectedAsPaid_Click(object sender, RoutedEventArgs e)
    {
        var selectedAllocations = AllocationsGrid.SelectedItems.OfType<LineAllocationEntity>().ToList();
        var selectedWorkEntries = WorkEntriesGrid.SelectedItems.OfType<WorkTimeEntryEntity>().ToList();
        if (selectedAllocations.Count == 0 && selectedWorkEntries.Count == 0)
        {
            SetStatus("Bitte mindestens eine Material- oder Arbeitszeitposition markieren.", StatusMessageType.Warning);
            return;
        }

        var invoiceNumber = (CustomerInvoiceNumberText.Text ?? string.Empty).Trim();
        foreach (var allocation in selectedAllocations)
        {
            await App.Api.UpdateAllocationStatusAsync(allocation.LineAllocationId, invoiceNumber, false, true);
        }

        foreach (var workEntry in selectedWorkEntries)
        {
            await App.Api.UpdateWorkTimeStatusAsync(workEntry.WorkTimeEntryId, invoiceNumber, false, true);
        }

        await LoadCustomerDataAsync();
        SetStatus($"{selectedAllocations.Count} Materialposition(en) und {selectedWorkEntries.Count} Arbeitszeitposition(en) wurden als bezahlt markiert.", StatusMessageType.Success);
    }

    private async void RemoveSelectedAllocations_Click(object sender, RoutedEventArgs e)
    {
        var selectedAllocations = AllocationsGrid.SelectedItems.OfType<LineAllocationEntity>().ToList();
        var selectedWorkEntries = WorkEntriesGrid.SelectedItems.OfType<WorkTimeEntryEntity>().ToList();
        if (selectedAllocations.Count == 0 && selectedWorkEntries.Count == 0)
        {
            SetStatus("Bitte mindestens eine Material- oder Arbeitszeitposition markieren.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Sollen {selectedAllocations.Count} Materialposition(en) und {selectedWorkEntries.Count} Arbeitszeitposition(en) wirklich entfernt werden?", "Auswahl entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var allocation in selectedAllocations)
        {
            await App.Api.DeleteAllocationAsync(allocation.LineAllocationId);
        }

        foreach (var workEntry in selectedWorkEntries)
        {
            await App.Api.DeleteWorkTimeAsync(workEntry.WorkTimeEntryId);
        }

        await LoadCustomerDataAsync();
        SetStatus($"{selectedAllocations.Count} Materialposition(en) und {selectedWorkEntries.Count} Arbeitszeitposition(en) wurden entfernt.", StatusMessageType.Success);
    }

    private async void ExportOpenItems_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseDecimal(ExportMarkupText.Text, out var markupPercent) || markupPercent < 0m)
        {
            SetStatus("Bitte einen gueltigen Export-Zuschlag eingeben.", StatusMessageType.Error);
            return;
        }

        if (!TryParseDecimal(SmallMaterialFlatFeeText.Text, out var smallMaterialFlatFee) || smallMaterialFlatFee < 0m)
        {
            SetStatus("Bitte eine gueltige Kleinmaterial-Pauschale eingeben.", StatusMessageType.Error);
            return;
        }

        var smallMaterialMode = GetSelectedSmallMaterialMode();
        var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var selectedProjectName = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.Name;

        var allocations = (await App.Api.GetAllocationsAsync(customer.CustomerId, selectedProjectId))
            .Where(a => string.IsNullOrWhiteSpace(a.CustomerInvoiceNumber))
            .OrderBy(a => a.InvoiceLine.Invoice.InvoiceDate)
            .ThenBy(a => a.InvoiceLine.Position)
            .ToList();
        var workEntries = (await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId))
            .Where(w => string.IsNullOrWhiteSpace(w.CustomerInvoiceNumber))
            .OrderBy(w => w.WorkDate)
            .ThenBy(w => w.StartTime)
            .ToList();

        if (allocations.Count == 0 && workEntries.Count == 0)
        {
            SetStatus("Fuer diese Auswahl gibt es keine offenen Positionen fuer den Export.", StatusMessageType.Warning);
            return;
        }

        var projectSuffix = selectedProjectId.HasValue ? $"_{selectedProjectName}" : string.Empty;
        var saveDialog = new SaveFileDialog
        {
            Filter = "Excel Arbeitsmappe (*.xlsx)|*.xlsx",
            FileName = $"{customer.Name}{projectSuffix}_offene_positionen_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        var rows = new List<ExcelExportService.ExportRow>();
        var regularAllocations = allocations.Where(a => !a.IsSmallMaterial).ToList();
        var smallMaterialAllocations = allocations.Where(a => a.IsSmallMaterial).ToList();

        foreach (var allocation in regularAllocations)
        {
            AddNormalAllocationRow(rows, allocation, markupPercent);
            await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
        }

        if (smallMaterialFlatFee > 0m)
        {
            ApplySmallMaterialFlatFee(rows, smallMaterialAllocations, smallMaterialFlatFee, selectedProjectName);
            foreach (var allocation in smallMaterialAllocations)
            {
                await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
            }
        }
        else
        {
            switch (smallMaterialMode)
            {
                case "Einzeln berechnen":
                    AddSmallMaterialRowsIndividually(rows, smallMaterialAllocations, markupPercent);
                    foreach (var allocation in smallMaterialAllocations)
                    {
                        await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
                    }
                    break;
                case "Als Sammelposition":
                    AddSmallMaterialRowsGrouped(rows, smallMaterialAllocations, markupPercent);
                    foreach (var allocation in smallMaterialAllocations)
                    {
                        await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
                    }
                    break;
                case "Nicht berechnen":
                    foreach (var allocation in smallMaterialAllocations)
                    {
                        allocation.ExportedMarkupPercent = 0m;
                        allocation.ExportedUnitPrice = 0m;
                        allocation.ExportedLineTotal = 0m;
                        allocation.LastExportedAt = DateTime.Now;
                        await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, 0m, 0m, 0m);
                    }
                    break;
            }
        }

        foreach (var workEntry in workEntries)
        {
            var projectLabel = workEntry.Project?.Name ?? "Ohne Projekt";
            workEntry.ExportedLineTotal = workEntry.CalculatedLineTotal;
            workEntry.ExportedUnitPrice = workEntry.HoursWorked > 0m ? workEntry.ExportedLineTotal / workEntry.HoursWorked : workEntry.ExportedLineTotal;
            workEntry.LastExportedAt = DateTime.Now;
            rows.Add(new ExcelExportService.ExportRow
            {
                SupplierInvoiceNumber = "Arbeitszeit",
                ArticleNumber = "ZEIT",
                Ean = string.Empty,
                Description = workEntry.TravelKilometers > 0m
                    ? $"[{projectLabel}] {workEntry.Description} ({workEntry.WorkDate:dd.MM.yyyy}, {workEntry.TimeRange}, {workEntry.TravelKilometers:0.##} km Anfahrt)"
                    : $"[{projectLabel}] {workEntry.Description} ({workEntry.WorkDate:dd.MM.yyyy}, {workEntry.TimeRange})",
                Quantity = workEntry.HoursWorked,
                Unit = "h",
                PurchaseUnitPrice = 0m,
                MarkupPercent = 0m,
                SalesUnitPrice = workEntry.ExportedUnitPrice,
                Total = workEntry.ExportedLineTotal
            });
            await App.Api.UpdateWorkTimeExportAsync(workEntry.WorkTimeEntryId, workEntry.ExportedUnitPrice, workEntry.ExportedLineTotal);
        }

        ExcelExportService.ExportOpenItems(saveDialog.FileName, customer.Name, rows);
        SetStatus($"Excel-Datei erstellt und Einnahmen gespeichert: {saveDialog.FileName}", StatusMessageType.Success);
        await LoadCustomerDataAsync();
    }

    private void AddNormalAllocationRow(List<ExcelExportService.ExportRow> rows, LineAllocationEntity allocation, decimal markupPercent)
    {
        var projectLabel = allocation.Project?.Name ?? "Ohne Projekt";
        var purchaseUnitPrice = GetPurchaseUnitPrice(allocation);
        var salesUnitPrice = PricingHelper.ApplyMarkup(purchaseUnitPrice, markupPercent);
        allocation.ExportedMarkupPercent = markupPercent;
        allocation.ExportedUnitPrice = salesUnitPrice;
        allocation.ExportedLineTotal = salesUnitPrice * allocation.AllocatedQuantity;
        allocation.LastExportedAt = DateTime.Now;
        rows.Add(new ExcelExportService.ExportRow
        {
            SupplierInvoiceNumber = allocation.InvoiceLine.Invoice.InvoiceNumber,
            ArticleNumber = allocation.InvoiceLine.ArticleNumber,
            Ean = allocation.InvoiceLine.Ean,
            Description = $"[{projectLabel}] {allocation.InvoiceLine.Description}",
            Quantity = allocation.AllocatedQuantity,
            Unit = allocation.InvoiceLine.Unit,
            PurchaseUnitPrice = purchaseUnitPrice,
            MarkupPercent = markupPercent,
            SalesUnitPrice = salesUnitPrice,
            Total = allocation.ExportedLineTotal
        });
    }

    private void AddSmallMaterialRowsIndividually(List<ExcelExportService.ExportRow> rows, IEnumerable<LineAllocationEntity> allocations, decimal markupPercent)
    {
        foreach (var allocation in allocations)
        {
            var projectLabel = allocation.Project?.Name ?? "Ohne Projekt";
            var purchaseUnitPrice = GetPurchaseUnitPrice(allocation);
            var salesUnitPrice = PricingHelper.ApplyMarkup(purchaseUnitPrice, markupPercent);
            allocation.ExportedMarkupPercent = markupPercent;
            allocation.ExportedUnitPrice = salesUnitPrice;
            allocation.ExportedLineTotal = salesUnitPrice * allocation.AllocatedQuantity;
            allocation.LastExportedAt = DateTime.Now;
            rows.Add(new ExcelExportService.ExportRow
            {
                SupplierInvoiceNumber = allocation.InvoiceLine.Invoice.InvoiceNumber,
                ArticleNumber = string.IsNullOrWhiteSpace(allocation.InvoiceLine.ArticleNumber) ? "KLEINMAT" : allocation.InvoiceLine.ArticleNumber,
                Ean = allocation.InvoiceLine.Ean,
                Description = $"[{projectLabel}] Kleinmaterial: {allocation.InvoiceLine.Description}",
                Quantity = allocation.AllocatedQuantity,
                Unit = allocation.InvoiceLine.Unit,
                PurchaseUnitPrice = purchaseUnitPrice,
                MarkupPercent = markupPercent,
                SalesUnitPrice = salesUnitPrice,
                Total = allocation.ExportedLineTotal
            });
        }
    }

    private void AddSmallMaterialRowsGrouped(List<ExcelExportService.ExportRow> rows, IEnumerable<LineAllocationEntity> allocations, decimal markupPercent)
    {
        foreach (var group in allocations.GroupBy(a => a.Project?.Name ?? "Ohne Projekt"))
        {
            decimal purchaseTotal = 0m;
            decimal salesTotal = 0m;
            foreach (var allocation in group)
            {
                var purchaseUnitPrice = GetPurchaseUnitPrice(allocation);
                var salesUnitPrice = PricingHelper.ApplyMarkup(purchaseUnitPrice, markupPercent);
                var lineTotal = salesUnitPrice * allocation.AllocatedQuantity;
                allocation.ExportedMarkupPercent = markupPercent;
                allocation.ExportedUnitPrice = salesUnitPrice;
                allocation.ExportedLineTotal = lineTotal;
                allocation.LastExportedAt = DateTime.Now;
                purchaseTotal += purchaseUnitPrice * allocation.AllocatedQuantity;
                salesTotal += lineTotal;
            }

            rows.Add(new ExcelExportService.ExportRow
            {
                SupplierInvoiceNumber = "Kleinmaterial",
                ArticleNumber = "KLEINMAT",
                Ean = string.Empty,
                Description = $"[{group.Key}] Kleinmaterial laut Nachweis",
                Quantity = 1m,
                Unit = "Pauschale",
                PurchaseUnitPrice = purchaseTotal,
                MarkupPercent = markupPercent,
                SalesUnitPrice = salesTotal,
                Total = salesTotal
            });
        }
    }

    private void ApplySmallMaterialFlatFee(List<ExcelExportService.ExportRow> rows, IReadOnlyList<LineAllocationEntity> allocations, decimal flatFee, string? selectedProjectName)
    {
        if (allocations.Count == 0)
        {
            return;
        }

        var totalPurchase = allocations.Sum(a => GetPurchaseUnitPrice(a) * a.AllocatedQuantity);
        var equalShare = flatFee / allocations.Count;
        foreach (var allocation in allocations)
        {
            decimal lineTotal = totalPurchase > 0m ? flatFee * ((GetPurchaseUnitPrice(allocation) * allocation.AllocatedQuantity) / totalPurchase) : equalShare;
            allocation.ExportedMarkupPercent = 0m;
            allocation.ExportedLineTotal = lineTotal;
            allocation.ExportedUnitPrice = allocation.AllocatedQuantity > 0m ? lineTotal / allocation.AllocatedQuantity : 0m;
            allocation.LastExportedAt = DateTime.Now;
        }

        var label = selectedProjectName;
        if (string.IsNullOrWhiteSpace(label))
        {
            var projectNames = allocations.Select(a => a.Project?.Name ?? "Ohne Projekt").Distinct().ToList();
            label = projectNames.Count == 1 ? projectNames[0] : "Mehrere Projekte";
        }

        rows.Add(new ExcelExportService.ExportRow
        {
            SupplierInvoiceNumber = "Kleinmaterial",
            ArticleNumber = "KM-PAUSCH",
            Ean = string.Empty,
            Description = $"[{label}] Kleinmaterial-Pauschale",
            Quantity = 1m,
            Unit = "Pauschale",
            PurchaseUnitPrice = totalPurchase,
            MarkupPercent = 0m,
            SalesUnitPrice = flatFee,
            Total = flatFee
        });
    }

    private decimal GetPurchaseUnitPrice(LineAllocationEntity allocation)
    {
        return allocation.CustomerUnitPrice > 0m ? allocation.CustomerUnitPrice : PricingHelper.NormalizeUnitPrice(allocation.InvoiceLine.NetUnitPrice, allocation.InvoiceLine.MetalSurcharge, allocation.InvoiceLine.PriceBasisQuantity);
    }

    private string GetSelectedSmallMaterialMode()
    {
        return (SmallMaterialModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Als Sammelposition";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCustomersAsync(CustomerCombo.SelectedItem is CustomerEntity customer ? customer.Name : null, (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId);
    }

    private async Task LoadCustomersAsync(string? selectCustomerName = null, int? selectedProjectId = null)
    {
        var selectedCustomerId = CustomerCombo.SelectedItem is CustomerEntity selectedCustomer ? selectedCustomer.CustomerId : (int?)null;
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectFilterCombo.ItemsSource = null;
            ProjectFilterCombo.SelectedItem = null;
            AllocationsGrid.ItemsSource = null;
            WorkEntriesGrid.ItemsSource = null;
            EditAllocationQtyText.Clear();
            CustomerInvoiceNumberText.Clear();
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Info);
            return;
        }

        CustomerEntity? customerToSelect = null;
        if (!string.IsNullOrWhiteSpace(selectCustomerName))
        {
            customerToSelect = customers.FirstOrDefault(c => c.Name == selectCustomerName);
        }

        customerToSelect ??= selectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value) : null;
        customerToSelect ??= customers[0];
        CustomerCombo.SelectedItem = customerToSelect;
        if (customerToSelect is not null)
        {
            await LoadProjectsAsync(customerToSelect, null, selectedProjectId);
            await LoadCustomerDataAsync();
        }
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer, string? selectProjectName = null, int? selectedProjectId = null)
    {
        if (customer == null)
        {
            ProjectFilterCombo.ItemsSource = null;
            ProjectFilterCombo.SelectedItem = null;
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: true);
        ProjectFilterCombo.ItemsSource = projects;
        ProjectSelectionItem? projectToSelect = null;
        if (!string.IsNullOrWhiteSpace(selectProjectName))
        {
            projectToSelect = projects.FirstOrDefault(p => p.Name == selectProjectName);
        }

        projectToSelect ??= selectedProjectId.HasValue ? projects.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) : null;
        ProjectFilterCombo.SelectedItem = projectToSelect ?? projects[0];
    }

    private async Task LoadCustomerDataAsync()
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            AllocationsGrid.ItemsSource = null;
            WorkEntriesGrid.ItemsSource = null;
            EditAllocationQtyText.Clear();
            CustomerInvoiceNumberText.Clear();
            SetStatus("Kein Kunde ausgewaehlt.", StatusMessageType.Info);
            return;
        }

        var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        ExportMarkupText.Text = customer.DefaultMarkupPercent.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

        var allocations = await App.Api.GetAllocationsAsync(customer.CustomerId, selectedProjectId);
        var workEntries = await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId);
        allocations = allocations.OrderByDescending(a => a.AllocatedAt).ToList();
        workEntries = workEntries.OrderByDescending(w => w.WorkDate).ThenByDescending(w => w.StartTime).ToList();

        AllocationsGrid.ItemsSource = allocations;
        WorkEntriesGrid.ItemsSource = workEntries;
        if (allocations.Count == 0)
        {
            EditAllocationQtyText.Clear();
        }

        if (allocations.Count == 0 && workEntries.Count == 0)
        {
            CustomerInvoiceNumberText.Clear();
            SetStatus("Keine Material- oder Arbeitszeitpositionen fuer die ausgewaehlte Kunden-/Projektansicht gefunden.", StatusMessageType.Info);
            return;
        }

        var smallMaterialCount = allocations.Count(a => a.IsSmallMaterial);
        SetStatus($"{allocations.Count} Materialposition(en), davon {smallMaterialCount} Kleinmaterial, und {workEntries.Count} Arbeitszeitposition(en) geladen.", StatusMessageType.Info);
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
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

