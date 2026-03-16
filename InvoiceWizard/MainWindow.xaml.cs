using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using InvoiceWizard.Converters;

namespace InvoiceWizard;

public partial class MainWindow : Window
{
    private readonly Dictionary<Button, SectionDefinition> _sections = new();
    private readonly Dictionary<Button, SectionNavigationItem> _subNavigationButtons = new();
    private SectionDefinition? _activeSection;

    public MainWindow()
    {
        InitializeComponent();
        MainFrame.Navigated += MainFrame_Navigated;
        BuildSections();
        ApplySessionInfo();
        NavigateToSection(MaterialButton);
    }

    private void Material_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection(MaterialButton);
    }

    private void MasterData_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection(MasterDataButton);
    }

    private void Organisation_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection(OrganisationButton);
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection(AdminButton);
    }

    private void Analytics_Click(object sender, RoutedEventArgs e)
    {
        _activeSection = null;
        BuildSubNavigation(Array.Empty<SectionNavigationItem>(), null);
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
            NavigateToSection(MaterialButton);
            return;
        }

        Close();
    }

    private void BuildSections()
    {
        _sections[MaterialButton] = new SectionDefinition(
            "Material",
            new[]
            {
                new SectionNavigationItem { Title = "Rechnungsimport", CreatePage = () => new Datenimport() },
                new SectionNavigationItem { Title = "Sonepar", CreatePage = () => new SoneparPage() },
                new SectionNavigationItem { Title = "Rechnungsarchiv", CreatePage = () => new InvoiceArchivePage() },
                new SectionNavigationItem { Title = "Positionen zuweisen", CreatePage = () => new DataHandling() },
                new SectionNavigationItem { Title = "Abrechnung / Export", CreatePage = () => new BillingExportPage() },
                new SectionNavigationItem { Title = "Arbeitszeit", CreatePage = () => new WorkTimePage() }
            });

        _sections[MasterDataButton] = new SectionDefinition(
            "Stammdaten",
            new[]
            {
                new SectionNavigationItem { Title = "Kundenpflege", CreatePage = () => new CustomerHandling() },
                new SectionNavigationItem { Title = "Projektdaten", CreatePage = () => new ProjectContactsPage() }
            });

        _sections[OrganisationButton] = new SectionDefinition(
            "Organisation",
            new[]
            {
                new SectionNavigationItem { Title = "Kalender", CreatePage = () => new CalendarPage() },
                new SectionNavigationItem { Title = "Notizen", CreatePage = () => new NotesPage() }
            });

        _sections[AdminButton] = new SectionDefinition(
            "Admin",
            new[]
            {
                new SectionNavigationItem { Title = "Firmendaten", CreatePage = () => new CompanyProfilePage() },
                new SectionNavigationItem { Title = "Abo / Lizenz", CreatePage = () => new SubscriptionPage() },
                new SectionNavigationItem { Title = "Benutzer", CreatePage = () => new TenantUsersPage() },
                new SectionNavigationItem { Title = "Bank / Konto", CreatePage = () => new BankingPage() }
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

    private void NavigateToSection(Button sectionButton)
    {
        if (!_sections.TryGetValue(sectionButton, out var section))
        {
            return;
        }

        _activeSection = section;
        var initialItem = section.Items.FirstOrDefault();
        BuildSubNavigation(section.Items, initialItem);

        if (initialItem is not null)
        {
            NavigateTo(initialItem.CreatePage(), sectionButton);
        }
    }

    private void BuildSubNavigation(IEnumerable<SectionNavigationItem> items, SectionNavigationItem? activeItem)
    {
        SubNavigationPanel.Children.Clear();
        _subNavigationButtons.Clear();

        foreach (var item in items)
        {
            var button = new Button
            {
                Content = item.Title,
                MinWidth = 150,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(16, 8, 16, 8),
                Style = (Style)FindResource(item == activeItem ? typeof(Button) : "SecondaryButtonStyle"),
                Opacity = item == activeItem ? 1.0 : 0.92
            };
            button.Click += (_, _) => NavigateToSubSection(item, button);
            SubNavigationPanel.Children.Add(button);
            _subNavigationButtons[button] = item;
        }

        SubNavigationPanel.Visibility = _subNavigationButtons.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NavigateToSubSection(SectionNavigationItem item, Button activeButton)
    {
        MainFrame.Navigate(item.CreatePage());
        UpdateNavigationState(GetPrimaryButtonForActiveSection());
        UpdateSubNavigationState(activeButton);
    }

    private Button GetPrimaryButtonForActiveSection()
    {
        return _sections.FirstOrDefault(pair => pair.Value == _activeSection).Key ?? MaterialButton;
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

    private void UpdateSubNavigationState(Button activeButton)
    {
        var primaryStyle = (Style)FindResource(typeof(Button));
        var secondaryStyle = (Style)FindResource("SecondaryButtonStyle");

        foreach (var button in _subNavigationButtons.Keys)
        {
            button.Style = button == activeButton ? primaryStyle : secondaryStyle;
            button.Opacity = button == activeButton ? 1.0 : 0.92;
        }
    }

    private void MainFrame_Navigated(object? sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        if (e.Content is not Page page)
        {
            ClearGlobalStatusBindings();
            return;
        }

        BindGlobalStatus(page);
    }

    private void BindGlobalStatus(Page page)
    {
        ClearGlobalStatusBindings();

        if (page.FindName("StatusBorder") is not Border pageStatusBorder ||
            page.FindName("StatusText") is not TextBlock pageStatusText)
        {
            GlobalStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        pageStatusBorder.Visibility = Visibility.Collapsed;

        BindingOperations.SetBinding(GlobalStatusBorder, Border.BackgroundProperty, new Binding(nameof(Border.Background)) { Source = pageStatusBorder });
        BindingOperations.SetBinding(GlobalStatusBorder, Border.BorderBrushProperty, new Binding(nameof(Border.BorderBrush)) { Source = pageStatusBorder });
        BindingOperations.SetBinding(GlobalStatusText, TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = pageStatusText });
        BindingOperations.SetBinding(GlobalStatusText, TextBlock.ForegroundProperty, new Binding(nameof(TextBlock.Foreground)) { Source = pageStatusText });
        BindingOperations.SetBinding(GlobalStatusBorder, VisibilityProperty, new Binding(nameof(TextBlock.Text))
        {
            Source = pageStatusText,
            Converter = new StringHasContentToVisibilityConverter()
        });
    }

    private void ClearGlobalStatusBindings()
    {
        BindingOperations.ClearAllBindings(GlobalStatusBorder);
        BindingOperations.ClearAllBindings(GlobalStatusText);

        GlobalStatusBorder.Background = (System.Windows.Media.Brush)FindResource("StatusInfoBackgroundBrush");
        GlobalStatusBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("StatusInfoBorderBrush");
        GlobalStatusText.Foreground = (System.Windows.Media.Brush)FindResource("StatusInfoTextBrush");
        GlobalStatusText.Text = string.Empty;
        GlobalStatusBorder.Visibility = Visibility.Collapsed;
    }
}

public sealed class SectionDefinition
{
    public SectionDefinition(string title, IEnumerable<SectionNavigationItem> items)
    {
        Title = title;
        Items = items.ToList();
    }

    public string Title { get; }
    public IReadOnlyList<SectionNavigationItem> Items { get; }
}
