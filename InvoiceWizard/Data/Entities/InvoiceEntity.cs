using System;
using System.Collections.Generic;

namespace InvoiceWizard.Data.Entities;

public class InvoiceEntity
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public bool HasSupplierInvoice { get; set; } = true;
    public string SupplierName { get; set; } = "Sonepar";
    public string SourcePdfPath { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string DisplayNumber => HasSupplierInvoice ? InvoiceNumber : "Keine Rechnung";
    public string ExpenseStatus => HasSupplierInvoice ? "Mit Rechnung" : "Ohne Rechnung";
    public List<InvoiceLineEntity> Lines { get; set; } = new();
}
