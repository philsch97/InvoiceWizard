using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SaveInvoice([FromBody] SaveInvoiceRequest request)
    {
        var exists = await db.Invoices.AnyAsync(x => x.ContentHash == request.ContentHash);
        if (exists)
        {
            return Conflict(new { message = "Invoice already exists." });
        }

        var invoice = new Invoice
        {
            InvoiceNumber = request.InvoiceNumber.Trim(),
            InvoiceDate = request.InvoiceDate,
            SupplierName = request.SupplierName.Trim(),
            SourcePdfPath = request.SourcePdfPath.Trim(),
            ContentHash = request.ContentHash.Trim(),
            Lines = request.Lines.Select(line => new InvoiceLine
            {
                Position = line.Position,
                ArticleNumber = line.ArticleNumber,
                Ean = line.Ean,
                Description = line.Description,
                Quantity = line.Quantity,
                Unit = line.Unit,
                NetUnitPrice = line.NetUnitPrice,
                GrossListPrice = line.GrossListPrice,
                PriceBasisQuantity = line.PriceBasisQuantity,
                LineTotal = line.LineTotal
            }).ToList()
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return Ok(new { invoice.InvoiceId, lineCount = invoice.Lines.Count });
    }
}

[ApiController]
[Route("api/invoicelines")]
public class InvoiceLinesController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceLineItemDto>>> GetLines([FromQuery] bool showCompleted = true)
    {
        var lines = await db.InvoiceLines
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
        var line = await db.InvoiceLines.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.InvoiceLineId == invoiceLineId);
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
            InvoiceNumber = line.Invoice.InvoiceNumber,
            InvoiceDate = line.Invoice.InvoiceDate,
            Position = line.Position,
            ArticleNumber = line.ArticleNumber,
            Ean = line.Ean,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            NetUnitPrice = line.NetUnitPrice,
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
            InvoiceNumber = allocation.InvoiceLine.Invoice.InvoiceNumber,
            InvoiceDate = allocation.InvoiceLine.Invoice.InvoiceDate,
            ArticleNumber = allocation.InvoiceLine.ArticleNumber,
            Description = allocation.InvoiceLine.Description,
            Unit = allocation.InvoiceLine.Unit,
            NetUnitPrice = allocation.InvoiceLine.NetUnitPrice,
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

[ApiController]
[Route("api/allocations")]
public class AllocationsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AllocationItemDto>>> GetAllocations([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var query = db.LineAllocations
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
                ArticleNumber = x.InvoiceLine.ArticleNumber,
                Description = x.InvoiceLine.Description,
                Unit = x.InvoiceLine.Unit,
                NetUnitPrice = x.InvoiceLine.NetUnitPrice,
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
        var line = await db.InvoiceLines.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.InvoiceLineId == request.InvoiceLineId);
        if (line is null)
        {
            return NotFound();
        }

        var remaining = line.Quantity - line.Allocations.Sum(x => x.AllocatedQuantity);
        if (request.AllocatedQuantity > remaining)
        {
            return ValidationProblem($"Remaining quantity is {remaining:0.##}.");
        }

        var allocation = new LineAllocation
        {
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
            .Include(x => x.Customer)
            .Include(x => x.Project)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .Where(x => x.LineAllocationId == allocation.LineAllocationId)
            .Select(x => new AllocationItemDto
            {
                LineAllocationId = x.LineAllocationId,
                InvoiceLineId = x.InvoiceLineId,
                InvoiceNumber = x.InvoiceLine.Invoice.InvoiceNumber,
                InvoiceDate = x.InvoiceLine.Invoice.InvoiceDate,
                ArticleNumber = x.InvoiceLine.ArticleNumber,
                Description = x.InvoiceLine.Description,
                Unit = x.InvoiceLine.Unit,
                NetUnitPrice = x.InvoiceLine.NetUnitPrice,
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
        var allocation = await db.LineAllocations.Include(x => x.InvoiceLine).ThenInclude(x => x.Allocations).FirstOrDefaultAsync(x => x.LineAllocationId == allocationId);
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
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId);
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
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId);
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
        var allocation = await db.LineAllocations.FirstOrDefaultAsync(x => x.LineAllocationId == allocationId);
        if (allocation is null)
        {
            return NotFound();
        }

        db.LineAllocations.Remove(allocation);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/worktimeentry-exports")]
public class WorkTimeEntryExportsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpPut("{entryId:int}")]
    public async Task<IActionResult> UpdateExport(int entryId, [FromBody] UpdateWorkTimeExportRequest request)
    {
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId);
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

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet("details")]
    public async Task<ActionResult<AnalyticsResponseDto>> GetDetails([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var allocations = await db.LineAllocations
            .Include(x => x.Project)
            .Include(x => x.Customer)
            .Include(x => x.InvoiceLine).ThenInclude(x => x.Invoice)
            .ToListAsync();

        var workEntries = await db.WorkTimeEntries
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
        if (!customerId.HasValue && !projectId.HasValue)
        {
            expenses = await db.InvoiceLines.Include(x => x.Invoice).Select(x => x.LineTotal).SumAsync();
        }
        else
        {
            expenses = allocations.Sum(x => x.AllocatedQuantity * (x.InvoiceLine.NetUnitPrice / (x.InvoiceLine.PriceBasisQuantity <= 0m ? 1m : x.InvoiceLine.PriceBasisQuantity)));
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
            expensesByMonth = db.InvoiceLines.Include(x => x.Invoice)
                .AsEnumerable()
                .GroupBy(x => new DateTime(x.Invoice.InvoiceDate.Year, x.Invoice.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));
        }
        else
        {
            expensesByMonth = allocations
                .GroupBy(x => new DateTime(x.InvoiceLine.Invoice.InvoiceDate.Year, x.InvoiceLine.Invoice.InvoiceDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedQuantity * (x.InvoiceLine.NetUnitPrice / (x.InvoiceLine.PriceBasisQuantity <= 0m ? 1m : x.InvoiceLine.PriceBasisQuantity))));
        }

        var maxValue = months.Select(m => Math.Max(revenueByMonth.GetValueOrDefault(m), expensesByMonth.GetValueOrDefault(m))).DefaultIfEmpty(1m).Max();
        if (maxValue <= 0m)
        {
            maxValue = 1m;
        }

        var projects = await db.Projects.Include(x => x.Customer).Include(x => x.Allocations).ThenInclude(x => x.InvoiceLine).Include(x => x.WorkTimeEntries).ToListAsync();
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
            }).OrderBy(x => x.CustomerName).ThenBy(x => x.ProjectName).ToList()
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
}

