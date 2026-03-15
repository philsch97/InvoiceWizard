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
    public string SourcePdfPath { get; set; } = "";
    public string OriginalPdfFileName { get; set; } = "";
    public bool HasStoredPdf { get; set; }
    public string ContentHash { get; set; } = "";
    public string DisplayNumber => HasSupplierInvoice ? InvoiceNumber : "Keine Rechnung";
    public string ExpenseStatus => HasSupplierInvoice ? "Mit Rechnung" : "Ohne Rechnung";
    public string InvoiceDirectionLabel => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) ? "Einnahme" : "Ausgabe";
    public string PartyLabel => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) ? "Kunde / Auftraggeber" : "Lieferant";
    public bool IsDraft => string.Equals(InvoiceStatus, "Draft", StringComparison.OrdinalIgnoreCase);
    public bool IsFinalized => string.Equals(InvoiceStatus, "Finalized", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
    public string InvoiceStatusLabel => InvoiceStatus switch
    {
        "Draft" => "Entwurf",
        "Cancelled" => "Storniert",
        _ => "Final"
    };
    public bool CanEditDraft => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) && IsDraft;
    public bool CanFinalizeDraft => CanEditDraft;
    public bool CanCancel => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) && !IsCancelled;
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
