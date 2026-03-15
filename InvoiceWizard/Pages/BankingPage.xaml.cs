using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Dialogs;
using Microsoft.Win32;

namespace InvoiceWizard;

public partial class BankingPage : Page
{
    private List<BankTransactionEntity> _transactions = [];

    public BankingPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(int? selectedTransactionId = null)
    {
        try
        {
            var summary = await App.Api.GetBankingSummaryAsync();
            PopulateSummary(summary);

            _transactions = await App.Api.GetBankTransactionsAsync(ShowAssignedCheckBox.IsChecked == true, ShowIgnoredCheckBox.IsChecked == true);
            TransactionsGrid.ItemsSource = _transactions;

            var selected = selectedTransactionId.HasValue
                ? _transactions.FirstOrDefault(x => x.BankTransactionId == selectedTransactionId.Value)
                : _transactions.FirstOrDefault();
            TransactionsGrid.SelectedItem = selected;
            await LoadSelectionDetailsAsync(selected);

            SetStatus("Bankbuchungen wurden geladen.", StatusMessageType.Info);
        }
        catch (Exception ex)
        {
            SetStatus($"Bankdaten konnten nicht geladen werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private void PopulateSummary(BankAccountSummaryEntity summary)
    {
        AccountNameText.Text = string.IsNullOrWhiteSpace(summary.AccountName) ? "-" : summary.AccountName;
        AccountIbanText.Text = string.IsNullOrWhiteSpace(summary.AccountIban) ? "Keine IBAN erkannt" : summary.AccountIban;
        CurrentBalanceText.Text = summary.CurrentBalance.HasValue
            ? $"{summary.CurrentBalance.Value:N2} EUR"
            : "Noch kein Saldo";
        LastBookingText.Text = summary.LastBookingDate.HasValue
            ? $"Letzte Buchung: {summary.LastBookingDate.Value:dd.MM.yyyy}"
            : "Noch keine Buchungen importiert";
        TransactionCountText.Text = summary.TransactionCount.ToString(CultureInfo.InvariantCulture);
        ImportInfoText.Text = "Importiere am besten den CSV-Kontoauszug direkt aus dem Online-Banking. Fuer die erste Version werden eingehende Buchungen Kundenrechnungen und ausgehende Buchungen Lieferantenrechnungen zugeordnet.";
    }

    private async Task LoadSelectionDetailsAsync(BankTransactionEntity? transaction)
    {
        if (transaction is null)
        {
            SelectedTransactionText.Text = "Noch keine Buchung ausgewaehlt.";
            SelectedTransactionDetailsText.Text = "Waehle links eine Buchung aus, um passende Rechnungen zu sehen.";
            AssignmentsGrid.ItemsSource = null;
            CandidatesGrid.ItemsSource = null;
            CandidateHintText.Text = "Noch keine Rechnungsvorschlaege geladen.";
            IgnoredInfoText.Visibility = Visibility.Collapsed;
            IgnoreTransactionButton.IsEnabled = false;
            AssignWithoutReceiptButton.IsEnabled = false;
            UnignoreTransactionButton.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedTransactionText.Text = $"{transaction.BookingDate:dd.MM.yyyy} | {transaction.AmountLabel} | {transaction.CounterpartyName}";
        SelectedTransactionDetailsText.Text = string.IsNullOrWhiteSpace(transaction.Purpose)
            ? $"Import: {transaction.ImportFileName}"
            : $"{transaction.Purpose}\nImport: {transaction.ImportFileName}";
        if (transaction.IsIgnored)
        {
            IgnoredInfoText.Text = $"Dieser Umsatz wurde ignoriert am {(transaction.IgnoredAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-")}. Grund: {transaction.IgnoredComment}";
            IgnoredInfoText.Visibility = Visibility.Visible;
            IgnoreTransactionButton.IsEnabled = false;
            AssignWithoutReceiptButton.IsEnabled = false;
            UnignoreTransactionButton.Visibility = Visibility.Visible;
        }
        else
        {
            IgnoredInfoText.Visibility = Visibility.Collapsed;
            IgnoreTransactionButton.IsEnabled = true;
            AssignWithoutReceiptButton.IsEnabled = transaction.Amount < 0m;
            UnignoreTransactionButton.Visibility = Visibility.Collapsed;
        }

        AssignmentsGrid.ItemsSource = transaction.Assignments;

            var directionHint = transaction.Amount >= 0m
            ? "Fuer diese eingehende Buchung werden passende Einnahmerechnungen und Kundenrechnungen vorgeschlagen."
            : "Fuer diese ausgehende Buchung werden passende Lieferantenrechnungen vorgeschlagen.";

        if (transaction.IsIgnored)
        {
            CandidatesGrid.ItemsSource = null;
            CandidateHintText.Text = "Ignorierte Umsaetze werden nicht mehr automatisch zugeordnet.";
            return;
        }

        try
        {
            var candidates = await App.Api.GetBankInvoiceCandidatesAsync(transaction.BankTransactionId);
            CandidatesGrid.ItemsSource = candidates;
            CandidateHintText.Text = candidates.Count == 0
                ? $"{directionHint} Aktuell wurde keine offene Rechnung erkannt."
                : $"{directionHint} Du kannst auch mehrere Treffer markieren und gesammelt zuordnen.";
        }
        catch (Exception ex)
        {
            CandidatesGrid.ItemsSource = null;
            CandidateHintText.Text = $"Rechnungsvorschlaege konnten nicht geladen werden: {ex.Message}";
        }
    }

    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Kontoauszug (*.zip;*.xml;*.csv;*.txt)|*.zip;*.xml;*.csv;*.txt|ZIP-Datei (*.zip)|*.zip|XML-Datei (*.xml)|*.xml|CSV-Datei (*.csv)|*.csv|Textdatei (*.txt)|*.txt|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            var result = await App.Api.ImportBankStatementFileAsync(Path.GetFileName(dialog.FileName), bytes);
            await ReloadAsync();
            var warningText = result.Warnings.Count == 0 ? string.Empty : $" Hinweise: {string.Join(" | ", result.Warnings)}";
            SetStatus($"{result.ImportedTransactions} Buchung(en) importiert, {result.SkippedTransactions} Duplikat(e) uebersprungen.{warningText}", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Kontoauszug-Import fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync((TransactionsGrid.SelectedItem as BankTransactionEntity)?.BankTransactionId);
    }

    private async void TransactionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadSelectionDetailsAsync(TransactionsGrid.SelectedItem as BankTransactionEntity);
    }

    private async void AssignCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionsGrid.SelectedItem is not BankTransactionEntity transaction)
        {
            SetStatus("Bitte zuerst eine Buchung auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var selectedCandidates = CandidatesGrid.SelectedItems.OfType<BankInvoiceCandidateEntity>().ToList();
        if (selectedCandidates.Count == 0)
        {
            SetStatus("Bitte zuerst mindestens eine passende Rechnung auswaehlen.", StatusMessageType.Warning);
            return;
        }

        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(AssignedAmountText.Text))
        {
            if (!TryParseDecimal(AssignedAmountText.Text, out var parsedAmount) || parsedAmount <= 0m)
            {
                SetStatus("Bitte einen gueltigen Zuordnungsbetrag eingeben.", StatusMessageType.Warning);
                return;
            }

            amount = parsedAmount;
        }

        if (selectedCandidates.Count > 1 && amount.HasValue)
        {
            SetStatus("Ein manueller Zuordnungsbetrag ist nur bei einer einzelnen markierten Rechnung moeglich.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var orderedCandidates = CandidatesGrid.Items.OfType<BankInvoiceCandidateEntity>()
                .Where(candidate => selectedCandidates.Contains(candidate))
                .ToList();
            BankTransactionEntity? updated = null;
            foreach (var candidate in orderedCandidates)
            {
                updated = await App.Api.AssignBankTransactionAsync(transaction.BankTransactionId, candidate, orderedCandidates.Count == 1 ? amount : null, AssignmentNoteText.Text);
                if (updated.RemainingAmount <= 0.009m)
                {
                    break;
                }
            }

            if (updated is null)
            {
                SetStatus("Es konnte keine Zuordnung erstellt werden.", StatusMessageType.Warning);
                return;
            }

            await ReloadAsync(updated.BankTransactionId);
            AssignedAmountText.Clear();
            AssignmentNoteText.Clear();
            SetStatus(selectedCandidates.Count == 1
                ? $"Buchung wurde der Rechnung {orderedCandidates[0].DisplayNumber} zugeordnet."
                : $"{orderedCandidates.Count} Rechnungen wurden der Buchung nacheinander zugeordnet.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Zuordnung fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void DeleteAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (AssignmentsGrid.SelectedItem is not BankTransactionAssignmentEntity assignment)
        {
            SetStatus("Bitte zuerst eine bestehende Zuordnung markieren.", StatusMessageType.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Soll die Zuordnung zur Rechnung {assignment.DisplayNumber} wirklich geloescht werden?",
                "Zuordnung loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var selectedTransactionId = (TransactionsGrid.SelectedItem as BankTransactionEntity)?.BankTransactionId;
            await App.Api.DeleteBankTransactionAssignmentAsync(assignment.BankTransactionAssignmentId);
            await ReloadAsync(selectedTransactionId);
            SetStatus("Zuordnung wurde entfernt.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Zuordnung konnte nicht geloescht werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void ShowAssignedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await ReloadAsync();
    }

    private async void IgnoreTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionsGrid.SelectedItem is not BankTransactionEntity transaction)
        {
            SetStatus("Bitte zuerst eine Buchung auswaehlen.", StatusMessageType.Warning);
            return;
        }

        var dialog = new TextPromptDialog(
            "Umsatz ignorieren",
            "Bitte begruende, warum dieser Umsatz ignoriert werden soll. Ohne Kommentar kann er nicht ausgeblendet werden.")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var updated = await App.Api.IgnoreBankTransactionAsync(transaction.BankTransactionId, dialog.Result);
            await ReloadAsync(ShowIgnoredCheckBox.IsChecked == true ? updated.BankTransactionId : null);
            SetStatus("Umsatz wurde ignoriert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Umsatz konnte nicht ignoriert werden: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void AssignWithoutReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionsGrid.SelectedItem is not BankTransactionEntity transaction)
        {
            SetStatus("Bitte zuerst eine Buchung auswaehlen.", StatusMessageType.Warning);
            return;
        }

        if (transaction.Amount >= 0m)
        {
            SetStatus("Zuordnung ohne Beleg ist nur fuer ausgehende Buchungen moeglich.", StatusMessageType.Warning);
            return;
        }

        var dialog = new ManualBankAssignmentDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var updated = await App.Api.AssignBankTransactionWithoutReceiptAsync(transaction.BankTransactionId, dialog.SelectedCategory, null, dialog.Note);
            await ReloadAsync(updated.BankTransactionId);
            SetStatus("Buchung wurde ohne Beleg zugeordnet.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Zuordnung ohne Beleg fehlgeschlagen: {ex.Message}", StatusMessageType.Error);
        }
    }

    private async void UnignoreTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionsGrid.SelectedItem is not BankTransactionEntity transaction)
        {
            SetStatus("Bitte zuerst eine Buchung auswaehlen.", StatusMessageType.Warning);
            return;
        }

        try
        {
            var updated = await App.Api.UnignoreBankTransactionAsync(transaction.BankTransactionId);
            await ReloadAsync(updated.BankTransactionId);
            SetStatus("Ignorierter Umsatz wurde wieder aktiviert.", StatusMessageType.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Ignorierung konnte nicht aufgehoben werden: {ex.Message}", StatusMessageType.Error);
        }
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

    private Brush GetBrush(StatusMessageType type, string variant)
    {
        var key = type switch
        {
            StatusMessageType.Success => $"StatusSuccess{variant}Brush",
            StatusMessageType.Warning => $"StatusWarning{variant}Brush",
            StatusMessageType.Error => $"StatusError{variant}Brush",
            _ => $"StatusInfo{variant}Brush"
        };

        return (Brush)FindResource(key);
    }
}
