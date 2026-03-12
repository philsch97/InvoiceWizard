using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplySessionInfo();
        NavigateTo(new Datenimport(), ImportButton);
    }

    private void Datenimport_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new Datenimport(), ImportButton);
    }

    private void Datenhandling_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new DataHandling(), AllocationButton);
    }

    private void CustomerHandling_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new CustomerHandling(), CustomerButton);
    }

    private void BillingExport_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new BillingExportPage(), BillingExportButton);
    }

    private void ProjectContacts_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new ProjectContactsPage(), ProjectContactsButton);
    }

    private void WorkTime_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new WorkTimePage(), WorkTimeButton);
    }

    private void Notes_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new NotesPage(), NotesButton);
    }

    private void TenantUsers_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new TenantUsersPage(), TenantUsersButton);
    }

    private void Analytics_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new AnalyticsPage(), AnalyticsButton);
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        App.ClearSession();
        Hide();

        if (await App.ShowLoginAsync())
        {
            ApplySessionInfo();
            Show();
            NavigateTo(new Datenimport(), ImportButton);
            return;
        }

        Close();
    }

    private void NavigateTo(Page page, Button activeButton)
    {
        MainFrame.Navigate(page);
        UpdateNavigationState(activeButton);
    }

    private void ApplySessionInfo()
    {
        var session = App.Session;
        TenantInfoText.Text = session is null ? "Nicht angemeldet" : session.Tenant.Name;
        UserInfoText.Text = session is null
            ? string.Empty
            : $"{session.User.DisplayName} | {session.User.Role}";

        var isAdmin = string.Equals(session?.User?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        TenantUsersButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateNavigationState(Button activeButton)
    {
        var primaryStyle = (Style)FindResource(typeof(Button));
        var secondaryStyle = (Style)FindResource("SecondaryButtonStyle");

        foreach (var button in new[] { ImportButton, AllocationButton, CustomerButton, BillingExportButton, ProjectContactsButton, WorkTimeButton, NotesButton, TenantUsersButton, AnalyticsButton })
        {
            if (button.Visibility != Visibility.Visible)
            {
                continue;
            }

            button.Style = button == activeButton ? primaryStyle : secondaryStyle;
            button.Opacity = button == activeButton ? 1.0 : 0.92;
        }
    }
}
