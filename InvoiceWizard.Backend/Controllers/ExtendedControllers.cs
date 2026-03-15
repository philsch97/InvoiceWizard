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
    [HttpPost]
    public async Task<IActionResult> SaveInvoice([FromBody] SaveInvoiceRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var exists = await db.Invoices.AnyAsync(x => x.TenantId == tenantId && x.ContentHash == request.ContentHash);
        if (exists)
        {
            return Conflict(new { message = "Invoice already exists." });
        }

        var fallbackNumber = $"MANUELL-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceDirection = NormalizeInvoiceDirection(request.InvoiceDirection),
            HasSupplierInvoice = request.HasSupplierInvoice,
            InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? fallbackNumber : request.InvoiceNumber.Trim(),
            InvoiceDate = request.InvoiceDate,
            SupplierName = request.SupplierName.Trim(),
            AccountingCategory = NormalizeAccountingCategory(request.AccountingCategory),
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
                PriceBasisQuantity = line.PriceBasisQuantity,
                LineTotal = line.LineTotal
            }).ToList()
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(request.PdfContentBase64))
        {
            invoice.StoredPdfPath = await SavePdfAsync(tenantId, invoice.InvoiceId, invoice.OriginalPdfFileName, request.PdfContentBase64, HttpContext.RequestAborted);
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
                InvoiceNumber = x.InvoiceNumber,
                InvoiceDate = x.InvoiceDate,
                SupplierName = x.SupplierName,
                HasSupplierInvoice = x.HasSupplierInvoice,
                AccountingCategory = x.AccountingCategory,
                OriginalPdfFileName = x.OriginalPdfFileName,
                HasStoredPdf = !string.IsNullOrWhiteSpace(x.StoredPdfPath)
            })
            .ToListAsync();

        return Ok(items);
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

        if (string.IsNullOrWhiteSpace(invoice.StoredPdfPath) || !System.IO.File.Exists(invoice.StoredPdfPath))
        {
            return NotFound(new { message = "Fuer diese Rechnung ist keine gespeicherte PDF vorhanden." });
        }

        var fileName = string.IsNullOrWhiteSpace(invoice.OriginalPdfFileName)
            ? $"{SanitizeFileName(invoice.InvoiceNumber)}.pdf"
            : invoice.OriginalPdfFileName;
        var bytes = await System.IO.File.ReadAllBytesAsync(invoice.StoredPdfPath, HttpContext.RequestAborted);
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

        if (!string.IsNullOrWhiteSpace(invoice.StoredPdfPath) && System.IO.File.Exists(invoice.StoredPdfPath))
        {
            System.IO.File.Delete(invoice.StoredPdfPath);
        }

        invoice.OriginalPdfFileName = request.OriginalPdfFileName.Trim();
        invoice.StoredPdfPath = await SavePdfAsync(tenantId, invoice.InvoiceId, invoice.OriginalPdfFileName, request.PdfContentBase64, HttpContext.RequestAborted);
        await db.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { invoice.InvoiceId, invoice.OriginalPdfFileName });
    }

    private async Task<string> SavePdfAsync(int tenantId, int invoiceId, string originalPdfFileName, string pdfContentBase64, CancellationToken cancellationToken)
    {
        var storageRoot = Path.Combine(environment.ContentRootPath, "storage", "invoices", tenantId.ToString());
        Directory.CreateDirectory(storageRoot);
        var safeName = string.IsNullOrWhiteSpace(originalPdfFileName)
            ? $"invoice_{invoiceId}.pdf"
            : $"{Path.GetFileNameWithoutExtension(SanitizeFileName(originalPdfFileName))}.pdf";
        var finalPath = Path.Combine(storageRoot, $"{invoiceId}_{safeName}");
        var bytes = Convert.FromBase64String(pdfContentBase64);
        await System.IO.File.WriteAllBytesAsync(finalPath, bytes, cancellationToken);
        return finalPath;
    }

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
            PriceBasisQuantity = line.PriceBasisQuantity,
            LineTotal = line.LineTotal,
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
            PriceBasisQuantity = allocation.InvoiceLine.PriceBasisQuantity,
            CustomerId = allocation.CustomerId,
            CustomerName = allocation.Customer.Name,
            ProjectId = allocation.ProjectId,
            ProjectName = allocation.Project?.Name,
            AllocatedQuantity = allocation.AllocatedQuantity,
            CustomerUnitPrice = allocation.CustomerUnitPrice,
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
                PriceBasisQuantity = x.InvoiceLine.PriceBasisQuantity,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.Name : null,
                AllocatedQuantity = x.AllocatedQuantity,
                CustomerUnitPrice = x.CustomerUnitPrice,
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
    [HttpGet("details")]
    public async Task<ActionResult<AnalyticsResponseDto>> GetDetails([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var allocations = await db.LineAllocations
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Project)
            .Include(x => x.Customer)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .ToListAsync();

        var workEntries = await db.WorkTimeEntries
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Project)
            .Include(x => x.Customer)
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

        var paidAllocationRevenue = allocations.Where(x => x.IsPaid).Sum(GetAllocationRevenue);
        var paidWorkRevenue = workEntries.Where(x => x.IsPaid).Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer));
        var openRevenue = allocations.Where(x => !x.IsPaid).Sum(GetAllocationRevenue)
            + workEntries.Where(x => !x.IsPaid).Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer));

        decimal expenses;
        List<ExpenseCategoryTotalDto> expenseCategories;
        if (!customerId.HasValue && !projectId.HasValue)
        {
            expenses = await db.InvoiceLines
                .Where(x => x.TenantId == tenantId && x.Invoice.HasSupplierInvoice)
                .Select(x => x.LineTotal)
                .SumAsync();
            expenseCategories = await db.InvoiceLines
                .Where(x => x.TenantId == tenantId && x.Invoice.HasSupplierInvoice)
                .GroupBy(x => x.Invoice.AccountingCategory)
                .Select(g => new ExpenseCategoryTotalDto
                {
                    AccountingCategory = g.Key,
                    Amount = g.Sum(x => x.LineTotal)
                })
                .OrderByDescending(x => x.Amount)
                .ToListAsync();
        }
        else
        {
            expenses = allocations.Sum(x => x.AllocatedQuantity * GetPurchaseUnitPrice(x.InvoiceLine));
            expenseCategories = allocations
                .GroupBy(x => x.InvoiceLine.Invoice.AccountingCategory)
                .Select(g => new ExpenseCategoryTotalDto
                {
                    AccountingCategory = g.Key,
                    Amount = g.Sum(x => x.AllocatedQuantity * GetPurchaseUnitPrice(x.InvoiceLine))
                })
                .OrderByDescending(x => x.Amount)
                .ToList();
        }

        var revenue = paidAllocationRevenue + paidWorkRevenue;
        var months = Enumerable.Range(0, 6).Select(offset => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5 + offset)).ToList();
        var revenueByMonth = allocations.Where(x => x.IsPaid && x.PaidAt != null)
            .GroupBy(x => new DateTime(x.PaidAt!.Value.Year, x.PaidAt.Value.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(GetAllocationRevenue));

        foreach (var group in workEntries.Where(x => x.IsPaid && x.PaidAt != null)
                     .GroupBy(x => new DateTime(x.PaidAt!.Value.Year, x.PaidAt.Value.Month, 1)))
        {
            revenueByMonth[group.Key] = revenueByMonth.TryGetValue(group.Key, out var existing)
                ? existing + group.Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer))
                : group.Sum(x => x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer));
        }

        Dictionary<DateTime, decimal> expensesByMonth;
        if (!customerId.HasValue && !projectId.HasValue)
        {
            expensesByMonth = db.InvoiceLines.Where(x => x.TenantId == tenantId && x.Invoice.HasSupplierInvoice)
                .Include(x => x.Invoice)
                .AsEnumerable()
                .GroupBy(x => new DateTime(x.Invoice.InvoiceDate.Year, x.Invoice.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));
        }
        else
        {
            expensesByMonth = allocations
                .GroupBy(x => new DateTime(x.InvoiceLine.Invoice.InvoiceDate.Year, x.InvoiceLine.Invoice.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedQuantity * GetPurchaseUnitPrice(x.InvoiceLine)));
        }

        var maxValue = months.Select(m => Math.Max(revenueByMonth.GetValueOrDefault(m), expensesByMonth.GetValueOrDefault(m))).DefaultIfEmpty(1m).Max();
        if (maxValue <= 0m)
        {
            maxValue = 1m;
        }

        var projects = await db.Projects.Where(x => x.TenantId == tenantId).Include(x => x.Customer).Include(x => x.Allocations).ThenInclude(x => x.InvoiceLine).Include(x => x.WorkTimeEntries).ToListAsync();
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
                PaidRevenue = project.Allocations.Where(a => a.IsPaid).Sum(GetAllocationRevenue)
                    + project.WorkTimeEntries.Where(w => w.IsPaid).Sum(w => w.ExportedLineTotal > 0m ? w.ExportedLineTotal : (w.HoursWorked * w.HourlyRate) + (w.TravelKilometers * w.TravelRatePerKilometer)),
                OpenRevenue = project.Allocations.Where(a => !a.IsPaid).Sum(GetAllocationRevenue)
                    + project.WorkTimeEntries.Where(w => !w.IsPaid).Sum(w => w.ExportedLineTotal > 0m ? w.ExportedLineTotal : (w.HoursWorked * w.HourlyRate) + (w.TravelKilometers * w.TravelRatePerKilometer)),
                LoggedHours = project.WorkTimeEntries.Sum(w => w.HoursWorked),
                OpenItemCount = project.Allocations.Count(a => !a.IsPaid) + project.WorkTimeEntries.Count(w => !w.IsPaid)
            }).OrderBy(x => x.CustomerName).ThenBy(x => x.ProjectName).ToList(),
            ExpenseCategories = expenseCategories
        });
    }

    private static decimal GetAllocationRevenue(LineAllocation allocation)
    {
        if (allocation.ExportedLineTotal > 0m)
        {
            return allocation.ExportedLineTotal;
        }

        return allocation.AllocatedQuantity * allocation.CustomerUnitPrice;
    }

    private static decimal GetPurchaseUnitPrice(InvoiceLine line)
    {
        var divisor = line.PriceBasisQuantity <= 0m ? 1m : line.PriceBasisQuantity;
        return (line.NetUnitPrice + line.MetalSurcharge) / divisor;
    }
}
