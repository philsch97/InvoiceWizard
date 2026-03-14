using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class SubscriptionPage : Page
{
    private ManagedTenantLicenseViewModel? _license;
    private List<SubscriptionPlanViewModel> _plans = [];

    public SubscriptionPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _plans = await App.Api.GetTenantSubscriptionPlansAsync();
            PlanCombo.ItemsSource = _plans;
            _license = await App.Api.GetCurrentTenantLicenseAsync();
            Populate(_license);
            SetStatus("Aktuelle Lizenzdaten geladen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusMessageType.Error);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async void SavePlan_Click(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(App.Session?.User?.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Nur Admins duerfen das Abo-Modell aendern.", StatusMessageType.Warning);
            return;
        }

        if (PlanCombo.SelectedItem is not SubscriptionPlanViewModel selectedPlan)
        {
            SetStatus("Bitte zuerst ein Abo-Modell auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            _license = await App.Api.UpdateCurrentTenantPlanAsync(selectedPlan.Code);
            Populate(_license);
            SetStatus($"Das Abo-Modell wurde auf {selectedPlan.Name} umgestellt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusMessageType.Error);
        }
    }

    private async void CancelSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(App.Session?.User?.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Nur Admins duerfen ein Abo kuendigen.", StatusMessageType.Warning);
            return;
        }

        var result = MessageBox.Show(
            "Willst du das Abo wirklich kuendigen? Die Firma bleibt nur noch bis zum aktuellen Laufzeitende nutzbar.",
            "Abo kuendigen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _license = await App.Api.CancelCurrentTenantLicenseAsync();
            Populate(_license);
            SetStatus("Das Abo wurde zur Kuendigung vorgemerkt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusMessageType.Error);
        }
    }

    private void Populate(ManagedTenantLicenseViewModel license)
    {
        TenantNameText.Text = license.TenantName;
        PlanText.Text = license.PlanName;
        BillingText.Text = $"{GetBillingCycleLabel(license.BillingCycle)} / {license.PriceNet:N2} EUR netto";
        StatusTextBox.Text = license.Status;
        PlanCombo.SelectedItem = _plans.FirstOrDefault(x => string.Equals(x.Code, license.PlanCode, StringComparison.OrdinalIgnoreCase));
        ValidUntilText.Text = FormatDate(license.ValidUntil);
        NextBillingDateText.Text = FormatDate(license.NextBillingDate);
        GraceUntilText.Text = FormatDate(license.GraceUntil);
        CancelledAtText.Text = FormatDate(license.CancelledAt);
        HintText.Text = license.CancelledAt.HasValue
            ? "Dein Abo ist bereits gekuendigt. Es laeuft noch bis zum hinterlegten Enddatum weiter."
            : "Wenn du kuendigst, wird die automatische Verlaengerung deaktiviert und das Abo endet zum aktuellen Laufzeitende.";
        CancelSubscriptionButton.Visibility = string.Equals(App.Session?.User?.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
        CancelSubscriptionButton.IsEnabled = !license.CancelledAt.HasValue;
        SavePlanButton.Visibility = CancelSubscriptionButton.Visibility;
    }

    private static string FormatDate(DateTime? value)
        => value.HasValue ? value.Value.ToLocalTime().ToString("dd.MM.yyyy") : "-";

    private static string GetBillingCycleLabel(string? billingCycle)
        => string.Equals(billingCycle, "Yearly", StringComparison.OrdinalIgnoreCase)
            ? "Jaehrlich"
            : string.Equals(billingCycle, "Manual", StringComparison.OrdinalIgnoreCase)
                ? "Manuell"
                : "Monatlich";

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
