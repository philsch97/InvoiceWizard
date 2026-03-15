using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceWizard;

public partial class CustomerHandling : Page
{
    public CustomerHandling()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCustomersAsync();
    }

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var customer = ReadCustomerFromForm();
        if (customer is null)
        {
            return;
        }

        var saved = await App.Api.SaveCustomerAsync(customer);
        SetStatus("Kunde gespeichert.", StatusMessageType.Success);
        await LoadCustomersAsync(saved.Name);
    }

    private async void UpdateCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity selectedCustomer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen, der aktualisiert werden soll.", StatusMessageType.Warning);
            return;
        }

        var customer = ReadCustomerFromForm();
        if (customer is null)
        {
            return;
        }

        var saved = await App.Api.SaveCustomerAsync(customer, selectedCustomer.CustomerId);
        SetStatus("Kundendaten aktualisiert.", StatusMessageType.Success);
        await LoadCustomersAsync(saved.Name, (ProjectFilterCombo.SelectedItem as ProjectSelectionItem)?.ProjectId);
    }

    private void ClearCustomerForm_Click(object sender, RoutedEventArgs e)
    {
        FillCustomerForm(null);
        CustomerCombo.SelectedItem = null;
        ProjectFilterCombo.ItemsSource = null;
        ProjectFilterCombo.SelectedItem = null;
        NewProjectNameText.Clear();
        SetStatus("Kundenformular geleert.", StatusMessageType.Info);
    }

    private async void AddProject_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var projectName = (NewProjectNameText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            SetStatus("Bitte einen Projektnamen eingeben.", StatusMessageType.Error);
            return;
        }

        var existingProjects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId);
        if (existingProjects.Any(p => p.Name == projectName))
        {
            SetStatus("Dieses Projekt existiert fuer den Kunden bereits.", StatusMessageType.Warning);
            return;
        }

        await App.Api.SaveProjectAsync(customer.CustomerId, projectName);
        NewProjectNameText.Clear();
        await LoadProjectsAsync(customer, projectName);
        SetStatus($"Projekt {projectName} wurde fuer {customer.Name} gespeichert.", StatusMessageType.Success);
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectFilterCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId == null)
        {
            SetStatus("Bitte ein konkretes Projekt zum Loeschen auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Soll das Projekt {projectSelection.Name} wirklich geloescht werden? Zugehoerige Material- und Arbeitszeitpositionen dieses Projekts werden dabei ebenfalls entfernt.", "Projekt loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await App.Api.DeleteProjectAsync(projectSelection.ProjectId.Value);
        if (CustomerCombo.SelectedItem is CustomerEntity customer)
        {
            await LoadProjectsAsync(customer);
        }

        SetStatus($"Projekt {projectSelection.Name} wurde geloescht.", StatusMessageType.Success);
    }

    private async void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show($"Soll der Kunde {customer.Name} wirklich geloescht werden? Vorhandene Projekte, Zuweisungen und Zeiteintraege dieses Kunden werden dabei ebenfalls entfernt.", "Kunde loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await App.Api.DeleteCustomerAsync(customer.CustomerId);
        await LoadCustomersAsync();
        SetStatus($"Kunde {customer.Name} wurde geloescht.", StatusMessageType.Success);
    }

    private async void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = CustomerCombo.SelectedItem as CustomerEntity;
        FillCustomerForm(customer);
        await LoadProjectsAsync(customer);
    }

    private async Task LoadCustomersAsync(string? selectCustomerName = null, int? selectedProjectId = null)
    {
        var selectedCustomerId = CustomerCombo.SelectedItem is CustomerEntity selectedCustomer ? selectedCustomer.CustomerId : (int?)null;
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;

        if (customers.Count == 0)
        {
            CustomerCombo.SelectedItem = null;
            ProjectFilterCombo.ItemsSource = null;
            ProjectFilterCombo.SelectedItem = null;
            FillCustomerForm(null);
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Info);
            return;
        }

        CustomerEntity? customerToSelect = null;
        if (!string.IsNullOrWhiteSpace(selectCustomerName))
        {
            customerToSelect = customers.FirstOrDefault(c => c.Name == selectCustomerName);
        }

        customerToSelect ??= selectedCustomerId.HasValue ? customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value) : null;
        customerToSelect ??= customers[0];
        CustomerCombo.SelectedItem = customerToSelect;
        FillCustomerForm(customerToSelect);

        if (customerToSelect is not null)
        {
            await LoadProjectsAsync(customerToSelect, selectedProjectId: selectedProjectId);
            SetStatus($"Kunde {customerToSelect.Name} geladen.", StatusMessageType.Info);
        }
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer, string? selectProjectName = null, int? selectedProjectId = null)
    {
        if (customer == null)
        {
            ProjectFilterCombo.ItemsSource = null;
            ProjectFilterCombo.SelectedItem = null;
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId, includeAll: false);
        ProjectFilterCombo.ItemsSource = projects;
        ProjectSelectionItem? projectToSelect = null;
        if (!string.IsNullOrWhiteSpace(selectProjectName))
        {
            projectToSelect = projects.FirstOrDefault(p => p.Name == selectProjectName);
        }

        projectToSelect ??= selectedProjectId.HasValue ? projects.FirstOrDefault(p => p.ProjectId == selectedProjectId.Value) : null;
        ProjectFilterCombo.SelectedItem = projectToSelect ?? projects.FirstOrDefault();
    }

    private CustomerEntity? ReadCustomerFromForm()
    {
        var firstName = (CustomerFirstNameText.Text ?? string.Empty).Trim();
        var lastName = (CustomerLastNameText.Text ?? string.Empty).Trim();
        var displayName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Bitte mindestens Vorname und/oder Nachname angeben.", StatusMessageType.Error);
            return null;
        }

        if (!TryParseDecimal(DefaultMarkupText.Text, out var defaultMarkup) || defaultMarkup < 0m)
        {
            SetStatus("Bitte einen gueltigen Standard-Zuschlag eingeben.", StatusMessageType.Error);
            return null;
        }

        return new CustomerEntity
        {
            CustomerNumber = (CustomerNumberText.Text ?? string.Empty).Trim(),
            Name = displayName,
            FirstName = firstName,
            LastName = lastName,
            Street = (CustomerStreetText.Text ?? string.Empty).Trim(),
            HouseNumber = (CustomerHouseNumberText.Text ?? string.Empty).Trim(),
            PostalCode = (CustomerPostalCodeText.Text ?? string.Empty).Trim(),
            City = (CustomerCityText.Text ?? string.Empty).Trim(),
            EmailAddress = (CustomerEmailText.Text ?? string.Empty).Trim(),
            PhoneNumber = (CustomerPhoneText.Text ?? string.Empty).Trim(),
            DefaultMarkupPercent = defaultMarkup
        };
    }

    private void FillCustomerForm(CustomerEntity? customer)
    {
        CustomerNumberText.Text = customer?.CustomerNumber ?? string.Empty;
        CustomerFirstNameText.Text = customer?.FirstName ?? string.Empty;
        CustomerLastNameText.Text = customer?.LastName ?? string.Empty;
        CustomerStreetText.Text = customer?.Street ?? string.Empty;
        CustomerHouseNumberText.Text = customer?.HouseNumber ?? string.Empty;
        CustomerPostalCodeText.Text = customer?.PostalCode ?? string.Empty;
        CustomerCityText.Text = customer?.City ?? string.Empty;
        CustomerEmailText.Text = customer?.EmailAddress ?? string.Empty;
        CustomerPhoneText.Text = customer?.PhoneNumber ?? string.Empty;
        DefaultMarkupText.Text = (customer?.DefaultMarkupPercent ?? 0m).ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
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
