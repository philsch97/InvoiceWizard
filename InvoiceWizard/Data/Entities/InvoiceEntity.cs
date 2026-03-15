using System;
using System.Collections.Generic;

namespace InvoiceWizard.Data.Entities;

public class InvoiceEntity
{
    public int InvoiceId { get; set; }
    public string InvoiceDirection { get; set; } = "Expense";
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public bool HasSupplierInvoice { get; set; } = true;
    public string SupplierName { get; set; } = "Sonepar";
    public string AccountingCategory { get; set; } = "MaterialAndGoods";
    public decimal InvoiceTotalAmount { get; set; }
    public string SourcePdfPath { get; set; } = "";
    public string OriginalPdfFileName { get; set; } = "";
    public bool HasStoredPdf { get; set; }
    public string ContentHash { get; set; } = "";
    public string DisplayNumber => HasSupplierInvoice ? InvoiceNumber : "Keine Rechnung";
    public string ExpenseStatus => HasSupplierInvoice ? "Mit Rechnung" : "Ohne Rechnung";
    public string InvoiceDirectionLabel => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) ? "Einnahme" : "Ausgabe";
    public string PartyLabel => string.Equals(InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase) ? "Kunde / Auftraggeber" : "Lieferant";
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
