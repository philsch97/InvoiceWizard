using System.Text.RegularExpressions;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/invoices")]
public partial class InvoicesController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("reserve-revenue-number")]
    public async Task<ActionResult<ReserveRevenueInvoiceNumberResponse>> ReserveRevenueInvoiceNumber([FromBody] ReserveRevenueInvoiceNumberRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var tenant = await db.Tenants.FirstAsync(x => x.TenantId == tenantId, HttpContext.RequestAborted);
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId, HttpContext.RequestAborted);
        if (customer is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(customer.CustomerNumber))
        {
            while (true)
            {
                var generatedCustomerNumber = $"K-{tenant.NextCustomerNumber:D5}";
                tenant.NextCustomerNumber++;
                var customerNumberExists = await db.Customers.AnyAsync(x => x.TenantId == tenantId && x.CustomerNumber == generatedCustomerNumber, HttpContext.RequestAborted);
                if (!customerNumberExists)
                {
                    customer.CustomerNumber = generatedCustomerNumber;
                    break;
                }
            }
        }

        string invoiceNumber;
        while (true)
        {
            invoiceNumber = $"RE-{DateTime.Today:yyyy}-{tenant.NextRevenueInvoiceNumber:D4}";
            tenant.NextRevenueInvoiceNumber++;
            var exists = await db.Invoices.AnyAsync(x => x.TenantId == tenantId && x.InvoiceDirection == "Revenue" && x.InvoiceNumber == invoiceNumber, HttpContext.RequestAborted);
            if (!exists)
            {
                break;
            }
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(new ReserveRevenueInvoiceNumberResponse
        {
            InvoiceNumber = invoiceNumber,
            CustomerId = customer.CustomerId,
            CustomerName = customer.Name,
            CustomerNumber = customer.CustomerNumber
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveInvoice([FromBody] SaveInvoiceRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var normalizedDirection = NormalizeInvoiceDirection(request.InvoiceDirection);
        var requestedStatus = NormalizeInvoiceStatus(request.InvoiceStatus);
        var requiresSupplierFields = string.Equals(normalizedDirection, "Expense", StringComparison.OrdinalIgnoreCase)
            && request.HasSupplierInvoice
            && !string.Equals(requestedStatus, "Review", StringComparison.OrdinalIgnoreCase);

        if (requiresSupplierFields && request.InvoiceTotalAmount <= 0m)
        {
            return ValidationProblem("Bitte einen Rechnungsbetrag groesser als 0 angeben.");
        }

        if (requiresSupplierFields && !request.PaymentDueDate.HasValue)
        {
            return ValidationProblem("Bitte ein Zahlungsdatum / Faelligkeitsdatum angeben.");
        }

        var exists = await db.Invoices.AnyAsync(x => x.TenantId == tenantId && x.ContentHash == request.ContentHash);
        if (exists)
        {
            return Conflict(new { message = "Invoice already exists." });
        }

        if (request.CustomerId.HasValue)
        {
            var customerExists = await db.Customers.AnyAsync(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId.Value, HttpContext.RequestAborted);
            if (!customerExists)
            {
                return ValidationProblem("Der ausgewaehlte Kunde wurde nicht gefunden.");
            }
        }

        var fallbackNumber = $"MANUELL-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var invoice = new Invoice
        {
            TenantId = tenantId,
            CustomerId = request.CustomerId,
            InvoiceDirection = normalizedDirection,
            InvoiceStatus = requestedStatus,
            HasSupplierInvoice = request.HasSupplierInvoice,
            InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? fallbackNumber : request.InvoiceNumber.Trim(),
            InvoiceDate = request.InvoiceDate,
            DeliveryDate = request.DeliveryDate?.Date,
            PaymentDueDate = request.PaymentDueDate?.Date,
            SupplierName = request.SupplierName.Trim(),
            AccountingCategory = NormalizeAccountingCategory(request.AccountingCategory),
            Subject = request.Subject.Trim(),
            ApplySmallBusinessRegulation = request.ApplySmallBusinessRegulation,
            DraftSavedAt = string.Equals(requestedStatus, "Draft", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            FinalizedAt = string.Equals(requestedStatus, "Finalized", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            InvoiceTotalAmount = decimal.Round(request.InvoiceTotalAmount, 2),
            ShippingCostNet = decimal.Round(request.ShippingCostNet, 2),
            ShippingCostGross = decimal.Round(request.ShippingCostGross, 2),
            SourcePdfPath = request.SourcePdfPath.Trim(),
            OriginalPdfFileName = string.IsNullOrWhiteSpace(request.OriginalPdfFileName) ? "" : request.OriginalPdfFileName.Trim(),
            ContentHash = request.ContentHash.Trim(),
            Lines = request.Lines.Select(line => new InvoiceLine
            {
                TenantId = tenantId,
                Position = line.Position,
                ArticleNumber = line.ArticleNumber,
                Ean = line.Ean,
                Description = line.Description,
                Quantity = line.Quantity,
                Unit = line.Unit,
                NetUnitPrice = line.NetUnitPrice,
                MetalSurcharge = line.MetalSurcharge,
                GrossListPrice = line.GrossListPrice,
                GrossUnitPrice = line.GrossUnitPrice,
                PriceBasisQuantity = line.PriceBasisQuantity,
                ShippingNetShare = line.ShippingNetShare,
                ShippingGrossShare = line.ShippingGrossShare,
                LineTotal = line.LineTotal,
                GrossLineTotal = line.GrossLineTotal
            }).ToList()
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(request.PdfContentBase64))
        {
            invoice.StoredPdfContent = DecodePdf(request.PdfContentBase64);
            invoice.StoredPdfPath = string.Empty;
            await db.SaveChangesAsync();
        }

        return Ok(new { invoice.InvoiceId, lineCount = invoice.Lines.Count });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItemDto>>> GetInvoices()
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var items = await db.Invoices
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.InvoiceId)
            .Select(x => new InvoiceListItemDto
            {
                InvoiceId = x.InvoiceId,
                InvoiceDirection = x.InvoiceDirection,
                InvoiceStatus = x.InvoiceStatus,
                InvoiceNumber = x.InvoiceNumber,
                InvoiceDate = x.InvoiceDate,
                DeliveryDate = x.DeliveryDate,
                PaymentDueDate = x.PaymentDueDate,
                CustomerId = x.CustomerId,
                SupplierName = x.SupplierName,
                HasSupplierInvoice = x.HasSupplierInvoice,
                AccountingCategory = x.AccountingCategory,
                Subject = x.Subject,
                ApplySmallBusinessRegulation = x.ApplySmallBusinessRegulation,
                InvoiceTotalAmount = x.InvoiceTotalAmount,
                ShippingCostNet = x.ShippingCostNet,
                ShippingCostGross = x.ShippingCostGross,
                OriginalPdfFileName = x.OriginalPdfFileName,
                HasStoredPdf = x.StoredPdfContent.Length > 0 || !string.IsNullOrWhiteSpace(x.StoredPdfPath),
                DraftSavedAt = x.DraftSavedAt,
                FinalizedAt = x.FinalizedAt,
                CancelledAt = x.CancelledAt,
                CancellationReason = x.CancellationReason
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{invoiceId:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> GetInvoice(int invoiceId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices
            .Include(x => x.Lines.OrderBy(l => l.Position))
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(MapInvoiceDetail(invoice));
    }

    [HttpPut("{invoiceId:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> UpdateInvoice(int invoiceId, [FromBody] SaveInvoiceRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        var currentStatus = NormalizeInvoiceStatus(invoice.InvoiceStatus);
        var requestedStatus = NormalizeInvoiceStatus(request.InvoiceStatus);
        if (!string.Equals(currentStatus, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(currentStatus, "Review", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Nur Entwuerfe oder Rechnungen im Status 'Pruefen' koennen nachtraeglich bearbeitet werden.");
        }

        if (request.CustomerId.HasValue)
        {
            var customerExists = await db.Customers.AnyAsync(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId.Value, HttpContext.RequestAborted);
            if (!customerExists)
            {
                return ValidationProblem("Der ausgewaehlte Kunde wurde nicht gefunden.");
            }
        }

        var requiresSupplierFields = string.Equals(NormalizeInvoiceDirection(request.InvoiceDirection), "Expense", StringComparison.OrdinalIgnoreCase)
            && request.HasSupplierInvoice
            && !string.Equals(requestedStatus, "Review", StringComparison.OrdinalIgnoreCase);

        if (requiresSupplierFields && request.InvoiceTotalAmount <= 0m)
        {
            return ValidationProblem("Bitte einen Rechnungsbetrag groesser als 0 angeben.");
        }

        if (requiresSupplierFields && !request.PaymentDueDate.HasValue)
        {
            return ValidationProblem("Bitte ein Zahlungsdatum / Faelligkeitsdatum angeben.");
        }

        invoice.InvoiceStatus = requestedStatus;
        invoice.InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? invoice.InvoiceNumber : request.InvoiceNumber.Trim();
        invoice.CustomerId = request.CustomerId;
        invoice.InvoiceDate = request.InvoiceDate.Date;
        invoice.DeliveryDate = request.DeliveryDate?.Date;
        invoice.PaymentDueDate = request.PaymentDueDate?.Date;
        invoice.SupplierName = request.SupplierName.Trim();
        invoice.AccountingCategory = NormalizeAccountingCategory(request.AccountingCategory);
        invoice.Subject = request.Subject.Trim();
        invoice.ApplySmallBusinessRegulation = request.ApplySmallBusinessRegulation;
        invoice.InvoiceTotalAmount = decimal.Round(request.InvoiceTotalAmount, 2);
        invoice.ShippingCostNet = decimal.Round(request.ShippingCostNet, 2);
        invoice.ShippingCostGross = decimal.Round(request.ShippingCostGross, 2);
        invoice.SourcePdfPath = request.SourcePdfPath.Trim();
        invoice.OriginalPdfFileName = string.IsNullOrWhiteSpace(request.OriginalPdfFileName) ? "" : request.OriginalPdfFileName.Trim();
        invoice.ContentHash = request.ContentHash.Trim();
        invoice.DraftSavedAt = string.Equals(requestedStatus, "Draft", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : invoice.DraftSavedAt;
        invoice.FinalizedAt = string.Equals(requestedStatus, "Finalized", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : invoice.FinalizedAt;

        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines = request.Lines.Select(line => new InvoiceLine
        {
            TenantId = tenantId,
            Position = line.Position,
            ArticleNumber = line.ArticleNumber,
            Ean = line.Ean,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            NetUnitPrice = line.NetUnitPrice,
            MetalSurcharge = line.MetalSurcharge,
            GrossListPrice = line.GrossListPrice,
            GrossUnitPrice = line.GrossUnitPrice,
            PriceBasisQuantity = line.PriceBasisQuantity,
            ShippingNetShare = line.ShippingNetShare,
            ShippingGrossShare = line.ShippingGrossShare,
            LineTotal = line.LineTotal,
            GrossLineTotal = line.GrossLineTotal
        }).ToList();

        if (!string.IsNullOrWhiteSpace(request.PdfContentBase64))
        {
            invoice.StoredPdfContent = DecodePdf(request.PdfContentBase64);
            invoice.StoredPdfPath = string.Empty;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(MapInvoiceDetail(invoice));
    }

    [HttpPost("{invoiceId:int}/finalize")]
    public async Task<ActionResult<InvoiceDetailDto>> FinalizeInvoice(int invoiceId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!string.Equals(invoice.InvoiceStatus, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Nur Entwuerfe koennen finalisiert werden.");
        }

        invoice.InvoiceStatus = "Finalized";
        invoice.FinalizedAt = DateTime.UtcNow;

        var allocations = await db.LineAllocations.Where(x => x.TenantId == tenantId && x.RevenueInvoiceId == invoiceId).ToListAsync(HttpContext.RequestAborted);
        foreach (var allocation in allocations)
        {
            allocation.CustomerInvoiceNumber = invoice.InvoiceNumber;
            allocation.CustomerInvoicedAt ??= invoice.FinalizedAt;
        }

        var workEntries = await db.WorkTimeEntries.Where(x => x.TenantId == tenantId && x.RevenueInvoiceId == invoiceId).ToListAsync(HttpContext.RequestAborted);
        foreach (var workEntry in workEntries)
        {
            workEntry.CustomerInvoiceNumber = invoice.InvoiceNumber;
            workEntry.CustomerInvoicedAt ??= invoice.FinalizedAt;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);

        var updated = await db.Invoices.Include(x => x.Lines.OrderBy(l => l.Position)).FirstAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        return Ok(MapInvoiceDetail(updated));
    }

    [HttpPost("{invoiceId:int}/cancel")]
    public async Task<ActionResult<InvoiceDetailDto>> CancelInvoice(int invoiceId, [FromBody] CancelInvoiceRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        if (string.Equals(invoice.InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Diese Rechnung wurde bereits storniert.");
        }

        var hasBankAssignments = await db.BankTransactionAssignments.AnyAsync(x => x.TenantId == tenantId && x.RevenueInvoiceId == invoiceId, HttpContext.RequestAborted);
        if (hasBankAssignments)
        {
            return ValidationProblem("Diese Rechnung hat bereits Zahlungszuordnungen. Bitte zuerst die Umsatzzuordnung pruefen oder entfernen.");
        }

        var allocations = await db.LineAllocations.Where(x => x.TenantId == tenantId && x.RevenueInvoiceId == invoiceId).ToListAsync(HttpContext.RequestAborted);
        var workEntries = await db.WorkTimeEntries.Where(x => x.TenantId == tenantId && x.RevenueInvoiceId == invoiceId).ToListAsync(HttpContext.RequestAborted);
        if (allocations.Any(x => x.IsPaid) || workEntries.Any(x => x.IsPaid))
        {
            return ValidationProblem("Bereits bezahlte Rechnungen koennen hier nicht storniert werden. Bitte zuerst die Zahlungszuordnung pruefen.");
        }

        invoice.InvoiceStatus = "Cancelled";
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.CancellationReason = request.Reason.Trim();

        foreach (var allocation in allocations)
        {
            allocation.RevenueInvoiceId = null;
            allocation.CustomerInvoiceNumber = null;
            allocation.CustomerInvoicedAt = null;
        }

        foreach (var workEntry in workEntries)
        {
            workEntry.RevenueInvoiceId = null;
            workEntry.CustomerInvoiceNumber = null;
            workEntry.CustomerInvoicedAt = null;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        var updated = await db.Invoices.Include(x => x.Lines.OrderBy(l => l.Position)).FirstAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        return Ok(MapInvoiceDetail(updated));
    }

    [HttpDelete("{invoiceId:int}")]
    public async Task<IActionResult> DeleteInvoice(int invoiceId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        var normalizedStatus = NormalizeInvoiceStatus(invoice.InvoiceStatus);
        var isExpenseInvoice = string.Equals(invoice.InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase);
        var isRevenueDraft = string.Equals(invoice.InvoiceDirection, "Revenue", StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedStatus, "Draft", StringComparison.OrdinalIgnoreCase);
        if (!isExpenseInvoice && !isRevenueDraft)
        {
            return ValidationProblem("Geloescht werden koennen nur Ausgaberechnungen oder Einnahme-Entwuerfe.");
        }

        var hasAssignments = await db.BankTransactionAssignments.AnyAsync(
            x => x.TenantId == tenantId && (x.SupplierInvoiceId == invoiceId || x.RevenueInvoiceId == invoiceId),
            HttpContext.RequestAborted);
        if (hasAssignments)
        {
            return ValidationProblem("Diese Rechnung ist bereits mit Bankbuchungen verknuepft und kann nicht geloescht werden.");
        }

        db.InvoiceLines.RemoveRange(invoice.Lines);
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("{invoiceId:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int invoiceId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.StoredPdfContent.Length == 0 && (string.IsNullOrWhiteSpace(invoice.StoredPdfPath) || !System.IO.File.Exists(invoice.StoredPdfPath)))
        {
            return NotFound(new { message = "Fuer diese Rechnung ist keine gespeicherte PDF vorhanden." });
        }

        var fileName = string.IsNullOrWhiteSpace(invoice.OriginalPdfFileName)
            ? $"{SanitizeFileName(invoice.InvoiceNumber)}.pdf"
            : invoice.OriginalPdfFileName;
        var bytes = invoice.StoredPdfContent.Length > 0
            ? invoice.StoredPdfContent
            : await System.IO.File.ReadAllBytesAsync(invoice.StoredPdfPath, HttpContext.RequestAborted);
        return File(bytes, "application/pdf", fileName);
    }

    [HttpPut("{invoiceId:int}/pdf")]
    public async Task<IActionResult> UploadPdf(int invoiceId, [FromBody] UploadInvoicePdfRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (invoice is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.PdfContentBase64))
        {
            return ValidationProblem("Bitte eine PDF-Datei hochladen.");
        }

        invoice.OriginalPdfFileName = request.OriginalPdfFileName.Trim();
        invoice.StoredPdfContent = DecodePdf(request.PdfContentBase64);
        invoice.StoredPdfPath = string.Empty;
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { invoice.InvoiceId, invoice.OriginalPdfFileName });
    }

    private static byte[] DecodePdf(string pdfContentBase64)
        => Convert.FromBase64String(pdfContentBase64);

    private static string NormalizeAccountingCategory(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "Tools" => "Tools",
            "Services" => "Services",
            "Office" => "Office",
            "Vehicle" => "Vehicle",
            "Other" => "Other",
            _ => "MaterialAndGoods"
        };
    }

    private static string NormalizeInvoiceDirection(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "Revenue" => "Revenue",
            _ => "Expense"
        };
    }

    private static string NormalizeInvoiceStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "Review" => "Review",
            "Draft" => "Draft",
            "Cancelled" => "Cancelled",
            _ => "Finalized"
        };
    }

    private static InvoiceDetailDto MapInvoiceDetail(Invoice invoice)
    {
        return new InvoiceDetailDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceDirection = invoice.InvoiceDirection,
            InvoiceStatus = invoice.InvoiceStatus,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DeliveryDate = invoice.DeliveryDate,
            PaymentDueDate = invoice.PaymentDueDate,
            CustomerId = invoice.CustomerId,
            SupplierName = invoice.SupplierName,
            HasSupplierInvoice = invoice.HasSupplierInvoice,
            AccountingCategory = invoice.AccountingCategory,
            Subject = invoice.Subject,
            ApplySmallBusinessRegulation = invoice.ApplySmallBusinessRegulation,
            InvoiceTotalAmount = invoice.InvoiceTotalAmount,
            ShippingCostNet = invoice.ShippingCostNet,
            ShippingCostGross = invoice.ShippingCostGross,
            OriginalPdfFileName = invoice.OriginalPdfFileName,
            HasStoredPdf = invoice.StoredPdfContent.Length > 0 || !string.IsNullOrWhiteSpace(invoice.StoredPdfPath),
            DraftSavedAt = invoice.DraftSavedAt,
            FinalizedAt = invoice.FinalizedAt,
            CancelledAt = invoice.CancelledAt,
            CancellationReason = invoice.CancellationReason,
            Lines = invoice.Lines.OrderBy(x => x.Position).Select(x => new SaveInvoiceLineRequest
            {
                Position = x.Position,
                ArticleNumber = x.ArticleNumber,
                Ean = x.Ean,
                Description = x.Description,
                Quantity = x.Quantity,
                Unit = x.Unit,
                NetUnitPrice = x.NetUnitPrice,
                MetalSurcharge = x.MetalSurcharge,
                GrossListPrice = x.GrossListPrice,
                GrossUnitPrice = x.GrossUnitPrice,
                PriceBasisQuantity = x.PriceBasisQuantity,
                ShippingNetShare = x.ShippingNetShare,
                ShippingGrossShare = x.ShippingGrossShare,
                LineTotal = x.LineTotal,
                GrossLineTotal = x.GrossLineTotal
            }).ToList()
        };
    }

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.Compiled)]
    private static partial Regex InvalidFileNameCharactersRegex();

    private static string SanitizeFileName(string value)
    {
        var sanitized = InvalidFileNameCharactersRegex().Replace(value, "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "rechnung" : sanitized;
    }
}

[Authorize]
[ApiController]
[Route("api/invoicelines")]
public class InvoiceLinesController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceLineItemDto>>> GetLines([FromQuery] bool showCompleted = true)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var lines = await db.InvoiceLines
            .Where(x => x.TenantId == tenantId && x.Invoice.InvoiceDirection == "Expense")
            .Include(x => x.Invoice)
            .Include(x => x.Allocations).ThenInclude(x => x.Customer)
            .Include(x => x.Allocations).ThenInclude(x => x.Project)
            .OrderByDescending(x => x.Invoice.InvoiceDate)
            .ThenBy(x => x.Position)
            .ToListAsync();

        var result = lines.Select(MapLine).ToList();
        if (!showCompleted)
        {
            result = result.Where(x => x.Quantity - x.Allocations.Sum(a => a.AllocatedQuantity) > 0m).ToList();
        }

        return Ok(result);
    }

    [HttpDelete("{invoiceLineId:int}")]
    public async Task<IActionResult> DeleteLine(int invoiceLineId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var line = await db.InvoiceLines.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.InvoiceLineId == invoiceLineId && x.TenantId == tenantId);
        if (line is null)
        {
            return NotFound();
        }

        db.InvoiceLines.Remove(line);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static InvoiceLineItemDto MapLine(InvoiceLine line)
    {
        return new InvoiceLineItemDto
        {
            InvoiceLineId = line.InvoiceLineId,
            InvoiceId = line.InvoiceId,
            InvoiceDirection = line.Invoice.InvoiceDirection,
            InvoiceNumber = line.Invoice.InvoiceNumber,
            InvoiceDate = line.Invoice.InvoiceDate,
            HasSupplierInvoice = line.Invoice.HasSupplierInvoice,
            AccountingCategory = line.Invoice.AccountingCategory,
            Position = line.Position,
            ArticleNumber = line.ArticleNumber,
            Ean = line.Ean,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            NetUnitPrice = line.NetUnitPrice,
            MetalSurcharge = line.MetalSurcharge,
            GrossListPrice = line.GrossListPrice,
            GrossUnitPrice = line.GrossUnitPrice,
            PriceBasisQuantity = line.PriceBasisQuantity,
            LineTotal = line.LineTotal,
            GrossLineTotal = line.GrossLineTotal,
            IsPaid = line.IsPaid,
            PaidAt = line.PaidAt,
            Allocations = line.Allocations.Select(MapAllocation).ToList()
        };
    }

    private static AllocationItemDto MapAllocation(LineAllocation allocation)
    {
        return new AllocationItemDto
        {
            LineAllocationId = allocation.LineAllocationId,
            InvoiceLineId = allocation.InvoiceLineId,
            InvoiceDirection = allocation.InvoiceLine.Invoice.InvoiceDirection,
            InvoiceNumber = allocation.InvoiceLine.Invoice.InvoiceNumber,
            InvoiceDate = allocation.InvoiceLine.Invoice.InvoiceDate,
            HasSupplierInvoice = allocation.InvoiceLine.Invoice.HasSupplierInvoice,
            AccountingCategory = allocation.InvoiceLine.Invoice.AccountingCategory,
            ArticleNumber = allocation.InvoiceLine.ArticleNumber,
            Description = allocation.InvoiceLine.Description,
            Unit = allocation.InvoiceLine.Unit,
            NetUnitPrice = allocation.InvoiceLine.NetUnitPrice,
            MetalSurcharge = allocation.InvoiceLine.MetalSurcharge,
            GrossListPrice = allocation.InvoiceLine.GrossListPrice,
            GrossUnitPrice = allocation.InvoiceLine.GrossUnitPrice,
            PriceBasisQuantity = allocation.InvoiceLine.PriceBasisQuantity,
            ShippingNetShare = allocation.InvoiceLine.ShippingNetShare,
            ShippingGrossShare = allocation.InvoiceLine.ShippingGrossShare,
            LineTotal = allocation.InvoiceLine.LineTotal,
            GrossLineTotal = allocation.InvoiceLine.GrossLineTotal,
            CustomerId = allocation.CustomerId,
            CustomerName = allocation.Customer.Name,
            ProjectId = allocation.ProjectId,
            ProjectName = allocation.Project?.Name,
            AllocatedQuantity = allocation.AllocatedQuantity,
            CustomerUnitPrice = allocation.CustomerUnitPrice,
            RevenueInvoiceId = allocation.RevenueInvoiceId,
            IsSmallMaterial = allocation.IsSmallMaterial,
            AllocatedAt = allocation.AllocatedAt,
            CustomerInvoiceNumber = allocation.CustomerInvoiceNumber,
            CustomerInvoicedAt = allocation.CustomerInvoicedAt,
            IsPaid = allocation.IsPaid,
            PaidAt = allocation.PaidAt,
            ExportedMarkupPercent = allocation.ExportedMarkupPercent,
            ExportedUnitPrice = allocation.ExportedUnitPrice,
            ExportedLineTotal = allocation.ExportedLineTotal,
            LastExportedAt = allocation.LastExportedAt
        };
    }
}

[Authorize]
[ApiController]
[Route("api/allocations")]
public class AllocationsController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AllocationItemDto>>> GetAllocations([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var query = db.LineAllocations
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Customer)
            .Include(x => x.Project)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        var result = await query.OrderByDescending(x => x.AllocatedAt)
            .Select(x => new AllocationItemDto
            {
                LineAllocationId = x.LineAllocationId,
                InvoiceLineId = x.InvoiceLineId,
                InvoiceNumber = x.InvoiceLine.Invoice.InvoiceNumber,
                InvoiceDate = x.InvoiceLine.Invoice.InvoiceDate,
                HasSupplierInvoice = x.InvoiceLine.Invoice.HasSupplierInvoice,
                AccountingCategory = x.InvoiceLine.Invoice.AccountingCategory,
                ArticleNumber = x.InvoiceLine.ArticleNumber,
                Description = x.InvoiceLine.Description,
                Unit = x.InvoiceLine.Unit,
                NetUnitPrice = x.InvoiceLine.NetUnitPrice,
                MetalSurcharge = x.InvoiceLine.MetalSurcharge,
                GrossListPrice = x.InvoiceLine.GrossListPrice,
                GrossUnitPrice = x.InvoiceLine.GrossUnitPrice,
                PriceBasisQuantity = x.InvoiceLine.PriceBasisQuantity,
                ShippingNetShare = x.InvoiceLine.ShippingNetShare,
                ShippingGrossShare = x.InvoiceLine.ShippingGrossShare,
                LineTotal = x.InvoiceLine.LineTotal,
                GrossLineTotal = x.InvoiceLine.GrossLineTotal,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.Name : null,
                AllocatedQuantity = x.AllocatedQuantity,
                CustomerUnitPrice = x.CustomerUnitPrice,
                RevenueInvoiceId = x.RevenueInvoiceId,
                IsSmallMaterial = x.IsSmallMaterial,
                AllocatedAt = x.AllocatedAt,
                CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                CustomerInvoicedAt = x.CustomerInvoicedAt,
                IsPaid = x.IsPaid,
                PaidAt = x.PaidAt,
                ExportedMarkupPercent = x.ExportedMarkupPercent,
                ExportedUnitPrice = x.ExportedUnitPrice,
                ExportedLineTotal = x.ExportedLineTotal,
                LastExportedAt = x.LastExportedAt
            }).ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AllocationItemDto>> CreateAllocation([FromBody] SaveAllocationRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var line = await db.InvoiceLines.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.InvoiceLineId == request.InvoiceLineId && x.TenantId == tenantId);
        if (line is null)
        {
            return NotFound();
        }

        await db.Entry(line).Reference(x => x.Invoice).LoadAsync(HttpContext.RequestAborted);
        if (!string.Equals(line.Invoice.InvoiceDirection, "Expense", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Nur Ausgaberechnungen koennen Projekten zugewiesen werden.");
        }

        if (!string.Equals(line.Invoice.AccountingCategory, "MaterialAndGoods", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Nur Rechnungen der Kategorie 'Material und Waren' koennen Projekten zugewiesen werden.");
        }

        var customerExists = await db.Customers.AnyAsync(x => x.CustomerId == request.CustomerId && x.TenantId == tenantId);
        if (!customerExists)
        {
            return ValidationProblem("Der ausgewaehlte Kunde wurde nicht gefunden.");
        }

        if (request.ProjectId.HasValue)
        {
            var projectExists = await db.Projects.AnyAsync(x => x.ProjectId == request.ProjectId.Value && x.CustomerId == request.CustomerId && x.TenantId == tenantId);
            if (!projectExists)
            {
                return ValidationProblem("Das ausgewaehlte Projekt gehoert nicht zum Kunden.");
            }
        }

        var remaining = line.Quantity - line.Allocations.Sum(x => x.AllocatedQuantity);
        if (request.AllocatedQuantity > remaining)
        {
            return ValidationProblem($"Remaining quantity is {remaining:0.##}.");
        }

        var allocation = new LineAllocation
        {
            TenantId = tenantId,
            InvoiceLineId = request.InvoiceLineId,
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            AllocatedQuantity = request.AllocatedQuantity,
            CustomerUnitPrice = request.CustomerUnitPrice,
            IsSmallMaterial = request.IsSmallMaterial
        };

        db.LineAllocations.Add(allocation);
        await db.SaveChangesAsync();
        return Ok(await db.LineAllocations
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Customer)
            .Include(x => x.Project)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .Where(x => x.LineAllocationId == allocation.LineAllocationId)
            .Select(x => new AllocationItemDto
            {
                LineAllocationId = x.LineAllocationId,
                InvoiceLineId = x.InvoiceLineId,
                InvoiceDirection = x.InvoiceLine.Invoice.InvoiceDirection,
                InvoiceNumber = x.InvoiceLine.Invoice.InvoiceNumber,
                InvoiceDate = x.InvoiceLine.Invoice.InvoiceDate,
                HasSupplierInvoice = x.InvoiceLine.Invoice.HasSupplierInvoice,
                AccountingCategory = x.InvoiceLine.Invoice.AccountingCategory,
                ArticleNumber = x.InvoiceLine.ArticleNumber,
                Description = x.InvoiceLine.Description,
                Unit = x.InvoiceLine.Unit,
                NetUnitPrice = x.InvoiceLine.NetUnitPrice,
                MetalSurcharge = x.InvoiceLine.MetalSurcharge,
                PriceBasisQuantity = x.InvoiceLine.PriceBasisQuantity,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.Name : null,
                AllocatedQuantity = x.AllocatedQuantity,
                CustomerUnitPrice = x.CustomerUnitPrice,
                RevenueInvoiceId = x.RevenueInvoiceId,
                IsSmallMaterial = x.IsSmallMaterial,
                AllocatedAt = x.AllocatedAt,
                CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                CustomerInvoicedAt = x.CustomerInvoicedAt,
                IsPaid = x.IsPaid,
                PaidAt = x.PaidAt,
                ExportedMarkupPercent = x.ExportedMarkupPercent,
                ExportedUnitPrice = x.ExportedUnitPrice,
                ExportedLineTotal = x.ExportedLineTotal,
                LastExportedAt = x.LastExportedAt
            }).FirstAsync());
    }

    [HttpPut("{allocationId:int}/quantity")]
    public async Task<IActionResult> UpdateQuantity(int allocationId, [FromBody] UpdateAllocationQuantityRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocation = await db.LineAllocations.Include(x => x.InvoiceLine).ThenInclude(x => x.Allocations).FirstOrDefaultAsync(x => x.LineAllocationId == allocationId && x.TenantId == tenantId);
        if (allocation is null)
        {
            return NotFound();
        }

        var otherAllocated = allocation.InvoiceLine.Allocations.Where(x => x.LineAllocationId != allocationId).Sum(x => x.AllocatedQuantity);
        var maxAllowed = allocation.InvoiceLine.Quantity - otherAllocated;
        if (request.AllocatedQuantity > maxAllowed)
        {
            return ValidationProblem($"Maximum allowed quantity is {maxAllowed:0.##}.");
        }

        allocation.AllocatedQuantity = request.AllocatedQuantity;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{allocationId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int allocationId, [FromBody] UpdateAllocationStatusRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId && x.TenantId == tenantId);
        if (allocation is null)
        {
            return NotFound();
        }

        var invoiceNumber = string.IsNullOrWhiteSpace(request.CustomerInvoiceNumber) ? null : request.CustomerInvoiceNumber.Trim();
        if (request.MarkInvoiced)
        {
            allocation.CustomerInvoiceNumber = invoiceNumber ?? allocation.CustomerInvoiceNumber;
            allocation.CustomerInvoicedAt ??= DateTime.UtcNow;
        }

        if (request.MarkPaid)
        {
            allocation.CustomerInvoiceNumber = invoiceNumber ?? allocation.CustomerInvoiceNumber;
            allocation.CustomerInvoicedAt ??= DateTime.UtcNow;
            allocation.IsPaid = true;
            allocation.PaidAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{allocationId:int}/export")]
    public async Task<IActionResult> UpdateExport(int allocationId, [FromBody] UpdateAllocationExportRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId && x.TenantId == tenantId);
        if (allocation is null)
        {
            return NotFound();
        }

        allocation.ExportedMarkupPercent = request.ExportedMarkupPercent;
        allocation.ExportedUnitPrice = request.ExportedUnitPrice;
        allocation.ExportedLineTotal = request.ExportedLineTotal;
        allocation.LastExportedAt = request.LastExportedAt ?? DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{allocationId:int}/revenue-link")]
    public async Task<IActionResult> UpdateRevenueLink(int allocationId, [FromBody] UpdateRevenueLinkRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (allocation is null)
        {
            return NotFound();
        }

        allocation.RevenueInvoiceId = request.RevenueInvoiceId;
        if (request.MarkInvoiced)
        {
            allocation.CustomerInvoiceNumber = string.IsNullOrWhiteSpace(request.RevenueInvoiceNumber) ? allocation.CustomerInvoiceNumber : request.RevenueInvoiceNumber.Trim();
            allocation.CustomerInvoicedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("{allocationId:int}")]
    public async Task<IActionResult> DeleteAllocation(int allocationId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId && x.TenantId == tenantId);
        if (allocation is null)
        {
            return NotFound();
        }

        db.LineAllocations.Remove(allocation);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[Authorize]
[ApiController]
[Route("api/worktimeentry-exports")]
public class WorkTimeEntryExportsController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpPut("{entryId:int}")]
    public async Task<IActionResult> UpdateExport(int entryId, [FromBody] UpdateWorkTimeExportRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId && x.TenantId == tenantId);
        if (entry is null)
        {
            return NotFound();
        }

        entry.ExportedUnitPrice = request.ExportedUnitPrice;
        entry.ExportedLineTotal = request.ExportedLineTotal;
        entry.LastExportedAt = request.LastExportedAt ?? DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[Authorize]
[ApiController]
[Route("api/analytics")]
public class AnalyticsController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    private const decimal GermanVatRate = 0.19m;

    [HttpGet("details")]
    public async Task<ActionResult<AnalyticsResponseDto>> GetDetails([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocations = await db.LineAllocations
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Project)
            .Include(x => x.Customer)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .Include(x => x.RevenueInvoice)
            .ToListAsync();

        var workEntries = await db.WorkTimeEntries
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Project)
            .Include(x => x.Customer)
            .Include(x => x.RevenueInvoice)
            .ToListAsync();

        if (customerId.HasValue)
        {
            allocations = allocations.Where(x => x.CustomerId == customerId.Value).ToList();
            workEntries = workEntries.Where(x => x.CustomerId == customerId.Value).ToList();
        }

        if (projectId.HasValue)
        {
            allocations = allocations.Where(x => x.ProjectId == projectId.Value).ToList();
            workEntries = workEntries.Where(x => x.ProjectId == projectId.Value).ToList();
        }

        var paidAllocationRevenue = allocations.Where(x => x.IsPaid).Sum(GetAllocationRevenueGross);
        var paidWorkRevenue = workEntries.Where(x => x.IsPaid).Sum(GetWorkRevenueGross);
        var openRevenue = allocations.Where(x => !x.IsPaid).Sum(GetAllocationRevenueGross)
            + workEntries.Where(x => !x.IsPaid).Sum(GetWorkRevenueGross);

        decimal expenses;
        List<ExpenseCategoryTotalDto> expenseCategories;
        if (!customerId.HasValue && !projectId.HasValue)
        {
            var expenseInvoices = await db.Invoices
                .Where(x => x.TenantId == tenantId && x.InvoiceDirection == "Expense")
                .Include(x => x.Lines)
                .ToListAsync();
            expenses = expenseInvoices.Sum(GetExpenseInvoiceGrossTotal);
            expenseCategories = expenseInvoices
                .GroupBy(x => x.AccountingCategory)
                .Select(g => new ExpenseCategoryTotalDto
                {
                    AccountingCategory = g.Key,
                    Amount = g.Sum(GetExpenseInvoiceGrossTotal)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }
        else
        {
            expenses = allocations.Sum(GetAllocatedExpenseGross);
            expenseCategories = allocations
                .GroupBy(x => x.InvoiceLine.Invoice.AccountingCategory)
                .Select(g => new ExpenseCategoryTotalDto
                {
                    AccountingCategory = g.Key,
                    Amount = g.Sum(GetAllocatedExpenseGross)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }

        var revenue = paidAllocationRevenue + paidWorkRevenue;
        var months = Enumerable.Range(0, 6).Select(offset => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5 + offset)).ToList();
        var revenueByMonth = allocations.Where(x => x.IsPaid && x.PaidAt != null)
            .GroupBy(x => new DateTime(x.PaidAt!.Value.Year, x.PaidAt.Value.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(GetAllocationRevenueGross));

        foreach (var group in workEntries.Where(x => x.IsPaid && x.PaidAt != null)
                     .GroupBy(x => new DateTime(x.PaidAt!.Value.Year, x.PaidAt.Value.Month, 1)))
        {
            revenueByMonth[group.Key] = revenueByMonth.TryGetValue(group.Key, out var existing)
                ? existing + group.Sum(GetWorkRevenueGross)
                : group.Sum(GetWorkRevenueGross);
        }

        Dictionary<DateTime, decimal> expensesByMonth;
        if (!customerId.HasValue && !projectId.HasValue)
        {
            var expenseInvoicesForMonths = await db.Invoices
                .Where(x => x.TenantId == tenantId && x.InvoiceDirection == "Expense")
                .Include(x => x.Lines)
                .AsNoTracking()
                .ToListAsync();
            expensesByMonth = expenseInvoicesForMonths
                .GroupBy(x => new DateTime(x.InvoiceDate.Year, x.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(GetExpenseInvoiceGrossTotal));
        }
        else
        {
            expensesByMonth = allocations
                .GroupBy(x => new DateTime(x.InvoiceLine.Invoice.InvoiceDate.Year, x.InvoiceLine.Invoice.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(GetAllocatedExpenseGross));
        }

        var maxValue = months.Select(m => Math.Max(revenueByMonth.GetValueOrDefault(m), expensesByMonth.GetValueOrDefault(m))).DefaultIfEmpty(1m).Max();
        if (maxValue <= 0m)
        {
            maxValue = 1m;
        }

        var projects = await db.Projects.Where(x => x.TenantId == tenantId)
            .Include(x => x.Customer)
            .Include(x => x.Allocations).ThenInclude(x => x.InvoiceLine)
            .Include(x => x.Allocations).ThenInclude(x => x.RevenueInvoice)
            .Include(x => x.WorkTimeEntries).ThenInclude(x => x.RevenueInvoice)
            .ToListAsync();
        if (customerId.HasValue)
        {
            projects = projects.Where(x => x.CustomerId == customerId.Value).ToList();
        }

        if (projectId.HasValue)
        {
            projects = projects.Where(x => x.ProjectId == projectId.Value).ToList();
        }

        return Ok(new AnalyticsResponseDto
        {
            Revenue = revenue,
            Expenses = expenses,
            Profit = revenue - expenses,
            OpenRevenue = openRevenue,
            Monthly = months.Select(month => new AnalyticsMonthDto
            {
                Label = month.ToString("MM.yyyy"),
                Revenue = revenueByMonth.GetValueOrDefault(month),
                Expenses = expensesByMonth.GetValueOrDefault(month),
                RevenueHeight = (double)(revenueByMonth.GetValueOrDefault(month) / maxValue * 180m),
                ExpenseHeight = (double)(expensesByMonth.GetValueOrDefault(month) / maxValue * 180m)
            }).ToList(),
            Projects = projects.Select(project => new ProjectAnalyticsRowDto
            {
                CustomerName = project.Customer.Name,
                ProjectName = project.Name,
                PaidRevenue = project.Allocations.Where(a => a.IsPaid).Sum(GetAllocationRevenueGross)
                    + project.WorkTimeEntries.Where(w => w.IsPaid).Sum(GetWorkRevenueGross),
                OpenRevenue = project.Allocations.Where(a => !a.IsPaid).Sum(GetAllocationRevenueGross)
                    + project.WorkTimeEntries.Where(w => !w.IsPaid).Sum(GetWorkRevenueGross),
                LoggedHours = project.WorkTimeEntries.Sum(w => w.HoursWorked),
                OpenItemCount = project.Allocations.Count(a => !a.IsPaid) + project.WorkTimeEntries.Count(w => !w.IsPaid)
            }).OrderBy(x => x.CustomerName).ThenBy(x => x.ProjectName).ToList(),
            ExpenseCategories = expenseCategories
        });
    }

    private static decimal GetAllocationRevenueGross(LineAllocation allocation)
    {
        var netAmount = allocation.ExportedLineTotal > 0m
            ? allocation.ExportedLineTotal
            : allocation.AllocatedQuantity * allocation.CustomerUnitPrice;
        return AddRevenueVatIfRequired(netAmount, allocation.RevenueInvoice?.ApplySmallBusinessRegulation ?? false);
    }

    private static decimal GetPurchaseUnitPrice(InvoiceLine line)
    {
        if (line.Quantity > 0m && line.LineTotal > 0m)
        {
            return decimal.Round(line.LineTotal / line.Quantity, 4);
        }

        var divisor = line.PriceBasisQuantity <= 0m ? 1m : line.PriceBasisQuantity;
        return (line.NetUnitPrice + line.MetalSurcharge) / divisor;
    }

    private static decimal GetAllocatedExpenseGross(LineAllocation allocation)
    {
        var purchaseUnitPrice = allocation.CustomerUnitPrice > 0m
            ? allocation.CustomerUnitPrice
            : GetPurchaseUnitPrice(allocation.InvoiceLine);
        var netAmount = allocation.AllocatedQuantity * purchaseUnitPrice;
        return netAmount * GetExpenseGrossFactor(allocation.InvoiceLine.Invoice);
    }

    private static decimal GetWorkRevenueGross(WorkTimeEntry workEntry)
    {
        var netAmount = workEntry.ExportedLineTotal > 0m
            ? workEntry.ExportedLineTotal
            : (workEntry.HoursWorked * workEntry.HourlyRate) + (workEntry.TravelKilometers * workEntry.TravelRatePerKilometer);
        return AddRevenueVatIfRequired(netAmount, workEntry.RevenueInvoice?.ApplySmallBusinessRegulation ?? false);
    }

    private static decimal GetExpenseInvoiceGrossTotal(Invoice invoice)
    {
        if (invoice.InvoiceTotalAmount > 0m)
        {
            return invoice.InvoiceTotalAmount;
        }

        var grossLinesTotal = invoice.Lines.Sum(x => x.GrossLineTotal);
        if (grossLinesTotal > 0m)
        {
            return grossLinesTotal;
        }

        var netTotal = GetInvoiceNetTotal(invoice);
        return AddVat(netTotal);
    }

    private static decimal GetInvoiceNetTotal(Invoice invoice)
    {
        var linesTotal = invoice.Lines.Sum(x => x.LineTotal);
        return linesTotal > 0m ? linesTotal : invoice.InvoiceTotalAmount;
    }

    private static decimal GetExpenseGrossFactor(Invoice invoice)
    {
        var netTotal = GetInvoiceNetTotal(invoice);
        if (invoice.InvoiceTotalAmount > 0m && netTotal > 0m)
        {
            return invoice.InvoiceTotalAmount / netTotal;
        }

        var grossLinesTotal = invoice.Lines.Sum(x => x.GrossLineTotal);
        if (grossLinesTotal > 0m && netTotal > 0m)
        {
            return grossLinesTotal / netTotal;
        }

        return 1m + GermanVatRate;
    }

    private static decimal AddRevenueVatIfRequired(decimal amount, bool applySmallBusinessRegulation)
    {
        return applySmallBusinessRegulation ? amount : AddVat(amount);
    }

    private static decimal AddVat(decimal amount)
    {
        return amount * (1m + GermanVatRate);
    }
}
