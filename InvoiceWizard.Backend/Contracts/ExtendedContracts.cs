using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class InvoiceLineItemDto
{
    public int InvoiceLineId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public bool HasSupplierInvoice { get; set; }
    public string AccountingCategory { get; set; } = "";
    public int Position { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal PriceBasisQuantity { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<AllocationItemDto> Allocations { get; set; } = new();
}

public class AllocationItemDto
{
    public int LineAllocationId { get; set; }
    public int InvoiceLineId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public bool HasSupplierInvoice { get; set; }
    public string AccountingCategory { get; set; } = "";
    public string ArticleNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal PriceBasisQuantity { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal CustomerUnitPrice { get; set; }
    public bool IsSmallMaterial { get; set; }
    public DateTime AllocatedAt { get; set; }
    public string? CustomerInvoiceNumber { get; set; }
    public DateTime? CustomerInvoicedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal ExportedMarkupPercent { get; set; }
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
}

public class SaveInvoiceRequest
{
    public bool HasSupplierInvoice { get; set; } = true;
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string SupplierName { get; set; } = "";
    public string AccountingCategory { get; set; } = "MaterialAndGoods";
    public string SourcePdfPath { get; set; } = "";
    public string OriginalPdfFileName { get; set; } = "";
    public string? PdfContentBase64 { get; set; }
    [Required]
    public string ContentHash { get; set; } = "";
    public List<SaveInvoiceLineRequest> Lines { get; set; } = new();
}

public class InvoiceListItemDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string SupplierName { get; set; } = "";
    public bool HasSupplierInvoice { get; set; }
    public string AccountingCategory { get; set; } = "";
    public string OriginalPdfFileName { get; set; } = "";
    public bool HasStoredPdf { get; set; }
}

public class SaveInvoiceLineRequest
{
    public int Position { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string Ean { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public decimal MetalSurcharge { get; set; }
    public decimal GrossListPrice { get; set; }
    public decimal PriceBasisQuantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class SaveAllocationRequest
{
    public int InvoiceLineId { get; set; }
    public int CustomerId { get; set; }
    public int? ProjectId { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal CustomerUnitPrice { get; set; }
    public bool IsSmallMaterial { get; set; }
}

public class UpdateAllocationQuantityRequest
{
    public decimal AllocatedQuantity { get; set; }
}

public class UpdateAllocationStatusRequest
{
    public string? CustomerInvoiceNumber { get; set; }
    public bool MarkInvoiced { get; set; }
    public bool MarkPaid { get; set; }
}

public class UpdateAllocationExportRequest
{
    public decimal ExportedMarkupPercent { get; set; }
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
}

public class UpdateWorkTimeExportRequest
{
    public decimal ExportedUnitPrice { get; set; }
    public decimal ExportedLineTotal { get; set; }
    public DateTime? LastExportedAt { get; set; }
}

public class AnalyticsResponseDto
{
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
    public decimal OpenRevenue { get; set; }
    public List<AnalyticsMonthDto> Monthly { get; set; } = new();
    public List<ProjectAnalyticsRowDto> Projects { get; set; } = new();
    public List<ExpenseCategoryTotalDto> ExpenseCategories { get; set; } = new();
}

public class AnalyticsMonthDto
{
    public string Label { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public double RevenueHeight { get; set; }
    public double ExpenseHeight { get; set; }
}

public class ProjectAnalyticsRowDto
{
    public string CustomerName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public decimal PaidRevenue { get; set; }
    public decimal OpenRevenue { get; set; }
    public decimal LoggedHours { get; set; }
    public int OpenItemCount { get; set; }
}

public class ExpenseCategoryTotalDto
{
    public string AccountingCategory { get; set; } = "";
    public decimal Amount { get; set; }
}
