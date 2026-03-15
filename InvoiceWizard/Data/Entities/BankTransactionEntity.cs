namespace InvoiceWizard.Data.Entities;

public class BankTransactionEntity
{
    public int BankTransactionId { get; set; }
    public int ImportId { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal? BalanceAfterBooking { get; set; }
    public string Currency { get; set; } = "EUR";
    public string CounterpartyName { get; set; } = "";
    public string CounterpartyIban { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Reference { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string AccountIban { get; set; } = "";
    public string ImportFileName { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public decimal AssignedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public List<BankTransactionAssignmentEntity> Assignments { get; set; } = new();

    public string AmountLabel => $"{Amount:N2} {Currency}";
    public string RemainingAmountLabel => $"{RemainingAmount:N2} {Currency}";
    public string DirectionLabel => Amount >= 0m ? "Eingang" : "Ausgang";
    public string AssignmentSummary => Assignments.Count == 0
        ? "Offen"
        : string.Join(" | ", Assignments.Select(x => $"{x.DisplayNumber} ({x.AssignedAmount:N2} {Currency})"));
}

public class BankTransactionAssignmentEntity
{
    public int BankTransactionAssignmentId { get; set; }
    public int BankTransactionId { get; set; }
    public string AssignmentType { get; set; } = "";
    public int? SupplierInvoiceId { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? CustomerInvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    public string PartyName { get; set; } = "";
    public decimal AssignedAmount { get; set; }
    public string Note { get; set; } = "";
    public DateTime AssignedAt { get; set; }
    public string AssignmentTypeLabel => string.Equals(AssignmentType, "SupplierInvoice", StringComparison.OrdinalIgnoreCase)
        ? "Lieferantenrechnung"
        : "Kundenrechnung";
    public string DisplayNumber => string.Equals(AssignmentType, "SupplierInvoice", StringComparison.OrdinalIgnoreCase)
        ? SupplierInvoiceNumber ?? "-"
        : CustomerInvoiceNumber ?? "-";
}

public class BankInvoiceCandidateEntity
{
    public string CandidateType { get; set; } = "";
    public int? SupplierInvoiceId { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? CustomerInvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    public string PartyName { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AssignedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsPaid { get; set; }
    public decimal MatchScore { get; set; }
    public string MatchReason { get; set; } = "";
    public string DisplayNumber => string.Equals(CandidateType, "SupplierInvoice", StringComparison.OrdinalIgnoreCase)
        ? SupplierInvoiceNumber ?? "-"
        : CustomerInvoiceNumber ?? "-";
    public string CandidateTypeLabel => string.Equals(CandidateType, "SupplierInvoice", StringComparison.OrdinalIgnoreCase)
        ? "Lieferantenrechnung"
        : "Kundenrechnung";
}

public class BankAccountSummaryEntity
{
    public int TransactionCount { get; set; }
    public decimal? CurrentBalance { get; set; }
    public DateTime? LastBookingDate { get; set; }
    public string AccountIban { get; set; } = "";
    public string AccountName { get; set; } = "";
}

public class BankImportResultEntity
{
    public int ImportId { get; set; }
    public string FileName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountIban { get; set; } = "";
    public string Currency { get; set; } = "EUR";
    public int ImportedTransactions { get; set; }
    public int SkippedTransactions { get; set; }
    public decimal? CurrentBalance { get; set; }
    public List<string> Warnings { get; set; } = new();
}
