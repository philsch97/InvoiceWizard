using InvoiceWizard.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InvoiceWizard.Dialogs;

public partial class SelectDraftInvoiceDialog : Window
{
    private readonly List<InvoiceEntity> _allDrafts;

    public SelectDraftInvoiceDialog(IEnumerable<InvoiceEntity> drafts, string customerName)
    {
        InitializeComponent();
        _allDrafts = drafts
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.InvoiceId)
            .ToList();
        DialogHintText.Text = $"Waehle den Entwurf fuer {customerName} aus, an den die aktuell offenen Positionen angehaengt werden sollen.";
        ApplyFilter();
    }

    public InvoiceEntity? SelectedDraft { get; private set; }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void DraftsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DraftsGrid.SelectedItem is InvoiceEntity)
        {
            ConfirmSelection();
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ApplyFilter()
    {
        var search = (SearchTextBox.Text ?? string.Empty).Trim();
        IEnumerable<InvoiceEntity> filtered = _allDrafts;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                (!string.IsNullOrWhiteSpace(x.InvoiceNumber) && x.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.Subject) && x.Subject.Contains(search, StringComparison.OrdinalIgnoreCase))
                || x.InvoiceDirectionLabel.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        DraftsGrid.ItemsSource = filtered.ToList();
        if (DraftsGrid.Items.Count > 0 && DraftsGrid.SelectedItem is null)
        {
            DraftsGrid.SelectedIndex = 0;
        }
    }

    private void ConfirmSelection()
    {
        if (DraftsGrid.SelectedItem is not InvoiceEntity invoice)
        {
            MessageBox.Show("Bitte zuerst einen Entwurf auswaehlen.", "Entwurf auswaehlen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedDraft = invoice;
        DialogResult = true;
    }
}
