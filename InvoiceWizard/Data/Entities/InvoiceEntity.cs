using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvoiceWizard.Data.Entities;

public class InvoiceEntity
{
    public int InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }

    public string SupplierName { get; set; } = "Sonepar";
    public string SourcePdfPath { get; set; } = "";

    // zur Dubletten-Erkennung (z.B. SHA256 der XML oder der relevanten Header+Totals)
    public string ContentHash { get; set; } = "";

    public List<InvoiceLineEntity> Lines { get; set; } = new();
}

