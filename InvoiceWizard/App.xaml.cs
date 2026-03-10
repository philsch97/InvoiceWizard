using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace InvoiceWizard;

public partial class App : Application
{
    public static Services.BackendApiClient Api { get; } = new();

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
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
