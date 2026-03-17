using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard.Dialogs;

public partial class DraftInvoiceEditorDialog : Window
{
    private readonly ObservableCollection<ManualInvoiceLineInput> _lines;

    public DraftInvoiceEditorDialog(string invoiceNumber, string customerNumber, string customerName, GeneratedInvoiceOptions initialOptions, IEnumerable<ManualInvoiceLineInput> initialLines)
    {
        InitializeComponent();
        InvoiceNumberText.Text = invoiceNumber;
        CustomerNumberText.Text = customerNumber;
        CustomerNameText.Text = customerName;
        InvoiceDatePicker.SelectedDate = initialOptions.InvoiceDate.Date;
        DeliveryDatePicker.SelectedDate = initialOptions.DeliveryDate.Date;
        SubjectText.Text = initialOptions.Subject;
        SmallBusinessCheck.IsChecked = initialOptions.ApplySmallBusinessRegulation;
        _lines = new ObservableCollection<ManualInvoiceLineInput>(initialLines.Select(line => CloneLine(line)));
        LinesGrid.ItemsSource = _lines;
        LinesGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(UpdateTotals);
        UpdateTotals();
    }

    public GeneratedInvoiceOptions? Result { get; private set; }
    public List<ManualInvoiceLineInput> ResultLines { get; private set; } = new();

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        _lines.Add(new ManualInvoiceLineInput
        {
            Position = _lines.Count + 1,
            Quantity = 1m,
            Unit = "ST",
            PriceBasisQuantity = 1m
        });
        LinesGrid.SelectedItem = _lines.LastOrDefault();
        UpdateTotals();
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (LinesGrid.SelectedItem is not ManualInvoiceLineInput selected)
        {
            MessageBox.Show("Bitte zuerst eine Position markieren.", "Entwurf", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _lines.Remove(selected);
        RenumberLines();
        UpdateTotals();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DeliveryDatePicker.SelectedDate is null)
        {
            MessageBox.Show("Bitte ein Lieferdatum auswaehlen.", "Entwurf", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_lines.Count == 0)
        {
            MessageBox.Show("Bitte mindestens eine Position erfassen.", "Entwurf", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var normalizedLines = new List<ManualInvoiceLineInput>();
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var validationError = ValidateLine(line, i + 1);
            if (validationError is not null)
            {
                MessageBox.Show(validationError, "Entwurf", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            normalizedLines.Add(CloneLine(line, i + 1));
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
        ResultLines = normalizedLines;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RenumberLines()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].Position = i + 1;
        }

        LinesGrid.Items.Refresh();
    }

    private void UpdateTotals()
    {
        RenumberLines();
        TotalText.Text = $"Gesamtbetrag Positionen: {_lines.Sum(x => x.LineTotal).ToString("N2", CultureInfo.GetCultureInfo("de-DE"))} EUR";
    }

    private static string? ValidateLine(ManualInvoiceLineInput line, int position)
    {
        if (string.IsNullOrWhiteSpace(line.Description))
        {
            return $"Position {position}: Bitte eine Beschreibung eingeben.";
        }

        if (string.IsNullOrWhiteSpace(line.Unit))
        {
            return $"Position {position}: Bitte eine Einheit eingeben.";
        }

        if (line.Quantity <= 0m)
        {
            return $"Position {position}: Die Menge muss groesser als 0 sein.";
        }

        if (line.NetUnitPrice < 0m)
        {
            return $"Position {position}: Der Netto-Preis darf nicht negativ sein.";
        }

        if (line.MetalSurcharge < 0m)
        {
            return $"Position {position}: Der Metallzuschlag darf nicht negativ sein.";
        }

        if (line.PriceBasisQuantity <= 0m)
        {
            return $"Position {position}: Die Preisbasis muss groesser als 0 sein.";
        }

        return null;
    }

    private static ManualInvoiceLineInput CloneLine(ManualInvoiceLineInput line, int? position = null)
    {
        return new ManualInvoiceLineInput
        {
            Position = position ?? line.Position,
            ArticleNumber = line.ArticleNumber,
            Ean = line.Ean,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            NetUnitPrice = line.NetUnitPrice,
            MetalSurcharge = line.MetalSurcharge,
            GrossListPrice = line.GrossListPrice,
            PriceBasisQuantity = line.PriceBasisQuantity
        };
    }
}
