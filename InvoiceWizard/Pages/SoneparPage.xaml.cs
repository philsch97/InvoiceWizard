using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;
using Microsoft.Win32;

namespace InvoiceWizard;

public partial class SoneparPage : Page
{
    private List<SoneparProductViewModel> _products = [];

    public SoneparPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var connection = await App.Api.GetSoneparConnectionAsync();
            ApplyConnection(connection);
            await ReloadDatanormStateAsync();
            SearchSummaryText.Text = connection.IsConfigured
                ? "Sonepar-Zugang ist gespeichert. Du kannst direkt nach Produkten suchen."
                : "Noch keine Sonepar-Zugangsdaten gespeichert.";
            SetStatus(connection.IsConfigured
                ? "Sonepar-Zugang geladen."
                : "Noch kein Sonepar-Zugang fuer diesen Mandanten hinterlegt.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Sonepar-Zugang konnte nicht geladen werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async Task ReloadDatanormStateAsync()
    {
        var state = await App.DatanormCatalog.GetStateAsync();
        if (state.ArticleCount <= 0)
        {
            DatanormSummaryText.Text = "Noch keine DATANORM-Datei importiert.";
            DatanormPreviewText.Text = "Importiere hier die DATANORM-Datei deines Großhändlers. Danach steht die Artikelsuche auch im Rechnungsimport ohne Beleg und auf der neuen Angebotsseite bereit.";
            return;
        }

        DatanormSummaryText.Text = $"{state.ArticleCount} DATANORM-Artikel importiert aus {state.SourceFileName}."
            + (state.ImportedAt.HasValue ? $" Letzter Import: {state.ImportedAt.Value:dd.MM.yyyy HH:mm}." : string.Empty);
        var previewItems = await App.DatanormCatalog.SearchAsync(string.Empty, 5);
        DatanormPreviewText.Text = previewItems.Count == 0
            ? string.Empty
            : "Beispiele: " + string.Join(" | ", previewItems.Select(x => $"{x.ArticleNumber} - {x.Description}"));
    }

    private void ApplyConnection(SoneparConnectionViewModel connection)
    {
        UsernameTextBox.Text = connection.Username;
        PasswordBox.Password = string.Empty;
        CustomerNumberTextBox.Text = string.Empty;
        ClientIdTextBox.Text = string.Empty;
        CustomerNumberMaskedText.Text = string.IsNullOrWhiteSpace(connection.CustomerNumberMasked) ? string.Empty : $"Gespeicherte Kundennummer: {connection.CustomerNumberMasked}";
        ClientIdMaskedText.Text = string.IsNullOrWhiteSpace(connection.ClientIdMasked) ? string.Empty : $"Gespeicherte Client ID: {connection.ClientIdMasked}";
        OrganizationIdTextBox.Text = connection.OrganizationId;
        TokenUrlTextBox.Text = connection.TokenUrl;
        OpenMasterDataBaseUrlTextBox.Text = connection.OpenMasterDataBaseUrl;
        SelectOmdVersion(connection.OmdVersion);
        DeleteConnectionButton.IsEnabled = connection.IsConfigured;
    }

    private void SelectOmdVersion(string version)
    {
        foreach (var item in OmdVersionComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), version, StringComparison.OrdinalIgnoreCase))
            {
                OmdVersionComboBox.SelectedItem = item;
                return;
            }
        }

        OmdVersionComboBox.SelectedIndex = 2;
    }

    private async void SaveConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var connection = new SoneparConnectionViewModel
            {
                Username = UsernameTextBox.Text.Trim(),
                Password = PasswordBox.Password,
                CustomerNumber = CustomerNumberTextBox.Text.Trim(),
                ClientId = ClientIdTextBox.Text.Trim(),
                OrganizationId = OrganizationIdTextBox.Text.Trim(),
                OmdVersion = (OmdVersionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "9.0.1",
                TokenUrl = TokenUrlTextBox.Text.Trim(),
                OpenMasterDataBaseUrl = OpenMasterDataBaseUrlTextBox.Text.Trim()
            };

            var saved = await App.Api.SaveSoneparConnectionAsync(connection);
            ApplyConnection(saved);
            SetStatus("Sonepar-Zugang wurde gespeichert und erfolgreich verifiziert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Sonepar-Zugang konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void DeleteConnection_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Soll der gespeicherte Sonepar-Zugang fuer diesen Mandanten wirklich geloescht werden?",
                "Sonepar abmelden",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await App.Api.DeleteSoneparConnectionAsync();
            ApplyConnection(new SoneparConnectionViewModel());
            ProductsGrid.ItemsSource = null;
            _products = [];
            ShowProduct(null);
            SearchSummaryText.Text = "Sonepar-Zugang wurde entfernt.";
            SetStatus("Sonepar-Zugang wurde fuer diesen Mandanten geloescht.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Sonepar-Zugang konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var searchType = (SearchTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "SupplierPid";
            var query = SearchQueryTextBox.Text.Trim();
            var result = await App.Api.SearchSoneparProductsAsync(searchType, query);
            _products = result.Products.ToList();
            ProductsGrid.ItemsSource = _products;
            ProductsGrid.SelectedItem = _products.FirstOrDefault();
            SearchSummaryText.Text = $"{_products.Count} Produkt(e) fuer \"{result.Query}\" geladen.";
            ShowProduct(ProductsGrid.SelectedItem as SoneparProductViewModel);
            SetStatus("Sonepar-Suche erfolgreich abgeschlossen.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            ProductsGrid.ItemsSource = null;
            _products = [];
            ShowProduct(null);
            SetStatus($"Sonepar-Suche fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void ImportDatanorm_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DATANORM / Textdateien (*.001;*.txt;*.dat;*.csv)|*.001;*.txt;*.dat;*.csv|Alle Dateien (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await App.DatanormCatalog.ImportAsync(dialog.FileName);
            await ReloadDatanormStateAsync();
            SetStatus($"{result.ImportedCount} DATANORM-Artikel aus {result.SourceFileName} importiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"DATANORM-Import fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void OpenDatanormSearch_Click(object sender, RoutedEventArgs e)
    {
        var articles = await App.DatanormCatalog.SearchAsync(string.Empty);
        if (articles.Count == 0)
        {
            SetStatus("Bitte zuerst eine DATANORM-Datei importieren.", StatusMessageType.Warning);
            return;
        }

        var dialog = new DatanormSearchDialog(articles)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private async void ReloadDatanorm_Click(object sender, RoutedEventArgs e)
    {
        await ReloadDatanormStateAsync();
        SetStatus("DATANORM-Katalog neu geladen.", StatusMessageType.Info);
    }

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowProduct(ProductsGrid.SelectedItem as SoneparProductViewModel);
    }

    private void ShowProduct(SoneparProductViewModel? product)
    {
        if (product is null)
        {
            DetailHeaderText.Text = "Noch kein Produkt ausgewaehlt.";
            DetailMetaText.Text = "Waehle links ein Suchergebnis aus.";
            DetailDescriptionText.Text = string.Empty;
            RawJsonTextBox.Text = string.Empty;
            return;
        }

        DetailHeaderText.Text = string.IsNullOrWhiteSpace(product.DescriptionShort)
            ? product.SupplierPid
            : $"{product.DescriptionShort} ({product.SupplierPid})";
        DetailMetaText.Text = $"GTIN: {Fallback(product.Gtin)} | Hersteller: {Fallback(product.ManufacturerName)} | Hersteller-Artikelnr.: {Fallback(product.ManufacturerPartNumber)} | Preis: {product.NetPriceLabel} | Mengeneinheit: {Fallback(product.Unit)}";
        DetailDescriptionText.Text = string.IsNullOrWhiteSpace(product.DescriptionLong) ? "Keine weiteren Produktdetails von Sonepar geliefert." : product.DescriptionLong;
        RawJsonTextBox.Text = product.RawJson;
    }

    private static string Fallback(string value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private void SetStatus(string message, StatusMessageType type)
    {
        StatusText.Text = message;

        (Brush background, Brush border, Brush foreground) = type switch
        {
            StatusMessageType.Success => ((Brush)FindResource("StatusSuccessBackgroundBrush"), (Brush)FindResource("StatusSuccessBorderBrush"), (Brush)FindResource("StatusSuccessTextBrush")),
            StatusMessageType.Warning => ((Brush)FindResource("StatusWarningBackgroundBrush"), (Brush)FindResource("StatusWarningBorderBrush"), (Brush)FindResource("StatusWarningTextBrush")),
            StatusMessageType.Error => ((Brush)FindResource("StatusErrorBackgroundBrush"), (Brush)FindResource("StatusErrorBorderBrush"), (Brush)FindResource("StatusErrorTextBrush")),
            _ => ((Brush)FindResource("StatusInfoBackgroundBrush"), (Brush)FindResource("StatusInfoBorderBrush"), (Brush)FindResource("StatusInfoTextBrush"))
        };

        StatusBorder.Background = background;
        StatusBorder.BorderBrush = border;
        StatusText.Foreground = foreground;
    }
}
