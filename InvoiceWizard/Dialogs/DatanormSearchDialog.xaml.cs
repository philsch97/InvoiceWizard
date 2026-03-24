using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Services;

namespace InvoiceWizard.Dialogs;

public partial class DatanormSearchDialog : Window
{
    private readonly List<DatanormArticleEntity> _allArticles;

    public DatanormSearchDialog(IEnumerable<DatanormArticleEntity> articles, bool requireVat = true)
    {
        InitializeComponent();
        _allArticles = articles.ToList();
        ResultsGrid.ItemsSource = _allArticles;
        MetalSurchargeTextBox.Text = "0";
        VatPercentTextBox.Text = "19";
    }

    public ManualInvoiceLineInput? Result { get; private set; }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ResultsGrid.ItemsSource = _allArticles;
            return;
        }

        var tokens = query.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ResultsGrid.ItemsSource = _allArticles
            .Where(article => tokens.All(token => article.SearchText.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is DatanormArticleEntity article)
        {
            SearchTextBox.Text = string.IsNullOrWhiteSpace(SearchTextBox.Text)
                ? article.Description
                : SearchTextBox.Text;
        }
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ConfirmSelection()
    {
        if (ResultsGrid.SelectedItem is not DatanormArticleEntity article)
        {
            MessageBox.Show("Bitte zuerst einen DATANORM-Artikel auswählen.", "DATANORM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(QuantityTextBox.Text, out var quantity) || quantity <= 0m)
        {
            MessageBox.Show("Bitte eine gültige Menge größer als 0 eingeben.", "DATANORM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(MetalSurchargeTextBox.Text, out var metalSurcharge) || metalSurcharge < 0m)
        {
            MessageBox.Show("Bitte einen gültigen Metallzuschlag eingeben.", "DATANORM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(VatPercentTextBox.Text, out var vatPercent) || vatPercent < 0m)
        {
            MessageBox.Show("Bitte einen gültigen MwSt.-Satz eingeben.", "DATANORM", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new ManualInvoiceLineInput
        {
            ArticleNumber = article.ArticleNumber,
            Ean = article.Ean,
            Description = article.Description,
            Quantity = quantity,
            Unit = article.Unit,
            NetUnitPrice = article.NetPrice,
            MetalSurcharge = metalSurcharge,
            GrossListPrice = article.GrossListPrice,
            GrossUnitPrice = article.GrossListPrice > 0m
                ? PricingHelper.RoundUnitPrice(PricingHelper.NormalizeUnitPrice(article.GrossListPrice, article.PriceBasisQuantity))
                : 0m,
            VatPercent = vatPercent,
            PriceBasisQuantity = article.PriceBasisQuantity <= 0m ? 1m : article.PriceBasisQuantity
        };

        if (Result.GrossUnitPrice > 0m)
        {
            Result.GrossLineTotal = PricingHelper.RoundCurrency(Result.Quantity * Result.GrossUnitPrice);
        }
        else if (vatPercent > 0m)
        {
            Result.GrossLineTotal = PricingHelper.RoundCurrency(Result.LineTotal * (1m + (vatPercent / 100m)));
            Result.GrossUnitPrice = PricingHelper.CalculateGrossUnitPriceFromLineTotal(Result.GrossLineTotal, Result.Quantity);
        }

        DialogResult = true;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
