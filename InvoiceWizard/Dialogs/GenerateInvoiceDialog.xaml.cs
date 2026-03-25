using System;
using System.Windows;

namespace InvoiceWizard.Dialogs;

public partial class GenerateInvoiceDialog : Window
{
    private const string DefaultInvoiceSubject = "Unsere Lieferungen/Leistungen stellen wir Ihnen wie folgt in Rechnung.";
    private const string DefaultCreditNoteSubject = "Wir schreiben Ihnen folgende Gutschrift gut.";
    private readonly string _invoiceNumber;
    private readonly string _creditNoteNumber;
    private readonly bool _isDraftMode;

    public GenerateInvoiceDialog(string invoiceNumber, string creditNoteNumber, string customerNumber, string customerName, bool isDraftMode = false, GeneratedInvoiceOptions? initialOptions = null)
    {
        InitializeComponent();
        _isDraftMode = isDraftMode;
        _invoiceNumber = invoiceNumber;
        _creditNoteNumber = string.IsNullOrWhiteSpace(creditNoteNumber) ? invoiceNumber : creditNoteNumber;
        CustomerNumberText.Text = customerNumber;
        CustomerNameText.Text = customerName;
        InvoiceDatePicker.SelectedDate = initialOptions?.InvoiceDate.Date ?? DateTime.Today;
        DeliveryDatePicker.SelectedDate = initialOptions?.DeliveryDate.Date ?? DateTime.Today;
        SubjectText.Text = string.IsNullOrWhiteSpace(initialOptions?.Subject)
            ? DefaultInvoiceSubject
            : initialOptions!.Subject;
        var applySmallBusinessRegulation = initialOptions?.ApplySmallBusinessRegulation ?? false;
        WithoutVatRadio.IsChecked = applySmallBusinessRegulation;
        WithVatRadio.IsChecked = !applySmallBusinessRegulation;
        var invoiceDirection = initialOptions?.InvoiceDirection ?? "Revenue";
        InvoiceRadio.IsChecked = !string.Equals(invoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase);
        CreditNoteRadio.IsChecked = string.Equals(invoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase);
        UpdateDocumentModeUi();
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
            ApplySmallBusinessRegulation = WithoutVatRadio.IsChecked == true,
            InvoiceDirection = CreditNoteRadio.IsChecked == true ? "RevenueReduction" : "Revenue"
        };

        DialogResult = true;
    }

    private void DocumentTypeRadio_Checked(object sender, RoutedEventArgs e)
    {
        UpdateDocumentModeUi();
    }

    private void UpdateDocumentModeUi()
    {
        var isCreditNote = CreditNoteRadio.IsChecked == true;
        InvoiceNumberText.Text = isCreditNote ? _creditNoteNumber : _invoiceNumber;
        DocumentNumberLabel.Text = isCreditNote ? "Gutschriftsnummer" : "Rechnungsnummer";
        DialogTitleText.Text = _isDraftMode
            ? (isCreditNote ? "Gutschriftsentwurf speichern" : "Rechnungsentwurf speichern")
            : (isCreditNote ? "Gutschrift erzeugen" : "Rechnung erzeugen");
        DialogHintText.Text = _isDraftMode
            ? "Der Entwurf wird mit Wasserzeichen gespeichert und kann spaeter noch bearbeitet oder finalisiert werden."
            : "Belegtyp, Nummer, Lieferdatum, Umsatzsteuer und Betreff kannst du vor dem Export noch anpassen.";
        ConfirmButton.Content = _isDraftMode ? "Entwurf speichern" : (isCreditNote ? "Gutschrift erzeugen" : "Rechnung erzeugen");

        if (string.Equals(SubjectText.Text, DefaultInvoiceSubject, StringComparison.Ordinal)
            || string.Equals(SubjectText.Text, DefaultCreditNoteSubject, StringComparison.Ordinal))
        {
            SubjectText.Text = isCreditNote ? DefaultCreditNoteSubject : DefaultInvoiceSubject;
        }
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
    public string InvoiceDirection { get; set; } = "Revenue";
}
