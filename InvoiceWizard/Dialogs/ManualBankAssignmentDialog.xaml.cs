using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class ManualBankAssignmentDialog : Window
{
    public string SelectedCategory { get; private set; } = "BankFees";
    public string Note => (NoteText.Text ?? string.Empty).Trim();

    public ManualBankAssignmentDialog()
    {
        InitializeComponent();
        CategoryCombo.ItemsSource = new[]
        {
            new ManualCategoryItem("BankFees", "Kontofuehrungsgebuehren"),
            new ManualCategoryItem("Insurance", "Versicherung")
        };
        CategoryCombo.DisplayMemberPath = nameof(ManualCategoryItem.Label);
        CategoryCombo.SelectedValuePath = nameof(ManualCategoryItem.Code);
        CategoryCombo.SelectedIndex = 0;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryCombo.SelectedValue is not string selectedCategory || string.IsNullOrWhiteSpace(selectedCategory))
        {
            MessageBox.Show(this, "Bitte eine Kategorie auswaehlen.", "Kategorie fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedCategory = selectedCategory;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record ManualCategoryItem(string Code, string Label);
}
