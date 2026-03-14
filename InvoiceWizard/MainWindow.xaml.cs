using System.Windows;
using System.Windows.Controls;

namespace InvoiceWizard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplySessionInfo();
        NavigateTo(CreateMaterialSection(), MaterialButton);
    }

    private void Material_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(CreateMaterialSection(), MaterialButton);
    }

    private void MasterData_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(CreateMasterDataSection(), MasterDataButton);
    }

    private void Organisation_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(CreateOrganisationSection(), OrganisationButton);
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(CreateAdminSection(), AdminButton);
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
            NavigateTo(CreateMaterialSection(), MaterialButton);
            return;
        }

        Close();
    }

    private SectionHostPage CreateMaterialSection()
    {
        return new SectionHostPage(
            "Material",
            "Hier findest du alle Bereiche rund um Materialimport, Zuordnung, Export und Arbeitszeit.",
            new[]
            {
                new SectionNavigationItem { Title = "Rechnungsimport", CreatePage = () => new Datenimport() },
                new SectionNavigationItem { Title = "Positionen zuweisen", CreatePage = () => new DataHandling() },
                new SectionNavigationItem { Title = "Abrechnung / Export", CreatePage = () => new BillingExportPage() },
                new SectionNavigationItem { Title = "Arbeitszeit", CreatePage = () => new WorkTimePage() }
            });
    }

    private SectionHostPage CreateMasterDataSection()
    {
        return new SectionHostPage(
            "Stammdaten",
            "Kunden, Projekte und die dazugehoerigen Stammdaten verwaltest du gesammelt in diesem Bereich.",
            new[]
            {
                new SectionNavigationItem { Title = "Kundenpflege", CreatePage = () => new CustomerHandling() },
                new SectionNavigationItem { Title = "Projektdaten", CreatePage = () => new ProjectContactsPage() }
            });
    }

    private SectionHostPage CreateOrganisationSection()
    {
        return new SectionHostPage(
            "Organisation",
            "Plane Termine und sammle Notizen an einer Stelle.",
            new[]
            {
                new SectionNavigationItem { Title = "Kalender", CreatePage = () => new CalendarPage() },
                new SectionNavigationItem { Title = "Notizen", CreatePage = () => new NotesPage() }
            });
    }

    private SectionHostPage CreateAdminSection()
    {
        return new SectionHostPage(
            "Admin",
            "Hier verwaltest du dein Abo und die Benutzer deiner Firma.",
            new[]
            {
                new SectionNavigationItem { Title = "Abo / Lizenz", CreatePage = () => new SubscriptionPage() },
                new SectionNavigationItem { Title = "Benutzer", CreatePage = () => new TenantUsersPage() }
            });
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
        AdminButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateNavigationState(Button activeButton)
    {
        var primaryStyle = (Style)FindResource(typeof(Button));
        var secondaryStyle = (Style)FindResource("SecondaryButtonStyle");

        foreach (var button in new[] { MaterialButton, MasterDataButton, OrganisationButton, AdminButton, AnalyticsButton })
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
