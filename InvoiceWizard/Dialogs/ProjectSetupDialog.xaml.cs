using System.Windows;
using System.Windows.Controls;
using InvoiceWizard.Data.Entities;

namespace InvoiceWizard.Dialogs;

public partial class ProjectSetupDialog : Window
{
    private readonly CustomerEntity _customer;
    private readonly ProjectEntity? _existingProject;
    private int _stepIndex;

    public ProjectSetupDialog(CustomerEntity customer, ProjectEntity? project = null)
    {
        _customer = customer;
        _existingProject = project;
        InitializeComponent();
        CustomerNameText.Text = customer.Name;
        PopulateForm(project);
        UpdateStepUi();
    }

    public ProjectEntity? ResultProject { get; private set; }

    private void PopulateForm(ProjectEntity? project)
    {
        DialogTitleText.Text = project is null ? "Projekt anlegen" : "Projekt bearbeiten";
        DialogSubtitleText.Text = project is null
            ? "Trage die Projektdaten Schritt fuer Schritt ein."
            : "Passe die Projektdaten Schritt fuer Schritt an.";
        Title = DialogTitleText.Text;

        ProjectNameText.Text = project?.Name ?? string.Empty;
        SelectProjectStatus(project?.ProjectStatus ?? "Active");

        ConnectionUserFirstNameText.Text = project?.ConnectionUserFirstName ?? string.Empty;
        ConnectionUserLastNameText.Text = project?.ConnectionUserLastName ?? string.Empty;
        ConnectionUserStreetText.Text = project?.ConnectionUserStreet ?? string.Empty;
        ConnectionUserHouseNumberText.Text = project?.ConnectionUserHouseNumber ?? string.Empty;
        ConnectionUserPostalCodeText.Text = project?.ConnectionUserPostalCode ?? string.Empty;
        ConnectionUserCityText.Text = project?.ConnectionUserCity ?? string.Empty;
        ConnectionUserParcelNumberText.Text = project?.ConnectionUserParcelNumber ?? string.Empty;
        ConnectionUserEmailText.Text = project?.ConnectionUserEmailAddress ?? string.Empty;
        ConnectionUserPhoneText.Text = project?.ConnectionUserPhoneNumber ?? string.Empty;

        PropertyOwnerFirstNameText.Text = project?.PropertyOwnerFirstName ?? string.Empty;
        PropertyOwnerLastNameText.Text = project?.PropertyOwnerLastName ?? string.Empty;
        PropertyOwnerStreetText.Text = project?.PropertyOwnerStreet ?? string.Empty;
        PropertyOwnerHouseNumberText.Text = project?.PropertyOwnerHouseNumber ?? string.Empty;
        PropertyOwnerPostalCodeText.Text = project?.PropertyOwnerPostalCode ?? string.Empty;
        PropertyOwnerCityText.Text = project?.PropertyOwnerCity ?? string.Empty;
        PropertyOwnerEmailText.Text = project?.PropertyOwnerEmailAddress ?? string.Empty;
        PropertyOwnerPhoneText.Text = project?.PropertyOwnerPhoneNumber ?? string.Empty;
    }

