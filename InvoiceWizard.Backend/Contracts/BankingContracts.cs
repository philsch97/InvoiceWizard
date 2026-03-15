using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class ImportBankStatementCsvRequest
{
    [Required]
    public string FileName { get; set; } = "";

    [Required]
    public string CsvContentBase64 { get; set; } = "";
}

public class ImportBankStatementCsvResponseDto
{
    public int ImportId { get; set; }
    public string FileName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountIban { get; set; } = "";
    public string Currency { get; set; } = "EUR";
    public int ImportedTransactions { get; set; }
    public int SkippedTransactions { get; set; }
    public decimal? CurrentBalance { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class BankTransactionListItemDto
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
    public List<BankTransactionAssignmentDto> Assignments { get; set; } = [];
}

public class BankTransactionAssignmentDto
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
}

public class BankInvoiceCandidateDto
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
}

public class AssignBankTransactionRequest
{
    public int? SupplierInvoiceId { get; set; }
    public string? CustomerInvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    [Range(0.01, 100000000)]
    public decimal? AssignedAmount { get; set; }
    [MaxLength(500)]
    public string Note { get; set; } = "";
}

public class BankAccountSummaryDto
{
    public int TransactionCount { get; set; }
    public decimal? CurrentBalance { get; set; }
    public DateTime? LastBookingDate { get; set; }
    public string AccountIban { get; set; } = "";
    public string AccountName { get; set; } = "";
}
