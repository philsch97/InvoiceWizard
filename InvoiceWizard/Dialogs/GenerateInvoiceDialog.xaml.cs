using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class GenerateInvoiceDialog : Window
{
    public GenerateInvoiceDialog(string invoiceNumber, string customerNumber, string customerName, bool isDraftMode = false, GeneratedInvoiceOptions? initialOptions = null)
    {
        InitializeComponent();
        InvoiceNumberText.Text = invoiceNumber;
        CustomerNumberText.Text = customerNumber;
        CustomerNameText.Text = customerName;
        InvoiceDatePicker.SelectedDate = initialOptions?.InvoiceDate.Date ?? DateTime.Today;
        DeliveryDatePicker.SelectedDate = initialOptions?.DeliveryDate.Date ?? DateTime.Today;
        SubjectText.Text = string.IsNullOrWhiteSpace(initialOptions?.Subject)
            ? "Unsere Lieferungen/Leistungen stellen wir Ihnen wie folgt in Rechnung."
            : initialOptions!.Subject;
        SmallBusinessCheck.IsChecked = initialOptions?.ApplySmallBusinessRegulation ?? true;
        ConfirmButton.Content = isDraftMode ? "Entwurf speichern" : "Rechnung erzeugen";
        DialogTitleText.Text = isDraftMode ? "Rechnungsentwurf speichern" : "Rechnung erzeugen";
        DialogHintText.Text = isDraftMode
            ? "Der Entwurf wird mit Wasserzeichen gespeichert und kann spaeter noch bearbeitet oder finalisiert werden."
            : "Die Rechnungsnummer wird automatisch eindeutig vergeben. Lieferdatum und Betreff kannst du vor dem Export noch anpassen.";
    }

    public GeneratedInvoiceOptions? Result { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DeliveryDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Bitte ein Lieferdatum auswaehlen.", "Rechnung", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new GeneratedInvoiceOptions
        {
            InvoiceNumber = InvoiceNumberText.Text,
            CustomerNumber = CustomerNumberText.Text,
            InvoiceDate = InvoiceDatePicker.SelectedDate ?? DateTime.Today,
            DeliveryDate = DeliveryDatePicker.SelectedDate.Value.Date,
            Subject = (SubjectText.Text ?? string.Empty).Trim(),
            ApplySmallBusinessRegulation = SmallBusinessCheck.IsChecked == true
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public class GeneratedInvoiceOptions
{
    public string InvoiceNumber { get; set; } = "";
    public string CustomerNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string Subject { get; set; } = "";
    public bool ApplySmallBusinessRegulation { get; set; }
}
