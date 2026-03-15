using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        ApplySameAsCustomerForms();
    }

    private async void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadProjectDetailsAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadProjectDetailsAsync();
    }

    private async void SaveProjectDetails_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId is null)
        {
            SetStatus("Bitte zuerst ein Projekt auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (CustomerCombo.SelectedItem is not CustomerEntity customer)
        {
            SetStatus("Bitte zuerst einen Kunden auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var project = new ProjectEntity
        {
            ProjectId = projectSelection.ProjectId.Value,
            CustomerId = customer.CustomerId,
            Customer = customer,
            Name = projectSelection.Name,
            ConnectionUserSameAsCustomer = ConnectionUserSameAsCustomerCheck.IsChecked == true,
            ConnectionUserFirstName = (ConnectionUserFirstNameText.Text ?? string.Empty).Trim(),
            ConnectionUserLastName = (ConnectionUserLastNameText.Text ?? string.Empty).Trim(),
            ConnectionUserStreet = (ConnectionUserStreetText.Text ?? string.Empty).Trim(),
            ConnectionUserHouseNumber = (ConnectionUserHouseNumberText.Text ?? string.Empty).Trim(),
            ConnectionUserPostalCode = (ConnectionUserPostalCodeText.Text ?? string.Empty).Trim(),
            ConnectionUserCity = (ConnectionUserCityText.Text ?? string.Empty).Trim(),
            ConnectionUserParcelNumber = (ConnectionUserParcelNumberText.Text ?? string.Empty).Trim(),
            ConnectionUserEmailAddress = (ConnectionUserEmailText.Text ?? string.Empty).Trim(),
            ConnectionUserPhoneNumber = (ConnectionUserPhoneText.Text ?? string.Empty).Trim(),
            PropertyOwnerSameAsCustomer = PropertyOwnerSameAsCustomerCheck.IsChecked == true,
            PropertyOwnerFirstName = (PropertyOwnerFirstNameText.Text ?? string.Empty).Trim(),
            PropertyOwnerLastName = (PropertyOwnerLastNameText.Text ?? string.Empty).Trim(),
            PropertyOwnerStreet = (PropertyOwnerStreetText.Text ?? string.Empty).Trim(),
            PropertyOwnerHouseNumber = (PropertyOwnerHouseNumberText.Text ?? string.Empty).Trim(),
            PropertyOwnerPostalCode = (PropertyOwnerPostalCodeText.Text ?? string.Empty).Trim(),
            PropertyOwnerCity = (PropertyOwnerCityText.Text ?? string.Empty).Trim(),
            PropertyOwnerEmailAddress = (PropertyOwnerEmailText.Text ?? string.Empty).Trim(),
            PropertyOwnerPhoneNumber = (PropertyOwnerPhoneText.Text ?? string.Empty).Trim()
        };

        var saved = await App.Api.UpdateProjectDetailsAsync(project);
        FillProjectForm(saved);
        SetStatus($"Projektdaten fuer {saved.Name} gespeichert.", StatusMessageType.Success);
    }

    private void ConnectionUserSameAsCustomerCheck_Changed(object sender, RoutedEventArgs e)
    {
        ApplyConnectionUserState();
    }

    private void PropertyOwnerSameAsCustomerCheck_Changed(object sender, RoutedEventArgs e)
    {
        ApplyPropertyOwnerState();
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await App.Api.GetCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        var selected = App.SelectedCustomerId.HasValue
            ? customers.FirstOrDefault(c => c.CustomerId == App.SelectedCustomerId.Value)
            : customers.FirstOrDefault();
        CustomerCombo.SelectedItem = selected ?? customers.FirstOrDefault();
        App.SetSelectedCustomer((CustomerCombo.SelectedItem as CustomerEntity)?.CustomerId);
        if (customers.Count == 0)
        {
            ClearProjectForm();
            SetStatus("Noch keine Kunden vorhanden.", StatusMessageType.Info);
        }
    }

    private async Task LoadProjectsAsync(CustomerEntity? customer)
    {
        if (customer is null)
        {
            ProjectCombo.ItemsSource = null;
            ProjectCombo.SelectedItem = null;
            ClearProjectForm();
            return;
        }

        var projects = await App.Api.GetProjectSelectionsAsync(customer.CustomerId);
        ProjectCombo.ItemsSource = projects;
        ProjectCombo.SelectedItem = projects.FirstOrDefault();
        if (projects.Count == 0)
        {
            ClearProjectForm();
            SetStatus("Fuer diesen Kunden gibt es noch kein Projekt.", StatusMessageType.Info);
        }
    }

    private async Task LoadProjectDetailsAsync()
    {
        if (ProjectCombo.SelectedItem is not ProjectSelectionItem projectSelection || projectSelection.ProjectId is null)
        {
            ClearProjectForm();
            return;
        }

        var project = await App.Api.GetProjectDetailsAsync(projectSelection.ProjectId.Value);
        if (CustomerCombo.SelectedItem is CustomerEntity customer)
        {
            project.Customer = customer;
        }

        FillProjectForm(project);
        SetStatus($"Projektdaten fuer {project.Name} geladen.", StatusMessageType.Info);
    }

    private void FillProjectForm(ProjectEntity? project)
    {
        if (project is null)
        {
            ClearProjectForm();
            return;
        }

        ConnectionUserSameAsCustomerCheck.IsChecked = project.ConnectionUserSameAsCustomer;
        ConnectionUserFirstNameText.Text = project.ConnectionUserFirstName;
        ConnectionUserLastNameText.Text = project.ConnectionUserLastName;
        ConnectionUserStreetText.Text = project.ConnectionUserStreet;
        ConnectionUserHouseNumberText.Text = project.ConnectionUserHouseNumber;
        ConnectionUserPostalCodeText.Text = project.ConnectionUserPostalCode;
        ConnectionUserCityText.Text = project.ConnectionUserCity;
        ConnectionUserParcelNumberText.Text = project.ConnectionUserParcelNumber;
        ConnectionUserEmailText.Text = project.ConnectionUserEmailAddress;
        ConnectionUserPhoneText.Text = project.ConnectionUserPhoneNumber;

        PropertyOwnerSameAsCustomerCheck.IsChecked = project.PropertyOwnerSameAsCustomer;
        PropertyOwnerFirstNameText.Text = project.PropertyOwnerFirstName;
        PropertyOwnerLastNameText.Text = project.PropertyOwnerLastName;
        PropertyOwnerStreetText.Text = project.PropertyOwnerStreet;
        PropertyOwnerHouseNumberText.Text = project.PropertyOwnerHouseNumber;
        PropertyOwnerPostalCodeText.Text = project.PropertyOwnerPostalCode;
        PropertyOwnerCityText.Text = project.PropertyOwnerCity;
        PropertyOwnerEmailText.Text = project.PropertyOwnerEmailAddress;
        PropertyOwnerPhoneText.Text = project.PropertyOwnerPhoneNumber;

        ApplySameAsCustomerForms();
    }

    private void ApplySameAsCustomerForms()
    {
        ApplyConnectionUserState();
        ApplyPropertyOwnerState();
    }

    private void ApplyConnectionUserState()
    {
        var useCustomer = ConnectionUserSameAsCustomerCheck.IsChecked == true;
        SetConnectionUserInputsEnabled(!useCustomer);
        if (useCustomer && CustomerCombo.SelectedItem is CustomerEntity customer)
        {
            ConnectionUserFirstNameText.Text = customer.FirstName;
            ConnectionUserLastNameText.Text = customer.LastName;
            ConnectionUserStreetText.Text = customer.Street;
            ConnectionUserHouseNumberText.Text = customer.HouseNumber;
            ConnectionUserPostalCodeText.Text = customer.PostalCode;
            ConnectionUserCityText.Text = customer.City;
            ConnectionUserParcelNumberText.Text = string.Empty;
            ConnectionUserEmailText.Text = customer.EmailAddress;
            ConnectionUserPhoneText.Text = customer.PhoneNumber;
        }
    }

    private void ApplyPropertyOwnerState()
    {
        var useCustomer = PropertyOwnerSameAsCustomerCheck.IsChecked == true;
        SetPropertyOwnerInputsEnabled(!useCustomer);
        if (useCustomer && CustomerCombo.SelectedItem is CustomerEntity customer)
        {
            PropertyOwnerFirstNameText.Text = customer.FirstName;
            PropertyOwnerLastNameText.Text = customer.LastName;
            PropertyOwnerStreetText.Text = customer.Street;
            PropertyOwnerHouseNumberText.Text = customer.HouseNumber;
            PropertyOwnerPostalCodeText.Text = customer.PostalCode;
            PropertyOwnerCityText.Text = customer.City;
            PropertyOwnerEmailText.Text = customer.EmailAddress;
            PropertyOwnerPhoneText.Text = customer.PhoneNumber;
        }
    }

    private void SetConnectionUserInputsEnabled(bool enabled)
    {
        foreach (var control in new Control[] { ConnectionUserFirstNameText, ConnectionUserLastNameText, ConnectionUserStreetText, ConnectionUserHouseNumberText, ConnectionUserPostalCodeText, ConnectionUserCityText, ConnectionUserParcelNumberText, ConnectionUserEmailText, ConnectionUserPhoneText })
        {
            control.IsEnabled = enabled;
        }
    }

    private void SetPropertyOwnerInputsEnabled(bool enabled)
    {
        foreach (var control in new Control[] { PropertyOwnerFirstNameText, PropertyOwnerLastNameText, PropertyOwnerStreetText, PropertyOwnerHouseNumberText, PropertyOwnerPostalCodeText, PropertyOwnerCityText, PropertyOwnerEmailText, PropertyOwnerPhoneText })
        {
            control.IsEnabled = enabled;
        }
    }

    private void ClearProjectForm()
    {
        ConnectionUserSameAsCustomerCheck.IsChecked = false;
        ConnectionUserFirstNameText.Clear();
        ConnectionUserLastNameText.Clear();
        ConnectionUserStreetText.Clear();
        ConnectionUserHouseNumberText.Clear();
        ConnectionUserPostalCodeText.Clear();
        ConnectionUserCityText.Clear();
        ConnectionUserParcelNumberText.Clear();
        ConnectionUserEmailText.Clear();
        ConnectionUserPhoneText.Clear();
        PropertyOwnerSameAsCustomerCheck.IsChecked = false;
        PropertyOwnerFirstNameText.Clear();
        PropertyOwnerLastNameText.Clear();
        PropertyOwnerStreetText.Clear();
        PropertyOwnerHouseNumberText.Clear();
        PropertyOwnerPostalCodeText.Clear();
        PropertyOwnerCityText.Clear();
        PropertyOwnerEmailText.Clear();
        PropertyOwnerPhoneText.Clear();
        SetConnectionUserInputsEnabled(true);
        SetPropertyOwnerInputsEnabled(true);
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
