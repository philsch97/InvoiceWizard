using System.Text;
using System.Windows;
using System.Windows.Threading;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class App : Application
{
    public static Services.BackendApiClient Api { get; } = new();
    public static AuthSessionViewModel? Session { get; private set; }

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    public static void SetSession(AuthSessionViewModel session)
    {
        Session = session;
        Api.SetAccessToken(session.AccessToken);
    }

    public static void ClearSession()
    {
        Session = null;
        Api.SetAccessToken(null);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            if (!await ShowLoginAsync())
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(BuildErrorMessage(ex), "InvoiceWizard Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    public static Task<bool> ShowLoginAsync()
    {
        var loginWindow = new LoginWindow();
        var result = loginWindow.ShowDialog();
        return Task.FromResult(result == true && Session is not null);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(BuildErrorMessage(e.Exception), "InvoiceWizard Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string BuildErrorMessage(Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Die Anwendung hat einen unerwarteten Fehler festgestellt.");
        builder.AppendLine();
        builder.AppendLine(exception.Message);

        if (exception.InnerException != null)
        {
            builder.AppendLine();
            builder.AppendLine("Details:");
            builder.AppendLine(exception.InnerException.Message);
        }

        return builder.ToString();
    }
}
