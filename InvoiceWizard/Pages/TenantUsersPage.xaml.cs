using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class TenantUsersPage : Page
{
    private readonly string[] _roles = ["Employee", "Admin"];
    private readonly string[] _billingCycles = ["Monthly", "Yearly", "Manual"];
    private List<TenantUserViewModel> _users = [];
    private List<SubscriptionPlanViewModel> _plans = [];
    private List<LicenseActivationViewModel> _licenseActivations = [];
    private List<ManagedTenantLicenseViewModel> _managedLicenses = [];
    private TenantUserViewModel? _selectedUser;
    private ManagedTenantLicenseViewModel? _selectedManagedLicense;

    public TenantUsersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        CreateRoleCombo.ItemsSource = _roles;
        EditRoleCombo.ItemsSource = _roles;
        ActivationBillingCycleCombo.ItemsSource = _billingCycles;
        ManagedBillingCycleCombo.ItemsSource = _billingCycles;
        CreateRoleCombo.SelectedItem = "Employee";
        EditRoleCombo.SelectedItem = "Employee";
        ActivationBillingCycleCombo.SelectedItem = "Monthly";
        ManagedBillingCycleCombo.SelectedItem = "Monthly";
        ActivationPriceNetText.Text = "0,00";
        ActivationRenewsAutomaticallyCheck.IsChecked = true;
        ManagedPriceNetText.Text = "0,00";
        ManagedIsActiveCheck.IsChecked = true;
        ApplySessionInfo();
        await LoadUsersAsync();
        await LoadLicensingAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
        await LoadLicensingAsync();
    }

    private async void CreateUser_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAdmin())
        {
            return;
        }

        var displayName = (CreateDisplayNameText.Text ?? string.Empty).Trim();
        var email = (CreateEmailText.Text ?? string.Empty).Trim();
        var password = CreatePasswordBox.Password;
        var role = CreateRoleCombo.SelectedItem as string ?? "Employee";

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Bitte Name, E-Mail-Adresse und Passwort fuer den neuen Benutzer ausfuellen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            await App.Api.CreateTenantUserAsync(new CreateTenantUserViewModel
            {
                DisplayName = displayName,
                Email = email,
                Password = password,
                Role = role
            });

            ClearCreateForm();
            await LoadUsersAsync();
            SetStatus($"Benutzer {displayName} wurde angelegt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private async void UpdateUser_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAdmin())
        {
            return;
        }

        if (_selectedUser is null)
        {
            SetStatus("Bitte zuerst einen Benutzer in der Liste auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var displayName = (EditDisplayNameText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Der Anzeigename darf nicht leer sein.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var updated = await App.Api.UpdateTenantUserAsync(_selectedUser.AppUserId, new UpdateTenantUserViewModel
            {
                DisplayName = displayName,
                Role = EditRoleCombo.SelectedItem as string ?? "Employee",
                IsActive = EditIsActiveCheck.IsChecked == true,
                Password = string.IsNullOrWhiteSpace(EditPasswordBox.Password) ? null : EditPasswordBox.Password
            });

            _selectedUser = updated;
            await LoadUsersAsync(updated.AppUserId);
            EditPasswordBox.Clear();
            SetStatus($"Benutzer {updated.DisplayName} wurde aktualisiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private async void CreateLicenseActivation_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsurePlatformAdmin())
        {
            return;
        }

        if (PlanCombo.SelectedItem is not SubscriptionPlanViewModel plan)
        {
            SetStatus("Bitte zuerst einen Tarif auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseNullableInt(MaxUsersOverrideText.Text, out var maxUsers) || !TryParseNullableInt(MaxProjectsOverrideText.Text, out var maxProjects) || !TryParseNullableInt(MaxCustomersOverrideText.Text, out var maxCustomers))
        {
            SetStatus("Die Lizenz-Overrides muessen leere Felder oder ganze Zahlen sein.", StatusMessageType.Warning);
            return;
        }

        if (!TryParsePrice(ActivationPriceNetText.Text, out var priceNet))
        {
            SetStatus("Bitte fuer den Netto-Preis eine gueltige Zahl eingeben.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var item = await App.Api.CreateLicenseActivationAsync(
                plan.Code,
                (ActivationCustomerEmailText.Text ?? string.Empty).Trim(),
                ActivationValidUntilPicker.SelectedDate,
                maxUsers,
                maxProjects,
                maxCustomers,
                MobileAccessOverrideCheck.IsChecked,
                ActivationBillingCycleCombo.SelectedItem as string ?? "Monthly",
                priceNet,
                ActivationRenewsAutomaticallyCheck.IsChecked == true);

            ClearLicenseForm();
            await LoadLicensingAsync();
            SetStatus($"Aktivierungscode {item.ActivationCode} wurde erzeugt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private void ManagedLicensesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ManagedLicensesGrid.SelectedItem is ManagedTenantLicenseViewModel item)
        {
            ApplySelectedManagedLicense(item);
            return;
        }

        ClearManagedLicenseSelection();
    }

    private async void SaveManagedLicense_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsurePlatformAdmin())
        {
            return;
        }

        if (_selectedManagedLicense is null)
        {
            SetStatus("Bitte zuerst eine Kundenlizenz auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ManagedPlanCombo.SelectedItem is not SubscriptionPlanViewModel selectedPlan)
        {
            SetStatus("Bitte einen Tarif fuer die Kundenlizenz auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (!TryParseNullableInt(ManagedMaxUsersText.Text, out var maxUsers) || !TryParseNullableInt(ManagedMaxProjectsText.Text, out var maxProjects) || !TryParseNullableInt(ManagedMaxCustomersText.Text, out var maxCustomers))
        {
            SetStatus("Die Lizenz-Limits muessen ganze Zahlen sein.", StatusMessageType.Warning);
            return;
        }

        if (!TryParsePrice(ManagedPriceNetText.Text, out var priceNet))
        {
            SetStatus("Bitte fuer den Netto-Preis eine gueltige Zahl eingeben.", StatusMessageType.Warning);
            return;
        }

        try
        {
            _selectedManagedLicense.PlanCode = selectedPlan.Code;
            _selectedManagedLicense.BillingCycle = ManagedBillingCycleCombo.SelectedItem as string ?? "Monthly";
            _selectedManagedLicense.PriceNet = priceNet;
            _selectedManagedLicense.RenewsAutomatically = ManagedRenewsAutomaticallyCheck.IsChecked == true;
            _selectedManagedLicense.ValidUntil = ManagedValidUntilPicker.SelectedDate;
            _selectedManagedLicense.NextBillingDate = ManagedNextBillingDatePicker.SelectedDate;
            _selectedManagedLicense.CancelledAt = ManagedCancelledAtPicker.SelectedDate;
            _selectedManagedLicense.GraceUntil = ManagedGraceUntilPicker.SelectedDate;
            _selectedManagedLicense.MaxUsers = maxUsers ?? selectedPlan.MaxUsers;
            _selectedManagedLicense.MaxProjects = maxProjects ?? selectedPlan.MaxProjects;
            _selectedManagedLicense.MaxCustomers = maxCustomers ?? selectedPlan.MaxCustomers;
            _selectedManagedLicense.IncludesMobileAccess = ManagedIncludesMobileAccessCheck.IsChecked == true;
            _selectedManagedLicense.IsActive = ManagedIsActiveCheck.IsChecked == true;

            var updated = await App.Api.UpdateManagedTenantLicenseAsync(_selectedManagedLicense);
            await LoadLicensingAsync(updated.TenantLicenseId);
            SetStatus($"Lizenz fuer {updated.TenantName} wurde gespeichert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UsersGrid.SelectedItem is TenantUserViewModel user)
        {
            ApplySelectedUser(user);
            return;
        }

        ClearSelection();
    }

    private void ClearCreateForm_Click(object sender, RoutedEventArgs e)
    {
        ClearCreateForm();
    }

    private void ClearLicenseForm_Click(object sender, RoutedEventArgs e)
    {
        ClearLicenseForm();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        UsersGrid.SelectedItem = null;
        ClearSelection();
    }

    private async Task LoadUsersAsync(int? selectedAppUserId = null)
    {
        try
        {
            _users = await App.Api.GetTenantUsersAsync();
            UsersGrid.ItemsSource = _users;

            var target = selectedAppUserId.HasValue
                ? _users.FirstOrDefault(x => x.AppUserId == selectedAppUserId.Value)
                : _users.FirstOrDefault(x => x.AppUserId == _selectedUser?.AppUserId);

            UsersGrid.SelectedItem = target;
            if (target is null)
            {
                ClearSelection();
            }
            else
            {
                ApplySelectedUser(target);
            }

            var activeUsers = _users.Count(x => x.IsActive);
            var maxUsers = App.Session?.License?.MaxUsers ?? 0;
            LicenseText.Text = maxUsers > 0
                ? $"Tarif {App.Session?.License?.PlanName ?? App.Session?.License?.PlanCode}: {activeUsers} von {maxUsers} Benutzern aktiv."
                : $"Aktive Benutzer: {activeUsers}.";

            SetStatus(_users.Count == 0 ? "Noch keine weiteren Benutzer angelegt." : $"{_users.Count} Benutzer fuer diese Firma geladen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private async Task LoadLicensingAsync(int? selectedTenantLicenseId = null)
    {
        var isPlatformAdmin = App.Session?.User?.IsPlatformAdmin == true;
        LicensingSectionBorder.Visibility = isPlatformAdmin ? Visibility.Visible : Visibility.Collapsed;
        ManagedLicensesBorder.Visibility = isPlatformAdmin ? Visibility.Visible : Visibility.Collapsed;
        if (!isPlatformAdmin)
        {
            return;
        }

        try
        {
            _plans = await App.Api.GetSubscriptionPlansAsync();
            _licenseActivations = await App.Api.GetLicenseActivationsAsync();
            _managedLicenses = await App.Api.GetManagedTenantLicensesAsync();
            PlanCombo.ItemsSource = _plans;
            ManagedPlanCombo.ItemsSource = _plans;
            PlanCombo.SelectedItem = _plans.FirstOrDefault(x => string.Equals(x.Code, "business", StringComparison.OrdinalIgnoreCase)) ?? _plans.FirstOrDefault();
            LicenseActivationsGrid.ItemsSource = _licenseActivations;
            ManagedLicensesGrid.ItemsSource = _managedLicenses;
            ManagedLicensesHintText.Text = "Hier verwaltest du laufende Kundenabos manuell: Tarif, Preis, Laufzeit, Auto-Verlaengerung, Kuendigung und Grace-Phase.";
            LicenseAdminText.Text = "Nur der Plattform-Admin kann neue Aktivierungscodes fuer Kunden erzeugen. Diese Codes werden einmalig von deinen Kunden im Loginfenster aktiviert und erzeugen danach eine laufende Firmenlizenz.";

            var target = selectedTenantLicenseId.HasValue
                ? _managedLicenses.FirstOrDefault(x => x.TenantLicenseId == selectedTenantLicenseId.Value)
                : _managedLicenses.FirstOrDefault(x => x.TenantLicenseId == _selectedManagedLicense?.TenantLicenseId);
            ManagedLicensesGrid.SelectedItem = target;
            if (target is null)
            {
                ClearManagedLicenseSelection();
            }
            else
            {
                ApplySelectedManagedLicense(target);
            }
        }
        catch (Exception ex)
        {
            SetStatus(GetFriendlyError(ex), StatusMessageType.Error);
        }
    }

    private void ApplySessionInfo()
    {
        var tenantName = App.Session?.Tenant?.Name ?? "Unbekannte Firma";
        var planName = App.Session?.License?.PlanName ?? App.Session?.License?.PlanCode ?? "Lizenz";
        var billingText = App.Session?.License is null ? string.Empty : $" Abo: {GetBillingCycleLabel(App.Session.License.BillingCycle)} / {App.Session.License.PriceNet:N2} EUR netto.";
        HeaderText.Text = App.Session?.User?.IsPlatformAdmin == true
            ? $"Du verwaltest die Plattform-Firma {tenantName}. Neben Benutzern kannst du hier auch Aktivierungscodes und laufende Kundenabos verwalten. Aktueller Tarif: {planName}.{billingText}"
            : $"Admins koennen hier Team-Mitglieder der Firma {tenantName} anlegen, Rollen vergeben und Zugriffe deaktivieren. Aktueller Tarif: {planName}.{billingText}";
    }

    private void ApplySelectedUser(TenantUserViewModel user)
    {
        _selectedUser = user;
        EditDisplayNameText.Text = user.DisplayName;
        EditEmailText.Text = user.Email;
        EditRoleCombo.SelectedItem = NormalizeRole(user.Role);
        EditIsActiveCheck.IsChecked = user.IsActive;
        EditPasswordBox.Clear();
        SelectionHintText.Text = user.IsDefault
            ? "Standardzugang der Firma. E-Mail ist fest, Rolle und Aktiv-Status koennen angepasst werden."
            : "E-Mail ist fix. Fuer ein neues Passwort einfach ein Feld ausfuellen und speichern.";
    }

    private void ClearCreateForm()
    {
        CreateDisplayNameText.Clear();
        CreateEmailText.Clear();
        CreatePasswordBox.Clear();
        CreateRoleCombo.SelectedItem = "Employee";
    }

    private void ClearLicenseForm()
    {
        ActivationCustomerEmailText.Clear();
        ActivationValidUntilPicker.SelectedDate = null;
        MaxUsersOverrideText.Clear();
        MaxProjectsOverrideText.Clear();
        MaxCustomersOverrideText.Clear();
        ActivationBillingCycleCombo.SelectedItem = "Monthly";
        ActivationPriceNetText.Text = "0,00";
        MobileAccessOverrideCheck.IsChecked = true;
        ActivationRenewsAutomaticallyCheck.IsChecked = true;
        PlanCombo.SelectedItem = _plans.FirstOrDefault(x => string.Equals(x.Code, "business", StringComparison.OrdinalIgnoreCase)) ?? _plans.FirstOrDefault();
    }

    private void ApplySelectedManagedLicense(ManagedTenantLicenseViewModel item)
    {
        _selectedManagedLicense = item;
        ManagedTenantNameText.Text = item.TenantName;
        ManagedPlanCombo.SelectedItem = _plans.FirstOrDefault(x => string.Equals(x.Code, item.PlanCode, StringComparison.OrdinalIgnoreCase));
        ManagedBillingCycleCombo.SelectedItem = item.BillingCycle;
        ManagedPriceNetText.Text = item.PriceNet.ToString("0.00", CultureInfo.InvariantCulture);
        ManagedValidUntilPicker.SelectedDate = item.ValidUntil;
        ManagedNextBillingDatePicker.SelectedDate = item.NextBillingDate;
        ManagedCancelledAtPicker.SelectedDate = item.CancelledAt;
        ManagedGraceUntilPicker.SelectedDate = item.GraceUntil;
        ManagedMaxUsersText.Text = item.MaxUsers.ToString(CultureInfo.InvariantCulture);
        ManagedMaxProjectsText.Text = item.MaxProjects.ToString(CultureInfo.InvariantCulture);
        ManagedMaxCustomersText.Text = item.MaxCustomers.ToString(CultureInfo.InvariantCulture);
        ManagedIncludesMobileAccessCheck.IsChecked = item.IncludesMobileAccess;
        ManagedRenewsAutomaticallyCheck.IsChecked = item.RenewsAutomatically;
        ManagedIsActiveCheck.IsChecked = item.IsActive;
        ManagedLicenseSelectionHintText.Text = $"{item.TenantName}: Status {item.Status}, aktuell {GetBillingCycleLabel(item.BillingCycle)} mit {item.PriceNet:N2} EUR netto.";
    }

    private void ClearManagedLicenseSelection()
    {
        _selectedManagedLicense = null;
        ManagedTenantNameText.Clear();
        ManagedPlanCombo.SelectedItem = _plans.FirstOrDefault();
        ManagedBillingCycleCombo.SelectedItem = "Monthly";
        ManagedPriceNetText.Text = "0,00";
        ManagedValidUntilPicker.SelectedDate = null;
        ManagedNextBillingDatePicker.SelectedDate = null;
        ManagedCancelledAtPicker.SelectedDate = null;
        ManagedGraceUntilPicker.SelectedDate = null;
        ManagedMaxUsersText.Clear();
        ManagedMaxProjectsText.Clear();
        ManagedMaxCustomersText.Clear();
        ManagedIncludesMobileAccessCheck.IsChecked = false;
        ManagedRenewsAutomaticallyCheck.IsChecked = false;
        ManagedIsActiveCheck.IsChecked = true;
        ManagedLicenseSelectionHintText.Text = "Waehle links eine Kundenlizenz aus, um Abo-Daten, Laufzeit und Status zu verwalten.";
    }

    private void ClearSelection()
    {
        _selectedUser = null;
        EditDisplayNameText.Clear();
        EditEmailText.Clear();
        EditPasswordBox.Clear();
        EditRoleCombo.SelectedItem = "Employee";
        EditIsActiveCheck.IsChecked = true;
        SelectionHintText.Text = "Waehle einen Benutzer aus der Liste, um Rolle, Aktiv-Status oder Passwort zu aendern.";
    }

    private bool EnsureAdmin()
    {
        if (!string.Equals(App.Session?.User?.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Nur Admins duerfen Benutzer verwalten.", StatusMessageType.Warning);
            return false;
        }

        return true;
    }

    private bool EnsurePlatformAdmin()
    {
        if (App.Session?.User?.IsPlatformAdmin != true)
        {
            SetStatus("Nur der Plattform-Admin darf Aktivierungscodes erzeugen.", StatusMessageType.Warning);
            return false;
        }

        return true;
    }

    private static bool TryParseNullableInt(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParsePrice(string? text, out decimal value)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static string NormalizeRole(string role)
        => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Employee";

    private static string GetBillingCycleLabel(string? billingCycle)
        => string.Equals(billingCycle, "Yearly", StringComparison.OrdinalIgnoreCase)
            ? "Jaehrlich"
            : string.Equals(billingCycle, "Manual", StringComparison.OrdinalIgnoreCase)
                ? "Manuell"
                : "Monatlich";

    private static string GetFriendlyError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("409"))
        {
            return "Der Vorgang konnte nicht abgeschlossen werden. Bitte Limits, E-Mail-Adresse und vorhandene Daten pruefen.";
        }

        if (message.Contains("403"))
        {
            return "Dieser Zugriff ist nur fuer berechtigte Admins erlaubt.";
        }

        if (message.Contains("400"))
        {
            return "Die Eingaben sind nicht vollstaendig oder ungueltig.";
        }

        return message;
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
