using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class TenantUsersPage : Page
{
    private readonly string[] _roles = ["Employee", "Admin"];
    private List<TenantUserViewModel> _users = [];
    private TenantUserViewModel? _selectedUser;

    public TenantUsersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        CreateRoleCombo.ItemsSource = _roles;
        EditRoleCombo.ItemsSource = _roles;
        CreateRoleCombo.SelectedItem = "Employee";
        EditRoleCombo.SelectedItem = "Employee";
        ApplySessionInfo();
        await LoadUsersAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
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

            if (_users.Count == 0)
            {
                SetStatus("Noch keine weiteren Benutzer angelegt.", StatusMessageType.Info);
            }
            else
            {
                SetStatus($"{_users.Count} Benutzer fuer diese Firma geladen.", StatusMessageType.Info);
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
        HeaderText.Text = $"Admins koennen hier Team-Mitglieder der Firma {tenantName} anlegen, Rollen vergeben und Zugriffe deaktivieren. Aktueller Tarif: {planName}.";
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

    private static string NormalizeRole(string role)
        => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Employee";

    private static string GetFriendlyError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("409"))
        {
            return "Der Benutzer konnte nicht angelegt oder aktualisiert werden. Bitte E-Mail, Benutzerlimit und Rolle pruefen.";
        }

        if (message.Contains("403"))
        {
            return "Dieser Zugriff ist nur fuer Admins erlaubt.";
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
