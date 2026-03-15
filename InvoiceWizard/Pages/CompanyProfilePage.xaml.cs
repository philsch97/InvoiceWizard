using InvoiceWizard.Data.Entities;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class CompanyProfilePage : Page
{
    public CompanyProfilePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadProfileAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadProfileAsync();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse((NextInvoiceNumberText.Text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nextInvoiceNumber) || nextInvoiceNumber <= 0)
        {
            SetStatus("Bitte eine gueltige naechste Rechnungsnummer eingeben.", StatusMessageType.Error);
            return;
        }

        if (!int.TryParse((NextCustomerNumberText.Text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nextCustomerNumber) || nextCustomerNumber <= 0)
        {
            SetStatus("Bitte eine gueltige naechste Kundennummer eingeben.", StatusMessageType.Error);
            return;
        }

        try
        {
            var saved = await App.Api.SaveCompanyProfileAsync(new CompanyProfileEntity
            {
                CompanyName = (CompanyNameText.Text ?? string.Empty).Trim(),
                CompanyStreet = (CompanyStreetText.Text ?? string.Empty).Trim(),
                CompanyHouseNumber = (CompanyHouseNumberText.Text ?? string.Empty).Trim(),
                CompanyPostalCode = (CompanyPostalCodeText.Text ?? string.Empty).Trim(),
                CompanyCity = (CompanyCityText.Text ?? string.Empty).Trim(),
                CompanyEmailAddress = (CompanyEmailText.Text ?? string.Empty).Trim(),
                CompanyPhoneNumber = (CompanyPhoneText.Text ?? string.Empty).Trim(),
                TaxNumber = (TaxNumberText.Text ?? string.Empty).Trim(),
                BankName = (BankNameText.Text ?? string.Empty).Trim(),
                BankIban = (BankIbanText.Text ?? string.Empty).Trim(),
                BankBic = (BankBicText.Text ?? string.Empty).Trim(),
                NextRevenueInvoiceNumber = nextInvoiceNumber,
                NextCustomerNumber = nextCustomerNumber
            });

            FillForm(saved);
            SetStatus("Firmendaten gespeichert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Firmendaten konnten nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            var profile = await App.Api.GetCompanyProfileAsync();
            FillForm(profile);
            SetStatus("Firmendaten geladen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Firmendaten konnten nicht geladen werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void FillForm(CompanyProfileEntity profile)
    {
        CompanyNameText.Text = profile.CompanyName;
        CompanyStreetText.Text = profile.CompanyStreet;
        CompanyHouseNumberText.Text = profile.CompanyHouseNumber;
        CompanyPostalCodeText.Text = profile.CompanyPostalCode;
        CompanyCityText.Text = profile.CompanyCity;
        CompanyEmailText.Text = profile.CompanyEmailAddress;
        CompanyPhoneText.Text = profile.CompanyPhoneNumber;
        TaxNumberText.Text = profile.TaxNumber;
        BankNameText.Text = profile.BankName;
        BankIbanText.Text = profile.BankIban;
        BankBicText.Text = profile.BankBic;
        NextInvoiceNumberText.Text = profile.NextRevenueInvoiceNumber.ToString(CultureInfo.InvariantCulture);
        NextCustomerNumberText.Text = profile.NextCustomerNumber.ToString(CultureInfo.InvariantCulture);
        InvoicePreviewText.Text = $"Naechste automatisch vergebene Rechnungsnummer: {profile.RevenueInvoiceNumberPreview}";
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
