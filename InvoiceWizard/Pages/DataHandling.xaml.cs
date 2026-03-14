using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class DataHandling : Page
{
    public DataHandling()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await LoadLinesAsync();
        };
    }

    private async void AllocateQuantity_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId == null)
        {
            SetStatus("Bitte ein Projekt des Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (LinesGrid.SelectedItems.OfType<InvoiceLineRow>().Count() != 1)
        {
            SetStatus("Bitte genau eine Position markieren, wenn du eine Teilmenge zuweisen willst.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseDecimal(AllocateQtyText.Text, out var qty) || qty <= 0m)
        {
            SetStatus("Bitte eine gueltige Zuweisungsmenge eingeben.", StatusMessageType.Error);
            return;
        }

        var selectedRow = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().First();
        if (!selectedRow.IsProjectAllocatable)
        {
            SetStatus("Nur Rechnungen der Kategorie 'Material und Waren' koennen Projekten zugewiesen werden.", StatusMessageType.Warning);
            return;
        }

        var defaultUnitPrice = PricingHelper.NormalizeUnitPrice(selectedRow.Line.NetUnitPrice, selectedRow.Line.MetalSurcharge, selectedRow.Line.PriceBasisQuantity);
        var hasCustomPrice = TryParseDecimal(CustomerPriceText.Text, out var enteredPrice);

        if (LooksLikeSwappedQuantityAndPrice(qty, enteredPrice, hasCustomPrice, selectedRow.RemainingQuantity, defaultUnitPrice))
        {
            var suggestion = $"Im Feld 'Preis/Stk' steht {enteredPrice:0.##}, waehrend die Menge noch 1 ist. Soll stattdessen die Menge auf {enteredPrice:0.##} gesetzt und der Standardpreis von {defaultUnitPrice:0.##} verwendet werden?";
            if (MessageBox.Show(suggestion, "Eingabe pruefen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                qty = enteredPrice;
                hasCustomPrice = false;
                CustomerPriceText.Clear();
                AllocateQtyText.Text = qty.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
            }
        }

        if (qty > selectedRow.RemainingQuantity)
        {
            SetStatus($"Die Restmenge betraegt {selectedRow.RemainingQuantity:0.##}.", StatusMessageType.Warning);
            return;
        }

        var customerUnitPrice = hasCustomPrice
            ? enteredPrice
            : defaultUnitPrice;

        try
        {
            await App.Api.CreateAllocationAsync(selectedRow.Line.InvoiceLineId, customer.CustomerId, projectSelection.ProjectId.Value, qty, customerUnitPrice, IsSmallMaterialCheckBox.IsChecked == true);
            await LoadLinesAsync();
            CustomerPriceText.Clear();
            AllocateQtyText.Text = "1";
            IsSmallMaterialCheckBox.IsChecked = false;
            SetStatus($"{qty:0.##} {selectedRow.Line.Unit} wurden {customer.Name} / {projectSelection.Name} zugewiesen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Zuweisung fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void AllocateCompleteSelection_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId == null)
        {
            SetStatus("Bitte ein Projekt des Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var selectedRows = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().ToList();
        if (selectedRows.Count == 0)
        {
            SetStatus("Bitte mindestens eine Position markieren.", StatusMessageType.Warning);
            return;
        }

        var blockedRows = selectedRows.Where(r => !r.IsProjectAllocatable).ToList();
        if (blockedRows.Count > 0)
        {
            SetStatus("Es koennen nur Rechnungen der Kategorie 'Material und Waren' komplett zugewiesen werden.", StatusMessageType.Warning);
            return;
        }

        var created = 0;
        foreach (var row in selectedRows.Where(r => r.RemainingQuantity > 0m))
        {
            var customerUnitPrice = TryParseDecimal(CustomerPriceText.Text, out var enteredPrice)
                ? enteredPrice
                : PricingHelper.NormalizeUnitPrice(row.Line.NetUnitPrice, row.Line.MetalSurcharge, row.Line.PriceBasisQuantity);
            await App.Api.CreateAllocationAsync(row.Line.InvoiceLineId, customer.CustomerId, projectSelection.ProjectId.Value, row.RemainingQuantity, customerUnitPrice, IsSmallMaterialCheckBox.IsChecked == true);
            created++;
        }

        await LoadLinesAsync();
        CustomerPriceText.Clear();
        IsSmallMaterialCheckBox.IsChecked = false;
        SetStatus($"{created} Position(en) wurden komplett an {customer.Name} / {projectSelection.Name} zugewiesen.", StatusMessageType.Success);
    }

    private async void DeleteLine_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().ToList();
        if (selectedRows.Count == 0)
        {
            SetStatus("Bitte mindestens eine Position markieren.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Sollen {selectedRows.Count} markierte Position(en) wirklich komplett geloescht werden? Alle zugehoerigen Zuweisungen gehen dabei verloren.", "Positionen loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var row in selectedRows)
        {
            await App.Api.DeleteInvoiceLineAsync(row.Line.InvoiceLineId);
        }

        await LoadLinesAsync();
        SetStatus($"{selectedRows.Count} Position(en) wurden geloescht.", StatusMessageType.Success);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCustomersAsync(CustomerCombo.SelectedItem is CustomerEntity customer ? customer.CustomerId : null);
        await LoadLinesAsync();
    }

    private async void ShowCompletedChanged(object sender, RoutedEventArgs e)
    {
        await LoadLinesAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
    }

    private async Task LoadCustomersAsync(int? selectedCustomerId = null)
    {
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            return;
        }

        var customer = selectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value) ?? customers[0] : customers[0];
        CustomerCombo.SelectedItem = customer;
        await LoadProjectsAsync(customer);
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer, int? selectedProjectId = null)
    {
        if (customer == null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId);
        ProjectCombo.ItemsSource = projects;
        if (projects.Count == 0)
        {
            ProjectCombo.SelectedItem = null;
            SetStatus("Dieser Kunde hat noch keine Projekte. Bitte lege zuerst unter 'Kunden und Export' ein Projekt an.", StatusMessageType.Warning);
            return;
        }

        ProjectCombo.SelectedItem = selectedProjectId.HasValue ? projects.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) ?? projects[0] : projects[0];
    }

    private async Task LoadLinesAsync()
    {
        var lines = await App.Api.GetInvoiceLineRowsAsync(ShowCompletedCheckBox.IsChecked == true);
        LinesGrid.ItemsSource = lines;
        SetStatus("Positionen geladen.", StatusMessageType.Info);
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool LooksLikeSwappedQuantityAndPrice(decimal quantity, decimal enteredPrice, bool hasCustomPrice, decimal remainingQuantity, decimal defaultUnitPrice)
    {
        if (!hasCustomPrice || quantity != 1m)
        {
            return false;
        }

        if (enteredPrice <= 1m || enteredPrice != decimal.Truncate(enteredPrice))
        {
            return false;
        }

        if (enteredPrice > remainingQuantity)
        {
            return false;
        }

        return defaultUnitPrice > 0m && enteredPrice < (defaultUnitPrice / 2m);
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




