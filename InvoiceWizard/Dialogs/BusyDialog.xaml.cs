using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class BusyDialog : Window
{
    public BusyDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }
}
