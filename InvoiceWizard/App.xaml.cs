using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard;

public partial class App : Application
{
    public static Services.BackendApiClient Api { get; } = new();
    public static Services.DatanormCatalogService DatanormCatalog { get; } = new();
    public static AuthSessionViewModel? Session { get; private set; }
    public static int? SelectedCustomerId { get; private set; }

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
        SelectedCustomerId = null;
        Api.SetAccessToken(null);
    }

    public static void SetSelectedCustomer(int? customerId)
    {
        SelectedCustomerId = customerId;
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

    private void HandleScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        var maxOffset = viewer.ScrollableHeight;
        if (maxOffset <= 0)
        {
            BubbleMouseWheel(viewer, e);
            return;
        }

        var scrollingUp = e.Delta > 0;
        var atTop = viewer.VerticalOffset <= 0;
        var atBottom = viewer.VerticalOffset >= maxOffset;

        if ((scrollingUp && atTop) || (!scrollingUp && atBottom))
        {
            BubbleMouseWheel(viewer, e);
            return;
        }

        var nextOffset = viewer.VerticalOffset - (e.Delta / 3d);
        viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(maxOffset, nextOffset)));
        e.Handled = true;
    }

    private static void BubbleMouseWheel(DependencyObject source, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var parent = FindVisualParent<UIElement>(source);
        if (parent is null)
        {
            return;
        }

        var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = source
        };

        parent.RaiseEvent(forwardedEvent);
    }

    private static TParent? FindVisualParent<TParent>(DependencyObject? child)
        where TParent : DependencyObject
    {
        while (child is not null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is TParent parent)
            {
                return parent;
            }
        }

        return null;
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
