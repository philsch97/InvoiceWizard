using InvoiceWizard.Data.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class AnalyticsPage : Page
{
    public AnalyticsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await LoadFiltersAsync();
            await LoadAnalyticsAsync();
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAnalyticsAsync();
    }

    private async void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        CustomerFilterCombo.SelectedIndex = 0;
        await LoadProjectsAsync();
        ProjectFilterCombo.SelectedIndex = 0;
        await LoadAnalyticsAsync();
    }

    private async void CustomerFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        await LoadProjectsAsync();
        await LoadAnalyticsAsync();
    }

    private async void ProjectFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        await LoadAnalyticsAsync();
    }

    private async Task LoadFiltersAsync()
    {
        var customers = new List<CustomerSelectionItem> { new() { CustomerId = null, Name = "Alle Kunden" } };
        customers.AddRange((await App.Api.GetCustomersAsync()).Select(c => new CustomerSelectionItem { CustomerId = c.CustomerId, Name = c.Name }));
        CustomerFilterCombo.ItemsSource = customers;
        CustomerFilterCombo.SelectedIndex = 0;
        await LoadProjectsAsync();
    }

    private async Task LoadProjectsAsync()
    {
        var selectedCustomerId = (CustomerFilterCombo.SelectedItem as CustomerSelectionItem)?.CustomerId;
        var projects = new List<ProjectSelectionItem> { new() { ProjectId = null, Name = "Alle Projekte" } };
        if (selectedCustomerId.HasValue)
        {
            projects.AddRange(await App.Api.GetProjectSelectionsAsync(selectedCustomerId.Value));
        }

        var previousProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        ProjectFilterCombo.ItemsSource = projects;
        ProjectFilterCombo.SelectedItem = previousProjectId.HasValue ? projects.FirstOrDefault(p => p.ProjectId == previousProjectId.Value) ?? projects[0] : projects[0];
    }

    private async Task LoadAnalyticsAsync()
    {
        var selectedCustomerId = (CustomerFilterCombo.SelectedItem as CustomerSelectionItem)?.CustomerId;
        var selectedProjectId = (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId;
        var analytics = await App.Api.GetAnalyticsAsync(selectedCustomerId, selectedProjectId);

        RevenueText.Text = analytics.Revenue.ToString("0.00 EUR", CultureInfo.GetCultureInfo("de-DE"));
        ExpensesText.Text = analytics.Expenses.ToString("0.00 EUR", CultureInfo.GetCultureInfo("de-DE"));
        ProfitText.Text = analytics.Profit.ToString("0.00 EUR", CultureInfo.GetCultureInfo("de-DE"));
        ProfitText.Foreground = analytics.Profit >= 0m ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
        OpenRevenueText.Text = analytics.OpenRevenue.ToString("0.00 EUR", CultureInfo.GetCultureInfo("de-DE"));

        MonthlyChart.ItemsSource = analytics.Monthly;
        ProjectAnalyticsGrid.ItemsSource = analytics.Projects;
        SetStatus($"Auswertung aktualisiert. {analytics.Projects.Count} Projekt(e) in der aktuellen Filterung.", StatusMessageType.Info);
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
