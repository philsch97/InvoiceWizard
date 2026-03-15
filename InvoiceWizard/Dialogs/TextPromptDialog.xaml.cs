using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class TextPromptDialog : Window
{
    public string Result { get; private set; } = "";

    public TextPromptDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueTextBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var value = (ValueTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(this, "Bitte einen Kommentar eingeben.", "Kommentar fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = value;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
