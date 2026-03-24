using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class InventoryPage : Page
{
    private List<InvoiceLineRow> _allLines = [];
    private string SelectedPoolMode => ((PoolModeCombo.SelectedItem as ComboBoxItem)?.Tag as string) ?? "inventory";
    private string SelectedPoolLabel => SelectedPoolMode == "generalSmallMaterial" ? "Kleinmaterial" : "Bestand";

    public InventoryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            ApplyPoolTexts();
            await LoadCustomersAsync();
            await LoadLinesAsync();
        };
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        App.SetSelectedCustomer((CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId);
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
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

    private async void PoolModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyPoolTexts();
        await LoadLinesAsync();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
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
            SetStatus($"Bitte genau eine {SelectedPoolLabel.ToLowerInvariant()}position markieren, wenn du eine Teilmenge zuweisen willst.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseDecimal(AllocateQtyText.Text, out var qty) || qty <= 0m)
        {
            SetStatus("Bitte eine gueltige Zuweisungsmenge eingeben.", StatusMessageType.Error);
            return;
        }

        var selectedRow = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().First();
        if (qty > selectedRow.RemainingQuantity)
        {
            SetStatus($"Im {SelectedPoolLabel.ToLowerInvariant()} sind noch {selectedRow.RemainingQuantity:0.##} {selectedRow.Unit} offen.", StatusMessageType.Warning);
            return;
        }

        var customerUnitPrice = TryParseDecimal(CustomerPriceText.Text, out var enteredPrice)
            ? enteredPrice
            : selectedRow.EffectivePurchaseUnitPrice;

        try
        {
            await App.Api.CreateAllocationAsync(selectedRow.Line.InvoiceLineId, customer.CustomerId, projectSelection.ProjectId.Value, qty, customerUnitPrice, false);
            await LoadLinesAsync();
            CustomerPriceText.Clear();
            AllocateQtyText.Text = "1";
            SetStatus($"{qty:0.##} {selectedRow.Unit} wurden direkt aus dem {SelectedPoolLabel.ToLowerInvariant()} an {customer.Name} / {projectSelection.Name} zugewiesen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Bestandszuweisung fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
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

        var selectedRows = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().Where(r => r.RemainingQuantity > 0m).ToList();
        if (selectedRows.Count == 0)
        {
            SetStatus($"Bitte mindestens eine offene {SelectedPoolLabel.ToLowerInvariant()}position markieren.", StatusMessageType.Warning);
            return;
        }

        foreach (var row in selectedRows)
        {
            var customerUnitPrice = TryParseDecimal(CustomerPriceText.Text, out var enteredPrice)
                ? enteredPrice
                : row.EffectivePurchaseUnitPrice;
            await App.Api.CreateAllocationAsync(row.Line.InvoiceLineId, customer.CustomerId, projectSelection.ProjectId.Value, row.RemainingQuantity, customerUnitPrice, false);
        }

        await LoadLinesAsync();
        CustomerPriceText.Clear();
        SetStatus($"{selectedRows.Count} {SelectedPoolLabel.ToLowerInvariant()}position(en) wurden komplett zugewiesen.", StatusMessageType.Success);
    }

    private async void RemoveFromPool_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = LinesGrid.SelectedItems.OfType<InvoiceLineRow>().ToList();
        if (selectedRows.Count == 0)
        {
            SetStatus($"Bitte mindestens eine {SelectedPoolLabel.ToLowerInvariant()}position markieren.", StatusMessageType.Warning);
            return;
        }

        foreach (var row in selectedRows)
        {
            if (SelectedPoolMode == "generalSmallMaterial")
            {
                await App.Api.SetInvoiceLineGeneralSmallMaterialAsync(row.Line.InvoiceLineId, false);
            }
            else
            {
                await App.Api.SetInvoiceLineInventoryStockAsync(row.Line.InvoiceLineId, false);
            }
        }

        await LoadLinesAsync();
        SetStatus($"{selectedRows.Count} Position(en) wurden aus dem {SelectedPoolLabel.ToLowerInvariant()} entfernt und stehen wieder unter 'Positionen zuweisen' zur Verfuegung.", StatusMessageType.Success);
    }

    private async Task LoadCustomersAsync(int? selectedCustomerId = null)
    {
        var customers = await App.Api.GetCustomersAsync(activeProjectsOnly: true);
        CustomerCombo.ItemsSource = customers;
        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            return;
        }

        var preferredCustomerId = App.SelectedCustomerId ?? selectedCustomerId;
        var customer = preferredCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == preferredCustomerId.Value) ?? customers[0] : customers[0];
        CustomerCombo.SelectedItem = customer;
        App.SetSelectedCustomer(customer.CustomerId);
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
            SetStatus("Dieser Kunde hat noch keine aktiven Projekte.", StatusMessageType.Warning);
            return;
        }

        ProjectCombo.SelectedItem = selectedProjectId.HasValue ? projects.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) ?? projects[0] : projects[0];
    }

    private async Task LoadLinesAsync()
    {
        _allLines = await App.Api.GetInvoiceLineRowsAsync(ShowCompletedCheckBox.IsChecked == true, SelectedPoolMode);
        ApplySearchFilter();
        SetStatus($"{_allLines.Count} {SelectedPoolLabel.ToLowerInvariant()}position(en) geladen.", StatusMessageType.Info);
    }

    private void ApplySearchFilter()
    {
        var query = (SearchTextBox.Text ?? string.Empty).Trim();
        IEnumerable<InvoiceLineRow> filtered = _allLines;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(x =>
                Contains(x.ArticleNumber, query)
                || Contains(x.Ean, query)
                || Contains(x.Description, query)
                || Contains(x.InvoiceNumber, query));
        }

        LinesGrid.ItemsSource = filtered.ToList();
    }

    private static bool Contains(string? source, string query)
        => !string.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void ApplyPoolTexts()
    {
        var isSmallMaterial = SelectedPoolMode == "generalSmallMaterial";
        SearchLabelText.Text = isSmallMaterial ? "Kleinmaterial durchsuchen" : "Bestand durchsuchen";
        PoolSectionTitleText.Text = isSmallMaterial ? "Kleinmaterialpositionen" : "Bestandspositionen";
        AllocateButton.Content = isSmallMaterial ? "Aus KM zuweisen" : "Aus Bestand zuweisen";
        RemoveButton.Content = isSmallMaterial ? "Aus KM nehmen" : "Aus Bestand nehmen";
        RemainingColumn.Header = isSmallMaterial ? "Noch als KM offen" : "Noch im Bestand";
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