    private void SelectProjectStatus(string projectStatus)
    {
        var normalized = NormalizeProjectStatus(projectStatus);
        foreach (var item in ProjectStatusCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, normalized, StringComparison.OrdinalIgnoreCase))
            {
                ProjectStatusCombo.SelectedItem = item;
                return;
            }
        }

        ProjectStatusCombo.SelectedIndex = 0;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0)
        {
            return;
        }

        _stepIndex--;
        UpdateStepUi();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateCurrentStep())
        {
            return;
        }

        if (_stepIndex >= 2)
        {
            return;
        }

        _stepIndex++;
        UpdateStepUi();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        for (var step = 0; step <= 2; step++)
        {
            _stepIndex = step;
            if (!ValidateCurrentStep())
            {
                UpdateStepUi();
                return;
            }
        }

        ResultProject = BuildProject();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private bool ValidateCurrentStep()
    {
        ValidationMessageText.Text = string.Empty;
        if (_stepIndex != 0)
        {
            return true;
        }

        var validationError = ValidateProjectName((ProjectNameText.Text ?? string.Empty).Trim());
        if (validationError is null)
        {
            return true;
        }

        ValidationMessageText.Text = validationError;
        return false;
    }

    private ProjectEntity BuildProject()
    {
        return new ProjectEntity
        {
            ProjectId = _existingProject?.ProjectId ?? 0,
            CustomerId = _customer.CustomerId,
            Customer = _customer,
            Name = (ProjectNameText.Text ?? string.Empty).Trim(),
            ProjectStatus = ((ProjectStatusCombo.SelectedItem as ComboBoxItem)?.Tag as string) ?? "Active",
            ConnectionUserSameAsCustomer = false,
            ConnectionUserFirstName = (ConnectionUserFirstNameText.Text ?? string.Empty).Trim(),
            ConnectionUserLastName = (ConnectionUserLastNameText.Text ?? string.Empty).Trim(),
            ConnectionUserStreet = (ConnectionUserStreetText.Text ?? string.Empty).Trim(),
            ConnectionUserHouseNumber = (ConnectionUserHouseNumberText.Text ?? string.Empty).Trim(),
            ConnectionUserPostalCode = (ConnectionUserPostalCodeText.Text ?? string.Empty).Trim(),
            ConnectionUserCity = (ConnectionUserCityText.Text ?? string.Empty).Trim(),
            ConnectionUserParcelNumber = (ConnectionUserParcelNumberText.Text ?? string.Empty).Trim(),
            ConnectionUserEmailAddress = (ConnectionUserEmailText.Text ?? string.Empty).Trim(),
            ConnectionUserPhoneNumber = (ConnectionUserPhoneText.Text ?? string.Empty).Trim(),
            PropertyOwnerSameAsCustomer = false,
            PropertyOwnerFirstName = (PropertyOwnerFirstNameText.Text ?? string.Empty).Trim(),
            PropertyOwnerLastName = (PropertyOwnerLastNameText.Text ?? string.Empty).Trim(),
            PropertyOwnerStreet = (PropertyOwnerStreetText.Text ?? string.Empty).Trim(),
            PropertyOwnerHouseNumber = (PropertyOwnerHouseNumberText.Text ?? string.Empty).Trim(),
            PropertyOwnerPostalCode = (PropertyOwnerPostalCodeText.Text ?? string.Empty).Trim(),
            PropertyOwnerCity = (PropertyOwnerCityText.Text ?? string.Empty).Trim(),
            PropertyOwnerEmailAddress = (PropertyOwnerEmailText.Text ?? string.Empty).Trim(),
            PropertyOwnerPhoneNumber = (PropertyOwnerPhoneText.Text ?? string.Empty).Trim()
        };
    }

    private void UpdateStepUi()
    {
        StepOnePanel.Visibility = _stepIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepTwoPanel.Visibility = _stepIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepThreePanel.Visibility = _stepIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepIndicatorText.Text = _stepIndex switch
        {
            0 => "Schritt 1 von 3",
            1 => "Schritt 2 von 3",
            _ => "Schritt 3 von 3"
        };

        BackButton.IsEnabled = _stepIndex > 0;
        NextButton.Visibility = _stepIndex < 2 ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = _stepIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopyConnectionUserFromCustomer_Click(object sender, RoutedEventArgs e)
    {
        ConnectionUserFirstNameText.Text = _customer.FirstName;
        ConnectionUserLastNameText.Text = _customer.LastName;
        ConnectionUserStreetText.Text = _customer.Street;
        ConnectionUserHouseNumberText.Text = _customer.HouseNumber;
        ConnectionUserPostalCodeText.Text = _customer.PostalCode;
        ConnectionUserCityText.Text = _customer.City;
        ConnectionUserParcelNumberText.Text = string.Empty;
        ConnectionUserEmailText.Text = _customer.EmailAddress;
        ConnectionUserPhoneText.Text = _customer.PhoneNumber;
    }

    private void CopyPropertyOwnerFromCustomer_Click(object sender, RoutedEventArgs e)
    {
        PropertyOwnerFirstNameText.Text = _customer.FirstName;
        PropertyOwnerLastNameText.Text = _customer.LastName;
        PropertyOwnerStreetText.Text = _customer.Street;
        PropertyOwnerHouseNumberText.Text = _customer.HouseNumber;
        PropertyOwnerPostalCodeText.Text = _customer.PostalCode;
        PropertyOwnerCityText.Text = _customer.City;
        PropertyOwnerEmailText.Text = _customer.EmailAddress;
        PropertyOwnerPhoneText.Text = _customer.PhoneNumber;
    }

    private void CopyPropertyOwnerFromConnectionUser_Click(object sender, RoutedEventArgs e)
    {
        PropertyOwnerFirstNameText.Text = ConnectionUserFirstNameText.Text;
        PropertyOwnerLastNameText.Text = ConnectionUserLastNameText.Text;
        PropertyOwnerStreetText.Text = ConnectionUserStreetText.Text;
        PropertyOwnerHouseNumberText.Text = ConnectionUserHouseNumberText.Text;
        PropertyOwnerPostalCodeText.Text = ConnectionUserPostalCodeText.Text;
        PropertyOwnerCityText.Text = ConnectionUserCityText.Text;
        PropertyOwnerEmailText.Text = ConnectionUserEmailText.Text;
        PropertyOwnerPhoneText.Text = ConnectionUserPhoneText.Text;
    }

    private static string NormalizeProjectStatus(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" or "aktiv" => "Active",
            "paused" or "pausiert" => "Paused",
            "ended" or "beendet" => "Ended",
            _ => "Active"
        };
    }

    private static string? ValidateProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return "Bitte einen Projektnamen eingeben.";
        }

        if (projectName.Length < 3 || !char.IsDigit(projectName[0]) || !char.IsDigit(projectName[1]) || !char.IsDigit(projectName[2]))
        {
            return "Der Projektname muss mit einer dreistelligen Nummer beginnen, z. B. 001 Musterprojekt.";
        }

        return null;
    }
}
