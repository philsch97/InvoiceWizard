using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
using InvoiceWizard.Services;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class BillingExportPage : Page
{
    private decimal _lastMarkupPercent;
    private string _lastSmallMaterialMode = "Als Sammelposition";
    private decimal _lastSmallMaterialFlatFee;

    public BillingExportPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = CustomerCombo.SelectedItem as CustomerEntity;
        App.SetSelectedCustomer(customer?.CustomerId);
        await LoadProjectsAsync(customer);
        await LoadCustomerDataAsync();
    }

    private async void ProjectFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadCustomerDataAsync();
    }

    private async void ShowLinkedItemsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadCustomerDataAsync();
    }

    private void AllocationsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AllocationsGrid.SelectedItem is LineAllocationEntity allocation)
        {
            EditAllocationQtyText.Text = allocation.AllocatedQuantity.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        }
    }

    private void WorkEntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
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

        var hasAssignedSmallMaterial = allocations.Any(a => a.IsSmallMaterial);
        if (!TryGetBillingOptions(hasAssignedSmallMaterial, out var billingOptions))
        {
            return;
        }

        var markupPercent = billingOptions.MarkupPercent;
        var smallMaterialFlatFee = billingOptions.SmallMaterialFlatFee;
        var smallMaterialMode = billingOptions.SmallMaterialMode;

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

    private async void GenerateInvoice_Click(object sender, RoutedEventArgs e)
    {
        await CreateRevenueInvoiceAsync(saveAsDraft: false);
    }

    private async void SaveDraftInvoice_Click(object sender, RoutedEventArgs e)
    {
        await CreateRevenueInvoiceAsync(saveAsDraft: true);
    }

    private async void AppendToDraftInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
            var selectedProjectName = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.Name;
            var selectedAllocationIds = AllocationsGrid.SelectedItems
                .OfType<LineAllocationEntity>()
                .Select(x => x.LineAllocationId)
                .ToHashSet();
            var selectedWorkEntryIds = WorkEntriesGrid.SelectedItems
                .OfType<WorkTimeEntryEntity>()
                .Select(x => x.WorkTimeEntryId)
                .ToHashSet();
            var hasExplicitSelection = selectedAllocationIds.Count > 0 || selectedWorkEntryIds.Count > 0;

            var availableDrafts = (await App.Api.GetInvoicesAsync())
                .Where(x => x.IsDraft && x.IsCustomerDocument && x.CustomerId == customer.CustomerId)
                .OrderByDescending(x => x.InvoiceDate)
                .ToList();

            if (availableDrafts.Count == 0)
            {
                SetStatus("Fuer diesen Kunden gibt es noch keinen offenen Einnahme-Entwurf.", StatusMessageType.Warning);
                return;
            }

            var draftDialog = new SelectDraftInvoiceDialog(availableDrafts, customer.Name)
            {
                Owner = Window.GetWindow(this)
            };

            if (draftDialog.ShowDialog() != true || draftDialog.SelectedDraft is null)
            {
                return;
            }

            var draftSummary = draftDialog.SelectedDraft;
            var draftDetail = await App.Api.GetInvoiceAsync(draftSummary.InvoiceId);

            var allAllocations = (await App.Api.GetAllocationsAsync(customer.CustomerId, selectedProjectId))
                .Where(a => !hasExplicitSelection || selectedAllocationIds.Contains(a.LineAllocationId))
                .OrderBy(GetAllocationInvoiceDate)
                .ThenBy(GetAllocationPosition)
                .ToList();
            var allWorkEntries = (await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId))
                .Where(w => !hasExplicitSelection || selectedWorkEntryIds.Contains(w.WorkTimeEntryId))
                .OrderBy(w => w.WorkDate)
                .ThenBy(w => w.StartTime)
                .ToList();

            var isCreditNote = string.Equals(draftDetail.InvoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase);
            var allocations = FilterAllocationsForDocument(allAllocations, isCreditNote, hasExplicitSelection).ToList();
            var workEntries = FilterWorkEntriesForDocument(allWorkEntries, isCreditNote, hasExplicitSelection).ToList();

            if (allocations.Count == 0 && workEntries.Count == 0)
            {
                SetStatus(isCreditNote
                    ? (hasExplicitSelection
                        ? "Die markierten Positionen koennen nicht an diesen Gutschriftsentwurf angehaengt werden."
                        : "Bitte fuer einen Gutschriftsentwurf zuerst die betroffenen Positionen markieren.")
                    : (hasExplicitSelection
                        ? "Die markierten Positionen sind nicht offen genug, um an den Entwurf angehaengt zu werden."
                        : "Fuer diese Auswahl gibt es keine offenen Positionen zum Anhaengen an den Entwurf."),
                    StatusMessageType.Warning);
                return;
            }

            var hasAssignedSmallMaterial = allocations.Any(a => a.IsSmallMaterial);
            if (!TryGetBillingOptions(hasAssignedSmallMaterial, out var billingOptions))
            {
                return;
            }

            var generatedLines = BuildGeneratedInvoiceLines(
                allocations,
                workEntries,
                billingOptions.MarkupPercent,
                billingOptions.SmallMaterialFlatFee,
                billingOptions.SmallMaterialMode,
                selectedProjectName,
                draftDetail.ApplySmallBusinessRegulation,
                isCreditNote);

            if (generatedLines.Count == 0)
            {
                SetStatus("Es konnten keine berechenbaren Positionen fuer den ausgewaehlten Entwurf erstellt werden.", StatusMessageType.Warning);
                return;
            }

            var existingLines = draftDetail.Lines
                .OrderBy(x => x.Position)
                .Select(ToManualLineInput)
                .ToList();
            var nextPosition = existingLines.Count == 0 ? 1 : existingLines.Max(x => x.Position) + 1;
            foreach (var line in generatedLines)
            {
                existingLines.Add(new ManualInvoiceLineInput
                {
                    Position = nextPosition++,
                    AccountingCategory = line.AccountingCategory,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    NetUnitPrice = line.UnitPrice,
                    MetalSurcharge = 0m,
                    GrossListPrice = 0m,
                    GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(
                        PricingHelper.CalculateRevenueGrossTotal(line.LineTotal, draftDetail.ApplySmallBusinessRegulation),
                        line.Quantity),
                    PriceBasisQuantity = 1m,
                    GrossLineTotal = PricingHelper.CalculateRevenueGrossTotal(line.LineTotal, draftDetail.ApplySmallBusinessRegulation)
                });
            }

            UpdateRevenueLineGrossAmounts(existingLines, draftDetail.ApplySmallBusinessRegulation);

            var company = await App.Api.GetCompanyProfileAsync();
            var pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
            {
                Company = company,
                Customer = customer,
                InvoiceNumber = draftDetail.InvoiceNumber,
                CustomerNumber = customer.CustomerNumber,
                InvoiceDate = draftDetail.InvoiceDate,
                DeliveryDate = draftDetail.DeliveryDate ?? draftDetail.InvoiceDate,
                Subject = draftDetail.Subject,
                InvoiceDirection = draftDetail.InvoiceDirection,
                ApplySmallBusinessRegulation = draftDetail.ApplySmallBusinessRegulation,
                IsDraft = true,
                Lines = existingLines.Select(x => new CustomerInvoicePdfService.InvoiceLine
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
                draftDetail.InvoiceId,
                draftDetail.InvoiceDirection,
                "Draft",
                draftDetail.InvoiceNumber,
                draftDetail.InvoiceDate,
                draftDetail.DeliveryDate,
                draftDetail.PaymentDueDate,
                draftDetail.CustomerId,
                draftDetail.SupplierName,
                draftDetail.AccountingCategory,
                draftDetail.Subject,
                draftDetail.ApplySmallBusinessRegulation,
                PricingHelper.CalculateRevenueGrossTotal(existingLines.Sum(x => x.LineTotal), draftDetail.ApplySmallBusinessRegulation),
                0m,
                0m,
                draftDetail.SourcePdfPath,
                draftDetail.OriginalPdfFileName,
                Convert.ToBase64String(pdfBytes),
                ComputeSha256(pdfBytes),
                existingLines,
                hasSupplierInvoice: true);

            foreach (var allocation in allocations)
            {
                await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
                if (!isCreditNote || string.IsNullOrWhiteSpace(allocation.CustomerInvoiceNumber))
                {
                    await App.Api.UpdateAllocationRevenueLinkAsync(allocation.LineAllocationId, draftDetail.InvoiceId, draftDetail.InvoiceNumber, false);
                }
            }

            foreach (var workEntry in workEntries)
            {
                await App.Api.UpdateWorkTimeExportAsync(workEntry.WorkTimeEntryId, workEntry.ExportedUnitPrice, workEntry.ExportedLineTotal);
                if (!isCreditNote || string.IsNullOrWhiteSpace(workEntry.CustomerInvoiceNumber))
                {
                    await App.Api.UpdateWorkTimeRevenueLinkAsync(workEntry.WorkTimeEntryId, draftDetail.InvoiceId, draftDetail.InvoiceNumber, false);
                }
            }

            await LoadCustomerDataAsync();
            SetStatus($"{generatedLines.Count} Position(en) wurden an Entwurf {draftDetail.InvoiceNumber} angehaengt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Positionen konnten nicht an den Entwurf angehaengt werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async Task CreateRevenueInvoiceAsync(bool saveAsDraft)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var companyProfile = await App.Api.GetCompanyProfileAsync();
            if (string.IsNullOrWhiteSpace(companyProfile.CompanyName) || string.IsNullOrWhiteSpace(companyProfile.BankIban))
            {
                SetStatus("Bitte zuerst unter Admin > Firmendaten Firmenname und Bankverbindung pflegen.", StatusMessageType.Warning);
                return;
            }

            var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
            var selectedProjectName = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.Name;
            var selectedAllocationIds = AllocationsGrid.SelectedItems
                .OfType<LineAllocationEntity>()
                .Select(x => x.LineAllocationId)
                .ToHashSet();
            var selectedWorkEntryIds = WorkEntriesGrid.SelectedItems
                .OfType<WorkTimeEntryEntity>()
                .Select(x => x.WorkTimeEntryId)
                .ToHashSet();
            var hasExplicitSelection = selectedAllocationIds.Count > 0 || selectedWorkEntryIds.Count > 0;

            var allAllocations = (await App.Api.GetAllocationsAsync(customer.CustomerId, selectedProjectId))
                .Where(a => !hasExplicitSelection || selectedAllocationIds.Contains(a.LineAllocationId))
                .OrderBy(GetAllocationInvoiceDate)
                .ThenBy(GetAllocationPosition)
                .ToList();
            var allWorkEntries = (await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId))
                .Where(w => !hasExplicitSelection || selectedWorkEntryIds.Contains(w.WorkTimeEntryId))
                .OrderBy(w => w.WorkDate)
                .ThenBy(w => w.StartTime)
                .ToList();

            var hasAssignedSmallMaterial = allAllocations.Any(a => a.IsSmallMaterial);
            if (!TryGetBillingOptions(hasAssignedSmallMaterial, out var billingOptions))
            {
                return;
            }
            var markupPercent = billingOptions.MarkupPercent;
            var smallMaterialFlatFee = billingOptions.SmallMaterialFlatFee;
            var smallMaterialMode = billingOptions.SmallMaterialMode;

            var invoiceReservation = await App.Api.ReserveRevenueInvoiceNumberAsync(customer.CustomerId, "Revenue");
            var creditNoteReservation = await App.Api.ReserveRevenueInvoiceNumberAsync(customer.CustomerId, "RevenueReduction");
            customer.CustomerNumber = invoiceReservation.CustomerNumber;

            var dialog = new GenerateInvoiceDialog(invoiceReservation.InvoiceNumber, creditNoteReservation.InvoiceNumber, invoiceReservation.CustomerNumber, customer.Name, saveAsDraft)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || dialog.Result is null)
            {
                return;
            }

            var isCreditNote = string.Equals(dialog.Result.InvoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase);
            var allocations = FilterAllocationsForDocument(allAllocations, isCreditNote, hasExplicitSelection).ToList();
            var workEntries = FilterWorkEntriesForDocument(allWorkEntries, isCreditNote, hasExplicitSelection).ToList();

            if (allocations.Count == 0 && workEntries.Count == 0)
            {
                SetStatus(isCreditNote
                    ? (hasExplicitSelection
                        ? "Die markierten Positionen koennen nicht fuer eine Gutschrift verwendet werden."
                        : "Bitte fuer eine Gutschrift zuerst die betroffenen Positionen markieren.")
                    : (hasExplicitSelection
                        ? "Die markierten Positionen sind nicht offen genug fuer eine Rechnung oder passen nicht zum aktuellen Filter."
                        : "Fuer diese Auswahl gibt es keine offenen Positionen fuer eine Rechnung."),
                    StatusMessageType.Warning);
                return;
            }

            if (!saveAsDraft)
            {
                var confirmation = MessageBox.Show(
                    isCreditNote
                        ? "Diese Gutschrift wird jetzt final erstellt und kann danach nicht mehr bearbeitet werden.\n\nWenn spaeter ein Fehler auffaellt, muss der Beleg storniert werden.\n\nWillst du die Gutschrift jetzt verbindlich erzeugen?"
                        : "Diese Rechnung wird jetzt final erstellt und kann danach nicht mehr bearbeitet werden.\n\nWenn spaeter ein Fehler auffaellt, muss die Rechnung storniert werden.\n\nWillst du die Rechnung jetzt verbindlich erzeugen?",
                    isCreditNote ? "Gutschrift verbindlich erzeugen" : "Rechnung verbindlich erzeugen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                {
                    SetStatus($"{(isCreditNote ? "Gutschrift" : "Rechnung")}serzeugung abgebrochen. Du kannst stattdessen auch zuerst einen Entwurf speichern.", StatusMessageType.Info);
                    return;
                }
            }

            List<GeneratedInvoiceLine> invoiceLines;
            try
            {
                invoiceLines = BuildGeneratedInvoiceLines(allocations, workEntries, markupPercent, smallMaterialFlatFee, smallMaterialMode, selectedProjectName, dialog.Result.ApplySmallBusinessRegulation, isCreditNote);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Die Rechnungspositionen konnten nicht aufgebaut werden: {ex.Message}", ex);
            }

            if (invoiceLines.Count == 0)
            {
                SetStatus("Es konnten keine berechenbaren Positionen fuer die Rechnung erstellt werden.", StatusMessageType.Warning);
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

            byte[] pdfBytes;
            try
            {
                pdfBytes = CustomerInvoicePdfService.Create(new CustomerInvoicePdfService.InvoiceDocument
                {
                    Company = companyProfile,
                    Customer = customer,
                    InvoiceNumber = dialog.Result.InvoiceNumber,
                    CustomerNumber = dialog.Result.CustomerNumber,
                    InvoiceDate = dialog.Result.InvoiceDate.Date,
                    DeliveryDate = dialog.Result.DeliveryDate.Date,
                    Subject = dialog.Result.Subject,
                    InvoiceDirection = dialog.Result.InvoiceDirection,
                    IsDraft = saveAsDraft,
                    ApplySmallBusinessRegulation = dialog.Result.ApplySmallBusinessRegulation,
                    Lines = invoiceLines.Select(x => new CustomerInvoicePdfService.InvoiceLine
                    {
                        Position = x.Position,
                        Description = x.Description,
                        Quantity = x.Quantity,
                        Unit = x.Unit,
                        UnitPrice = x.UnitPrice,
                        LineTotal = x.LineTotal
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Das Rechnungs-PDF konnte nicht erzeugt werden: {ex.Message}", ex);
            }

            await File.WriteAllBytesAsync(saveDialog.FileName, pdfBytes);

            int invoiceId;
            try
            {
                invoiceId = await App.Api.SaveInvoiceAsync(
                    dialog.Result.InvoiceDirection,
                    saveAsDraft ? "Draft" : "Finalized",
                    dialog.Result.InvoiceNumber,
                    dialog.Result.InvoiceDate.Date,
                    dialog.Result.DeliveryDate.Date,
                    null,
                    customer.CustomerId,
                    customer.Name,
                    "Other",
                    dialog.Result.Subject,
                    dialog.Result.ApplySmallBusinessRegulation,
                    PricingHelper.CalculateRevenueGrossTotal(invoiceLines.Sum(x => x.LineTotal), dialog.Result.ApplySmallBusinessRegulation),
                    0m,
                    0m,
                    saveDialog.FileName,
                    Path.GetFileName(saveDialog.FileName),
                    Convert.ToBase64String(pdfBytes),
                    ComputeSha256(pdfBytes),
                    invoiceLines.Select(x => new ManualInvoiceLineInput
                    {
                        Position = x.Position,
                        AccountingCategory = x.AccountingCategory,
                        Description = x.Description,
                        Quantity = x.Quantity,
                        Unit = x.Unit,
                        NetUnitPrice = x.UnitPrice,
                        MetalSurcharge = 0m,
                        GrossListPrice = 0m,
                        GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(
                            PricingHelper.CalculateRevenueGrossTotal(x.LineTotal, dialog.Result.ApplySmallBusinessRegulation),
                            x.Quantity),
                        PriceBasisQuantity = 1m,
                        GrossLineTotal = PricingHelper.CalculateRevenueGrossTotal(x.LineTotal, dialog.Result.ApplySmallBusinessRegulation)
                    }),
                    hasSupplierInvoice: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Die Rechnung konnte nicht gespeichert werden: {ex.Message}", ex);
            }

            try
            {
                foreach (var allocation in allocations)
                {
                    await App.Api.UpdateAllocationExportAsync(allocation.LineAllocationId, allocation.ExportedMarkupPercent, allocation.ExportedUnitPrice, allocation.ExportedLineTotal);
                    if (!isCreditNote || string.IsNullOrWhiteSpace(allocation.CustomerInvoiceNumber))
                    {
                        await App.Api.UpdateAllocationRevenueLinkAsync(allocation.LineAllocationId, invoiceId, dialog.Result.InvoiceNumber, !saveAsDraft);
                    }
                }

                foreach (var workEntry in workEntries)
                {
                    await App.Api.UpdateWorkTimeExportAsync(workEntry.WorkTimeEntryId, workEntry.ExportedUnitPrice, workEntry.ExportedLineTotal);
                    if (!isCreditNote || string.IsNullOrWhiteSpace(workEntry.CustomerInvoiceNumber))
                    {
                        await App.Api.UpdateWorkTimeRevenueLinkAsync(workEntry.WorkTimeEntryId, invoiceId, dialog.Result.InvoiceNumber, !saveAsDraft);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Die offenen Positionen konnten nicht mit der Rechnung verknuepft werden: {ex.Message}", ex);
            }

            await LoadCustomerDataAsync();
            SetStatus(saveAsDraft
                ? $"Entwurf {dialog.Result.InvoiceNumber} wurde gespeichert."
                : $"{(isCreditNote ? "Gutschrift" : "Rechnung")} {dialog.Result.InvoiceNumber} wurde erstellt und als PDF gespeichert.", StatusMessageType.Success);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus($"{(saveAsDraft ? "Entwurf" : "Rechnung")} konnte nicht erzeugt werden: {ex.Message}", StatusMessageType.Error);
        }
        catch (Exception ex)
        {
            var fallbackMessage = ex is NullReferenceException
                ? "Mindestens eine verknuepfte Position enthaelt noch unvollstaendige Daten. Bitte Auswahl aktualisieren und die betroffenen Materialpositionen pruefen."
                : ex.Message;
            SetStatus($"{(saveAsDraft ? "Entwurf" : "Rechnung")} konnte nicht erzeugt werden: {fallbackMessage}", StatusMessageType.Error);
        }
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
            SupplierInvoiceNumber = GetAllocationInvoiceDisplayNumber(allocation),
            ArticleNumber = GetAllocationArticleNumber(allocation),
            Ean = GetAllocationEan(allocation),
            Description = $"[{projectLabel}] {GetAllocationDescription(allocation)}",
            Quantity = allocation.AllocatedQuantity,
            Unit = GetAllocationUnit(allocation),
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
                SupplierInvoiceNumber = GetAllocationInvoiceDisplayNumber(allocation),
                ArticleNumber = string.IsNullOrWhiteSpace(GetAllocationArticleNumber(allocation)) ? "KLEINMAT" : GetAllocationArticleNumber(allocation),
                Ean = GetAllocationEan(allocation),
                Description = $"[{projectLabel}] Kleinmaterial: {GetAllocationDescription(allocation)}",
                Quantity = allocation.AllocatedQuantity,
                Unit = GetAllocationUnit(allocation),
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
        if (flatFee <= 0m)
        {
            return;
        }

        if (allocations.Count == 0)
        {
            rows.Add(new ExcelExportService.ExportRow
            {
                SupplierInvoiceNumber = "Kleinmaterial",
                ArticleNumber = "KM-PAUSCH",
                Ean = string.Empty,
                Description = $"[{ResolveSmallMaterialLabel(selectedProjectName, allocations)}] Kleinmaterial-Pauschale",
                Quantity = 1m,
                Unit = "Pauschale",
                PurchaseUnitPrice = 0m,
                MarkupPercent = 0m,
                SalesUnitPrice = flatFee,
                Total = flatFee
            });
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

        var label = ResolveSmallMaterialLabel(selectedProjectName, allocations);

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

    private decimal GetPurchaseUnitPrice(LineAllocationEntity allocation, bool useGrossPurchaseValues = false)
    {
        if (allocation.InvoiceLine is not null)
        {
            return useGrossPurchaseValues
                ? GetDefaultAllocationGrossUnitPrice(allocation)
                : GetDefaultAllocationNetUnitPrice(allocation);
        }

        return allocation.CustomerUnitPrice <= 0m
            ? 0m
            : PricingHelper.RoundUnitPrice(allocation.CustomerUnitPrice);
    }

    private static decimal GetDefaultAllocationNetUnitPrice(LineAllocationEntity allocation)
    {
        if (allocation.InvoiceLine is null)
        {
            return 0m;
        }

        if (allocation.InvoiceLine.Quantity > 0m && allocation.InvoiceLine.LineTotal > 0m)
        {
            return PricingHelper.RoundUnitPrice(allocation.InvoiceLine.LineTotal / allocation.InvoiceLine.Quantity);
        }

        return PricingHelper.NormalizeUnitPrice(
            allocation.InvoiceLine.NetUnitPrice,
            allocation.InvoiceLine.MetalSurcharge,
            allocation.InvoiceLine.PriceBasisQuantity);
    }

    private static decimal GetDefaultAllocationGrossUnitPrice(LineAllocationEntity allocation)
    {
        if (allocation.InvoiceLine is null)
        {
            return 0m;
        }

        if (allocation.InvoiceLine.Quantity > 0m && allocation.InvoiceLine.GrossLineTotal > 0m)
        {
            return PricingHelper.RoundUnitPrice(allocation.InvoiceLine.GrossLineTotal / allocation.InvoiceLine.Quantity);
        }

        if (allocation.InvoiceLine.GrossUnitPrice > 0m)
        {
            return PricingHelper.RoundUnitPrice(allocation.InvoiceLine.GrossUnitPrice);
        }

        var netUnitPrice = GetDefaultAllocationNetUnitPrice(allocation);
        return netUnitPrice <= 0m
            ? 0m
            : PricingHelper.RoundUnitPrice(PricingHelper.CalculateExpenseGrossTotal(netUnitPrice));
    }

    private List<GeneratedInvoiceLine> BuildGeneratedInvoiceLines(
        IReadOnlyList<LineAllocationEntity> allocations,
        IReadOnlyList<WorkTimeEntryEntity> workEntries,
        decimal markupPercent,
        decimal smallMaterialFlatFee,
        string smallMaterialMode,
        string? selectedProjectName,
        bool applySmallBusinessRegulation,
        bool isCreditNote)
    {
        var lines = new List<GeneratedInvoiceLine>();
        var position = 1;
        var regularAllocations = allocations.Where(a => !a.IsSmallMaterial).ToList();
        var smallMaterialAllocations = allocations.Where(a => a.IsSmallMaterial).ToList();
        var useGrossPurchaseValues = applySmallBusinessRegulation;

        foreach (var allocation in regularAllocations)
        {
            lines.Add(BuildRegularAllocationLine(allocation, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues, position++, isCreditNote));
        }

        if (smallMaterialFlatFee > 0m)
        {
            lines.Add(BuildSmallMaterialFlatFeeLine(smallMaterialAllocations, smallMaterialFlatFee, selectedProjectName, position++, isCreditNote));
        }
        else if (smallMaterialAllocations.Count > 0)
        {
            switch (smallMaterialMode)
            {
                case "Einzeln berechnen":
                    foreach (var allocation in smallMaterialAllocations)
                    {
                        lines.Add(BuildSmallMaterialSingleLine(allocation, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues, position++, isCreditNote));
                    }

                    break;
                case "Als Sammelposition":
                    foreach (var groupedLine in BuildSmallMaterialGroupedLines(smallMaterialAllocations, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues, position, out position, isCreditNote))
                    {
                        lines.Add(groupedLine);
                    }

                    break;
                case "Nicht berechnen":
                    foreach (var allocation in smallMaterialAllocations)
                    {
                        allocation.ExportedMarkupPercent = 0m;
                        allocation.ExportedUnitPrice = 0m;
                        allocation.ExportedLineTotal = 0m;
                        allocation.LastExportedAt = DateTime.Now;
                    }

                    break;
            }
        }

        if (workEntries.Count > 0)
        {
            foreach (var workEntry in workEntries)
            {
                workEntry.ExportedLineTotal = workEntry.CalculatedLineTotal;
                workEntry.ExportedUnitPrice = workEntry.HoursWorked > 0m ? workEntry.ExportedLineTotal / workEntry.HoursWorked : workEntry.ExportedLineTotal;
                workEntry.LastExportedAt = DateTime.Now;
            }

            var totalHours = workEntries.Sum(x => x.HoursWorked);
            var totalAmount = workEntries.Sum(x => x.ExportedLineTotal);
            var distinctDescriptions = workEntries
                .Select(x => string.IsNullOrWhiteSpace(x.Description) ? "Arbeitszeit" : x.Description.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var description = distinctDescriptions.Count == 1
                ? distinctDescriptions[0]
                : "Arbeitszeit";

            lines.Add(new GeneratedInvoiceLine
            {
                Position = position++,
                Description = description,
                Quantity = totalHours,
                Unit = "h",
                AccountingCategory = "Services",
                UnitPrice = ApplyDocumentSign(totalHours > 0m ? totalAmount / totalHours : totalAmount, isCreditNote),
                LineTotal = ApplyDocumentSign(totalAmount, isCreditNote)
            });
        }

        return lines.Where(x => x.LineTotal > 0m).ToList();
    }

    private GeneratedInvoiceLine BuildRegularAllocationLine(LineAllocationEntity allocation, decimal markupPercent, bool applySmallBusinessRegulation, bool useGrossPurchaseValues, int position, bool isCreditNote)
    {
        EnsureAllocationCanBeBilled(allocation);
        var purchaseUnitPrice = GetPurchaseUnitPrice(allocation, useGrossPurchaseValues);
        var salesUnitPrice = CalculateRevenueSalesUnitPrice(purchaseUnitPrice, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues);
        allocation.ExportedMarkupPercent = markupPercent;
        allocation.ExportedUnitPrice = salesUnitPrice;
        allocation.ExportedLineTotal = salesUnitPrice * allocation.AllocatedQuantity;
        allocation.LastExportedAt = DateTime.Now;
        return new GeneratedInvoiceLine
        {
            Position = position,
            AccountingCategory = allocation.InvoiceLine?.AccountingCategory ?? "MaterialAndGoods",
            Description = allocation.Project is null
                ? GetAllocationDescription(allocation)
                : $"[{allocation.Project.Name}] {GetAllocationDescription(allocation)}",
            Quantity = allocation.AllocatedQuantity,
            Unit = GetAllocationUnit(allocation),
            UnitPrice = ApplyDocumentSign(salesUnitPrice, isCreditNote),
            LineTotal = ApplyDocumentSign(allocation.ExportedLineTotal, isCreditNote)
        };
    }

    private GeneratedInvoiceLine BuildSmallMaterialSingleLine(LineAllocationEntity allocation, decimal markupPercent, bool applySmallBusinessRegulation, bool useGrossPurchaseValues, int position, bool isCreditNote)
    {
        EnsureAllocationCanBeBilled(allocation);
        var purchaseUnitPrice = GetPurchaseUnitPrice(allocation, useGrossPurchaseValues);
        var salesUnitPrice = CalculateRevenueSalesUnitPrice(purchaseUnitPrice, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues);
        allocation.ExportedMarkupPercent = markupPercent;
        allocation.ExportedUnitPrice = salesUnitPrice;
        allocation.ExportedLineTotal = salesUnitPrice * allocation.AllocatedQuantity;
        allocation.LastExportedAt = DateTime.Now;
        return new GeneratedInvoiceLine
        {
            Position = position,
            AccountingCategory = "MaterialAndGoods",
            Description = allocation.Project is null
                ? $"Kleinmaterial: {GetAllocationDescription(allocation)}"
                : $"[{allocation.Project.Name}] Kleinmaterial: {GetAllocationDescription(allocation)}",
            Quantity = allocation.AllocatedQuantity,
            Unit = GetAllocationUnit(allocation),
            UnitPrice = ApplyDocumentSign(salesUnitPrice, isCreditNote),
            LineTotal = ApplyDocumentSign(allocation.ExportedLineTotal, isCreditNote)
        };
    }

    private List<GeneratedInvoiceLine> BuildSmallMaterialGroupedLines(IReadOnlyList<LineAllocationEntity> allocations, decimal markupPercent, bool applySmallBusinessRegulation, bool useGrossPurchaseValues, int startPosition, out int nextPosition, bool isCreditNote)
    {
        var lines = new List<GeneratedInvoiceLine>();
        var position = startPosition;

        foreach (var group in allocations.GroupBy(a => a.Project?.Name ?? "Ohne Projekt"))
        {
            var salesTotal = 0m;
            foreach (var allocation in group)
            {
                var purchaseUnitPrice = GetPurchaseUnitPrice(allocation, useGrossPurchaseValues);
                var salesUnitPrice = CalculateRevenueSalesUnitPrice(purchaseUnitPrice, markupPercent, applySmallBusinessRegulation, useGrossPurchaseValues);
                var lineTotal = salesUnitPrice * allocation.AllocatedQuantity;
                allocation.ExportedMarkupPercent = markupPercent;
                allocation.ExportedUnitPrice = salesUnitPrice;
                allocation.ExportedLineTotal = lineTotal;
                allocation.LastExportedAt = DateTime.Now;
                salesTotal += lineTotal;
            }

            lines.Add(new GeneratedInvoiceLine
            {
                Position = position++,
                AccountingCategory = "MaterialAndGoods",
                Description = $"[{group.Key}] Kleinmaterial laut Nachweis",
                Quantity = 1m,
                Unit = "Pauschale",
                UnitPrice = ApplyDocumentSign(salesTotal, isCreditNote),
                LineTotal = ApplyDocumentSign(salesTotal, isCreditNote)
            });
        }

        nextPosition = position;
        return lines;
    }

    private GeneratedInvoiceLine BuildSmallMaterialFlatFeeLine(IReadOnlyList<LineAllocationEntity> allocations, decimal flatFee, string? selectedProjectName, int position, bool isCreditNote)
    {
        if (allocations.Count > 0)
        {
            var totalPurchase = allocations.Sum(a => GetPurchaseUnitPrice(a) * a.AllocatedQuantity);
            var equalShare = flatFee / allocations.Count;
            foreach (var allocation in allocations)
            {
                var purchaseShare = GetPurchaseUnitPrice(allocation) * allocation.AllocatedQuantity;
                var lineTotal = totalPurchase > 0m ? flatFee * (purchaseShare / totalPurchase) : equalShare;
                allocation.ExportedMarkupPercent = 0m;
                allocation.ExportedLineTotal = lineTotal;
                allocation.ExportedUnitPrice = allocation.AllocatedQuantity > 0m ? lineTotal / allocation.AllocatedQuantity : 0m;
                allocation.LastExportedAt = DateTime.Now;
            }
        }

        var label = ResolveSmallMaterialLabel(selectedProjectName, allocations);

        return new GeneratedInvoiceLine
        {
            Position = position,
            AccountingCategory = "MaterialAndGoods",
            Description = $"[{label}] Kleinmaterial-Pauschale",
            Quantity = 1m,
            Unit = "Pauschale",
            UnitPrice = ApplyDocumentSign(flatFee, isCreditNote),
            LineTotal = ApplyDocumentSign(flatFee, isCreditNote)
        };
    }

    private static decimal ApplyDocumentSign(decimal amount, bool isCreditNote)
        => isCreditNote ? -amount : amount;

    private static decimal CalculateRevenueSalesUnitPrice(decimal purchaseUnitPrice, decimal markupPercent, bool applySmallBusinessRegulation, bool useGrossPurchaseValues)
    {
        if (applySmallBusinessRegulation && useGrossPurchaseValues)
        {
            return PricingHelper.ApplyMarkup(purchaseUnitPrice, markupPercent);
        }

        return PricingHelper.CalculateRevenueUnitPrice(purchaseUnitPrice, markupPercent, applySmallBusinessRegulation);
    }

    private static IEnumerable<LineAllocationEntity> FilterAllocationsForDocument(IEnumerable<LineAllocationEntity> allocations, bool isCreditNote, bool hasExplicitSelection)
    {
        if (isCreditNote)
        {
            return hasExplicitSelection ? allocations : [];
        }

        return allocations.Where(a => string.IsNullOrWhiteSpace(a.CustomerInvoiceNumber) && !a.RevenueInvoiceId.HasValue);
    }

    private static IEnumerable<WorkTimeEntryEntity> FilterWorkEntriesForDocument(IEnumerable<WorkTimeEntryEntity> workEntries, bool isCreditNote, bool hasExplicitSelection)
    {
        if (isCreditNote)
        {
            return hasExplicitSelection ? workEntries : [];
        }

        return workEntries.Where(w => string.IsNullOrWhiteSpace(w.CustomerInvoiceNumber) && !w.RevenueInvoiceId.HasValue);
    }

    private static string ResolveSmallMaterialLabel(string? selectedProjectName, IReadOnlyList<LineAllocationEntity> allocations)
    {
        if (!string.IsNullOrWhiteSpace(selectedProjectName))
        {
            return selectedProjectName;
        }

        if (allocations.Count == 0)
        {
            return "Allgemein";
        }

        var projectNames = allocations.Select(a => a.Project?.Name ?? "Ohne Projekt").Distinct().ToList();
        return projectNames.Count == 1 ? projectNames[0] : "Mehrere Projekte";
    }

    private static DateTime GetAllocationInvoiceDate(LineAllocationEntity allocation)
    {
        return allocation.InvoiceLine?.Invoice?.InvoiceDate ?? allocation.AllocatedAt.Date;
    }

    private static int GetAllocationPosition(LineAllocationEntity allocation)
    {
        return allocation.InvoiceLine?.Position ?? int.MaxValue;
    }

    private static string GetAllocationDescription(LineAllocationEntity allocation)
    {
        return string.IsNullOrWhiteSpace(allocation.InvoiceLine?.Description)
            ? "Materialposition"
            : allocation.InvoiceLine.Description;
    }

    private static string GetAllocationUnit(LineAllocationEntity allocation)
    {
        return string.IsNullOrWhiteSpace(allocation.InvoiceLine?.Unit)
            ? "Stk"
            : allocation.InvoiceLine.Unit;
    }

    private static string GetAllocationArticleNumber(LineAllocationEntity allocation)
    {
        return allocation.InvoiceLine?.ArticleNumber ?? string.Empty;
    }

    private static string GetAllocationEan(LineAllocationEntity allocation)
    {
        return allocation.InvoiceLine?.Ean ?? string.Empty;
    }

    private static string GetAllocationInvoiceDisplayNumber(LineAllocationEntity allocation)
    {
        return string.IsNullOrWhiteSpace(allocation.InvoiceLine?.InvoiceDisplayNumber)
            ? "Keine Rechnung"
            : allocation.InvoiceLine.InvoiceDisplayNumber;
    }

    private static void EnsureAllocationCanBeBilled(LineAllocationEntity allocation)
    {
        if (allocation.InvoiceLine is null && allocation.CustomerUnitPrice <= 0m)
        {
            throw new InvalidOperationException("Mindestens eine zugewiesene Materialposition hat weder Rechnungszeile noch hinterlegten EK/Stk.");
        }
    }

    private bool TryGetBillingOptions(bool showSmallMaterialMode, out BillingOptionsResult result)
    {
        var dialog = new BillingOptionsDialog(_lastMarkupPercent, _lastSmallMaterialMode, _lastSmallMaterialFlatFee, showSmallMaterialMode)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            result = new BillingOptionsResult();
            SetStatus("Abrechnungsvorgang abgebrochen.", StatusMessageType.Info);
            return false;
        }

        result = dialog.Result;
        _lastMarkupPercent = result.MarkupPercent;
        _lastSmallMaterialMode = result.SmallMaterialMode;
        _lastSmallMaterialFlatFee = result.SmallMaterialFlatFee;
        return true;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCustomersAsync(CustomerCombo.SelectedItem is CustomerEntity customer ? customer.Name : null, (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId);
    }

    private async Task LoadCustomersAsync(string? selectCustomerName = null, int? selectedProjectId = null)
    {
        var selectedCustomerId = CustomerCombo.SelectedItem is CustomerEntity selectedCustomer ? selectedCustomer.CustomerId : (int?)null;
        var customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectFilterCombo.ItemsSource = null;
            ProjectFilterCombo.SelectedItem = null;
            AllocationsGrid.ItemsSource = null;
            WorkEntriesGrid.ItemsSource = null;
            EditAllocationQtyText.Clear();
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Info);
            return;
        }

        CustomerEntity? customerToSelect = null;
        if (!string.IsNullOrWhiteSpace(selectCustomerName))
        {
            customerToSelect = customers.FirstOrDefault(c => c.Name == selectCustomerName);
        }

        customerToSelect ??= App.SelectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == App.SelectedCustomerId.Value) : null;
        customerToSelect ??= selectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value) : null;
        customerToSelect ??= customers[0];
        CustomerCombo.SelectedItem = customerToSelect;
        App.SetSelectedCustomer(customerToSelect.CustomerId);
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
            SetStatus("Kein Kunde ausgewaehlt.", StatusMessageType.Info);
            return;
        }

        var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        _lastMarkupPercent = customer.DefaultMarkupPercent;
        var showLinked = ShowLinkedItemsCheckBox.IsChecked == true;

        var allocations = await App.Api.GetAllocationsAsync(customer.CustomerId, selectedProjectId);
        var workEntries = await App.Api.GetWorkTimeEntriesAsync(customer.CustomerId, selectedProjectId);
        if (!showLinked)
        {
            allocations = allocations
                .Where(a => string.IsNullOrWhiteSpace(a.CustomerInvoiceNumber) && !a.RevenueInvoiceId.HasValue)
                .ToList();
            workEntries = workEntries
                .Where(w => string.IsNullOrWhiteSpace(w.CustomerInvoiceNumber) && !w.RevenueInvoiceId.HasValue)
                .ToList();
        }

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
            SetStatus("Keine Material- oder Arbeitszeitpositionen fuer die ausgewaehlte Kunden-/Projektansicht gefunden.", StatusMessageType.Info);
            return;
        }

        var smallMaterialCount = allocations.Count(a => a.IsSmallMaterial);
        var statusPrefix = showLinked ? "Auch bereits verknuepfte Positionen werden angezeigt." : "Es werden nur offene Positionen angezeigt.";
        SetStatus($"{statusPrefix} {allocations.Count} Materialposition(en), davon {smallMaterialCount} Kleinmaterial, und {workEntries.Count} Arbeitszeitposition(en) geladen.", StatusMessageType.Info);
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

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static void UpdateRevenueLineGrossAmounts(IEnumerable<ManualInvoiceLineInput> lines, bool applySmallBusinessRegulation)
    {
        foreach (var line in lines)
        {
            line.GrossLineTotal = PricingHelper.CalculateRevenueGrossTotal(line.LineTotal, applySmallBusinessRegulation);
            line.GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(line.GrossLineTotal, line.Quantity);
        }
    }

    private static ManualInvoiceLineInput ToManualLineInput(InvoiceLineEntity line)
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
            AccountingCategory = line.AccountingCategory,
            GrossListPrice = line.GrossListPrice,
            GrossUnitPrice = line.GrossUnitPrice,
            PriceBasisQuantity = line.PriceBasisQuantity,
            ShippingNetShare = line.ShippingNetShare,
            ShippingGrossShare = line.ShippingGrossShare,
            GrossLineTotal = line.GrossLineTotal
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private sealed class GeneratedInvoiceLine
    {
        public int Position { get; set; }
        public string AccountingCategory { get; set; } = "MaterialAndGoods";
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}


