using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class LoginWindow : Window
{
    private AuthBootstrapStateViewModel? _bootstrapState;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        ToggleBusy(true);
        try
        {
            App.ClearSession();
            _bootstrapState = await App.Api.GetBootstrapStateAsync();
            var hasUsers = _bootstrapState.HasUsers;
            LoginPanel.Visibility = hasUsers ? Visibility.Visible : Visibility.Collapsed;
            BootstrapPanel.Visibility = hasUsers ? Visibility.Collapsed : Visibility.Visible;
            IntroText.Text = hasUsers
                ? "Melde dich an oder aktiviere mit einem Lizenzcode eine neue Firma."
                : "Fuer diesen Server wird jetzt zuerst der erste Plattform-Admin eingerichtet.";
            SetStatus(hasUsers ? "Bitte anmelden oder einen Aktivierungscode verwenden." : "Es wurde noch kein Benutzer gefunden. Bitte zuerst den ersten Plattform-Admin anlegen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Backend konnte nicht erreicht werden: {ex.Message}", StatusMessageType.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var email = (LoginEmailText.Text ?? string.Empty).Trim();
        var password = LoginPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Bitte E-Mail-Adresse und Passwort eingeben.", StatusMessageType.Warning);
            return;
        }

        await ExecuteAuthAsync(() => App.Api.LoginAsync(email, password), "Anmeldung erfolgreich.");
    }

    private async void ActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        var activationCode = (ActivationCodeText.Text ?? string.Empty).Trim();
        var tenantName = (ActivationTenantText.Text ?? string.Empty).Trim();
        var displayName = (ActivationDisplayNameText.Text ?? string.Empty).Trim();
        var email = (ActivationEmailText.Text ?? string.Empty).Trim();
        var password = ActivationPasswordBox.Password;
        var confirmPassword = ActivationPasswordConfirmBox.Password;

        if (string.IsNullOrWhiteSpace(activationCode) || string.IsNullOrWhiteSpace(tenantName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Bitte alle Pflichtfelder fuer die Lizenzaktivierung ausfuellen.", StatusMessageType.Warning);
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            SetStatus("Die Passwoerter stimmen nicht ueberein.", StatusMessageType.Error);
            return;
        }

        await ExecuteAuthAsync(() => App.Api.ActivateLicenseAsync(activationCode, tenantName, displayName, email, password), "Lizenz aktiviert und Firma erfolgreich eingerichtet.");
    }

    private async void Bootstrap_Click(object sender, RoutedEventArgs e)
    {
        var tenantName = (BootstrapTenantText.Text ?? string.Empty).Trim();
        var displayName = (BootstrapDisplayNameText.Text ?? string.Empty).Trim();
        var email = (BootstrapEmailText.Text ?? string.Empty).Trim();
        var password = BootstrapPasswordBox.Password;
        var confirmPassword = BootstrapPasswordConfirmBox.Password;

        if (string.IsNullOrWhiteSpace(tenantName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Bitte alle Pflichtfelder fuer den ersten Admin ausfuellen.", StatusMessageType.Warning);
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            SetStatus("Die Passwoerter stimmen nicht ueberein.", StatusMessageType.Error);
            return;
        }

        await ExecuteAuthAsync(() => App.Api.BootstrapAdminAsync(tenantName, displayName, email, password), "Plattform-Admin wurde angelegt und angemeldet.");
    }

    private async Task ExecuteAuthAsync(Func<Task<AuthSessionViewModel>> action, string successMessage)
    {
        ToggleBusy(true);
        try
        {
            var session = await action();
            App.SetSession(session);
            SetStatus(successMessage, StatusMessageType.Success);
            DialogResult = true;
            Close();
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Anmeldung fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusMessageType.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ToggleBusy(bool isBusy)
    {
        LoginButton.IsEnabled = !isBusy;
        ActivateLicenseButton.IsEnabled = !isBusy;
        CancelLoginButton.IsEnabled = !isBusy;
        BootstrapButton.IsEnabled = !isBusy;
        CancelBootstrapButton.IsEnabled = !isBusy;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
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
