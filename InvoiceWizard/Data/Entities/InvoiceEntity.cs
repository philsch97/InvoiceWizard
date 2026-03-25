using System;
using System.Collections.Generic;

namespace InvoiceWizard.Data.Entities;

public class InvoiceEntity
{
    public int InvoiceId { get; set; }
    public string InvoiceDirection { get; set; } = "Expense";
    public int? CustomerId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public bool HasSupplierInvoice { get; set; } = true;
    public string SupplierName { get; set; } = "Sonepar";
    public string AccountingCategory { get; set; } = "MaterialAndGoods";
    public string Subject { get; set; } = "";
    public bool ApplySmallBusinessRegulation { get; set; }
    public string InvoiceStatus { get; set; } = "Finalized";
    public DateTime? DraftSavedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancellationReason { get; set; } = "";
    public decimal InvoiceTotalAmount { get; set; }
    public decimal ShippingCostNet { get; set; }
    public decimal ShippingCostGross { get; set; }
    public string SourcePdfPath { get; set; } = "";
    public string OriginalPdfFileName { get; set; } = "";
    public bool HasStoredPdf { get; set; }
    public string ContentHash { get; set; } = "";
    public string DisplayNumber => HasSupplierInvoice ? InvoiceNumber : "Keine Rechnung";
    public string ExpenseStatus => HasSupplierInvoice ? "Mit Rechnung" : "Ohne Rechnung";
    public string InvoiceDirectionLabel => InvoiceDirection switch
    {
        "Revenue" => "Einnahme",
        "RevenueReduction" => "Einnahmenminderung",
        "ExpenseReduction" => "Ausgabenminderung",
        _ => "Ausgabe"
    };
    public string PartyLabel => IsCustomerDocument ? "Kunde / Auftraggeber" : "Lieferant";
    public bool IsCustomerDocument => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(InvoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase);
    public bool IsCreditNote => string.Equals(InvoiceDirection, "RevenueReduction", StringComparison.OrdinalIgnoreCase)
        || string.Equals(InvoiceDirection, "ExpenseReduction", StringComparison.OrdinalIgnoreCase);
    public bool IsDraft => string.Equals(InvoiceStatus, "Draft", StringComparison.OrdinalIgnoreCase);
    public bool IsReview => string.Equals(InvoiceStatus, "Review", StringComparison.OrdinalIgnoreCase);
    public bool IsFinalized => string.Equals(InvoiceStatus, "Finalized", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
    public string InvoiceStatusLabel => InvoiceStatus switch
    {
        "Review" => "Prüfen",
        "Draft" => "Entwurf",
        "Cancelled" => "Storniert",
        _ => "Final"
    };
    public bool CanEditDraft => IsCustomerDocument && IsDraft;
    public bool CanFinalizeDraft => CanEditDraft;
    public bool CanLoadForReview => (string.Equals(InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase)
        || string.Equals(InvoiceDirection, "ExpenseReduction", StringComparison.OrdinalIgnoreCase)) && IsReview;
    public bool CanCancel => IsCustomerDocument && !IsCancelled;
    public bool CanDelete => string.Equals(InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase)
        || string.Equals(InvoiceDirection, "ExpenseReduction", StringComparison.OrdinalIgnoreCase)
        || (IsCustomerDocument && IsDraft);
    public string AccountingCategoryLabel => AccountingCategory switch
    {
        "Tools" => "Werkzeug",
        "Services" => "Dienstleistungen",
        "Office" => "Buero",
        "Vehicle" => "Fahrzeug",
        "Other" => "Sonstiges",
        _ => "Material und Waren"
    };
    public List<InvoiceLineEntity> Lines { get; set; } = new();
}
