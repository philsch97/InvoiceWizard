using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using InvoiceWizard.Dialogs;

namespace InvoiceWizard;

public partial class ProjectContactsPage : Page
{
    public ProjectContactsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        App.SetSelectedCustomer((CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId);
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadProjectDetailsAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadProjectDetailsAsync();
    }

    private async void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var dialog = new ProjectSetupDialog(customer)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.ResultProject is null)
        {
            return;
        }

        try
        {
            var savedProject = await App.Api.SaveProjectAsync(dialog.ResultProject);
            await LoadProjectsAsync(customer, savedProject.ProjectId);
            SetStatus($"Projekt {savedProject.Name} wurde angelegt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Projekt konnte nicht gespeichert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void EditProject_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId is null)
        {
            SetStatus("Bitte zuerst ein Projekt auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var currentProject = await App.Api.GetProjectDetailsAsync(projectSelection.ProjectId.Value);
        var dialog = new ProjectSetupDialog(customer, currentProject)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.ResultProject is null)
        {
            return;
        }

        try
        {
            var savedProject = await App.Api.SaveProjectAsync(dialog.ResultProject);
            await LoadProjectsAsync(customer, savedProject.ProjectId);
            SetStatus($"Projekt {savedProject.Name} wurde aktualisiert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Projekt konnte nicht aktualisiert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId is null)
        {
            SetStatus("Bitte zuerst ein Projekt zum Loeschen auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Soll das Projekt {projectSelection.Name} wirklich geloescht werden? Zugehoerige Material-, Arbeitszeit- und Notizeintraege dieses Projekts werden dabei ebenfalls entfernt.", "Projekt loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await App.Api.DeleteProjectAsync(projectSelection.ProjectId.Value);
        await LoadProjectsAsync(CustomerCombo.SelectedItem as CustomerEntity);
        SetStatus($"Projekt {projectSelection.Name} wurde geloescht.", StatusMessageType.Success);
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;

        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectCombo.ItemsSource = null;
            ClearProjectSummary();
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Info);
            return;
        }

        var customer = App.SelectedCustomerId.HasValue
            ? customers.FirstOrDefault(c => c.CustomerId == App.SelectedCustomerId.Value) ?? customers[0]
            : customers[0];
        CustomerCombo.SelectedItem = customer;
        App.SetSelectedCustomer(customer.CustomerId);
        await LoadProjectsAsync(customer);
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer, int? selectedProjectId = null)
    {
        if (customer is null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            ClearProjectSummary();
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: false, includeInactive: true);
        ProjectCombo.ItemsSource = projects;
        ProjectCombo.SelectedItem = selectedProjectId.HasValue
            ? projects.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) ?? projects.FirstOrDefault()
            : projects.FirstOrDefault();

        if (projects.Count == 0)
        {
            ClearProjectSummary();
            SetStatus("Fuer diesen Kunden gibt es noch kein Projekt.", StatusMessageType.Info);
        }
    }

    private async Task LoadProjectDetailsAsync()
    {
        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId is null)
        {
            ClearProjectSummary();
            return;
        }

        var project = await App.Api.GetProjectDetailsAsync(projectSelection.ProjectId.Value);
        FillProjectSummary(project);
        SetStatus($"Projekt {project.Name} geladen.", StatusMessageType.Info);
    }

    private void FillProjectSummary(ProjectEntity project)
    {
        ProjectSummaryTitleText.Text = project.Name;
        ProjectSummaryMetaText.Text = $"Kunde: {project.Customer.Name} | Erstellt: {project.CreatedAt:dd.MM.yyyy}";
        ProjectStatusText.Text = project.ProjectStatusLabel;
        OpenPositionsText.Text = project.OpenPositionCount.ToString();
        OpenDraftInvoicesText.Text = project.OpenDraftInvoiceCount.ToString();
        ProjectEndHintText.Text = project.CanBeEnded
            ? "Dieses Projekt kann bei Bedarf beendet werden."
            : project.CannotEndReason;
        ConnectionUserSummaryText.Text = BuildContactSummary(
            project.ConnectionUserFirstName,
            project.ConnectionUserLastName,
            project.ConnectionUserStreet,
            project.ConnectionUserHouseNumber,
            project.ConnectionUserPostalCode,
            project.ConnectionUserCity,
            project.ConnectionUserEmailAddress,
            project.ConnectionUserPhoneNumber,
            project.ConnectionUserParcelNumber);
        PropertyOwnerSummaryText.Text = BuildContactSummary(
            project.PropertyOwnerFirstName,
            project.PropertyOwnerLastName,
            project.PropertyOwnerStreet,
            project.PropertyOwnerHouseNumber,
            project.PropertyOwnerPostalCode,
            project.PropertyOwnerCity,
            project.PropertyOwnerEmailAddress,
            project.PropertyOwnerPhoneNumber,
            null);
    }

    private static string BuildContactSummary(string firstName, string lastName, string street, string houseNumber, string postalCode, string city, string email, string phone, string? parcelNumber)
    {
        var lines = new List<string>();
        var name = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            lines.Add(name);
        }

        var address = string.Join(" ", new[] { street, houseNumber }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            lines.Add(address);
        }

        var cityLine = string.Join(" ", new[] { postalCode, city }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(cityLine))
        {
            lines.Add(cityLine);
        }

        if (!string.IsNullOrWhiteSpace(parcelNumber))
        {
            lines.Add($"Flurnummer: {parcelNumber}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            lines.Add($"E-Mail: {email}");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            lines.Add($"Telefon: {phone}");
        }

        return lines.Count == 0 ? "Keine Daten hinterlegt." : string.Join(Environment.NewLine, lines);
    }

    private void ClearProjectSummary()
    {
        ProjectSummaryTitleText.Text = "Kein Projekt ausgewaehlt";
        ProjectSummaryMetaText.Text = "Waehle oben einen Kunden und ein Projekt aus oder lege direkt ein neues Projekt an.";
        ProjectStatusText.Text = "-";
        OpenPositionsText.Text = "0";
        OpenDraftInvoicesText.Text = "0";
        ProjectEndHintText.Text = string.Empty;
        ConnectionUserSummaryText.Text = "Keine Daten hinterlegt.";
        PropertyOwnerSummaryText.Text = "Keine Daten hinterlegt.";
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
