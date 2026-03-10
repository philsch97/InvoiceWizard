using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> GetCustomers()
    {
        var customers = await db.Customers.OrderBy(x => x.Name)
            .Select(x => new CustomerListItemDto
            {
                CustomerId = x.CustomerId,
                Name = x.Name,
                DefaultMarkupPercent = x.DefaultMarkupPercent,
                ProjectCount = x.Projects.Count,
                OpenWorkItems = x.WorkTimeEntries.Count(w => !w.IsPaid)
            }).ToListAsync();
        return Ok(customers);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerListItemDto>> SaveCustomer([FromBody] SaveCustomerRequest request)
    {
        var name = request.Name.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Name == name);
        if (customer is null)
        {
            customer = new Customer { Name = name, DefaultMarkupPercent = request.DefaultMarkupPercent };
            db.Customers.Add(customer);
        }
        else
        {
            customer.DefaultMarkupPercent = request.DefaultMarkupPercent;
        }

        await db.SaveChangesAsync();
        return Ok(new CustomerListItemDto { CustomerId = customer.CustomerId, Name = customer.Name, DefaultMarkupPercent = customer.DefaultMarkupPercent });
    }

    [HttpPut("{customerId:int}")]
    public async Task<ActionResult<CustomerListItemDto>> UpdateCustomer(int customerId, [FromBody] SaveCustomerRequest request)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null)
        {
            return NotFound();
        }

        customer.Name = request.Name.Trim();
        customer.DefaultMarkupPercent = request.DefaultMarkupPercent;
        await db.SaveChangesAsync();
        return Ok(new CustomerListItemDto { CustomerId = customer.CustomerId, Name = customer.Name, DefaultMarkupPercent = customer.DefaultMarkupPercent });
    }

    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> DeleteCustomer(int customerId)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null)
        {
            return NotFound();
        }

        var todoListIds = await db.TodoLists.Where(x => x.CustomerId == customerId).Select(x => x.TodoListId).ToListAsync();
        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => todoListIds.Contains(x.TodoListId)).ToListAsync());
        db.TodoAttachments.RemoveRange(await db.TodoAttachments.Where(x => todoListIds.Contains(x.TodoListId)).ToListAsync());
        db.TodoLists.RemoveRange(await db.TodoLists.Where(x => x.CustomerId == customerId).ToListAsync());
        db.LineAllocations.RemoveRange(await db.LineAllocations.Where(x => x.CustomerId == customerId).ToListAsync());
        db.WorkTimeEntries.RemoveRange(await db.WorkTimeEntries.Where(x => x.CustomerId == customerId).ToListAsync());
        db.Projects.RemoveRange(await db.Projects.Where(x => x.CustomerId == customerId).ToListAsync());
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{customerId:int}/projects")]
    public async Task<ActionResult<IReadOnlyList<ProjectListItemDto>>> GetProjectsForCustomer(int customerId)
    {
        var projects = await db.Projects.Where(x => x.CustomerId == customerId).OrderBy(x => x.Name)
            .Select(x => new ProjectListItemDto
            {
                ProjectId = x.ProjectId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                Name = x.Name,
                OpenWorkItems = x.WorkTimeEntries.Count(w => !w.IsPaid),
                LoggedHours = x.WorkTimeEntries.Sum(w => (decimal?)w.HoursWorked) ?? 0m
            }).ToListAsync();

        return Ok(projects);
    }

    [HttpPost("{customerId:int}/projects")]
    public async Task<ActionResult<ProjectListItemDto>> SaveProject(int customerId, [FromBody] SaveProjectRequest request)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null)
        {
            return NotFound();
        }

        var projectName = request.Name.Trim();
        var project = await db.Projects.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Name == projectName);
        if (project is null)
        {
            project = new Project { CustomerId = customerId, Name = projectName };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }

        return Ok(new ProjectListItemDto { ProjectId = project.ProjectId, CustomerId = customerId, CustomerName = customer.Name, Name = project.Name });
    }
}

[ApiController]
[Route("api/projects")]
public class ProjectsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectListItemDto>>> GetProjects([FromQuery] int? customerId)
    {
        var query = db.Projects.AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        var projects = await query.OrderBy(x => x.Customer.Name).ThenBy(x => x.Name)
            .Select(x => new ProjectListItemDto
            {
                ProjectId = x.ProjectId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                Name = x.Name,
                OpenWorkItems = x.WorkTimeEntries.Count(w => !w.IsPaid),
                LoggedHours = x.WorkTimeEntries.Sum(w => (decimal?)w.HoursWorked) ?? 0m
            }).ToListAsync();

        return Ok(projects);
    }

    [HttpDelete("{projectId:int}")]
    public async Task<IActionResult> DeleteProject(int projectId)
    {
        var project = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId);
        if (project is null)
        {
            return NotFound();
        }

        var todoListIds = await db.TodoLists.Where(x => x.ProjectId == projectId).Select(x => x.TodoListId).ToListAsync();
        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => todoListIds.Contains(x.TodoListId)).ToListAsync());
        db.TodoAttachments.RemoveRange(await db.TodoAttachments.Where(x => todoListIds.Contains(x.TodoListId)).ToListAsync());
        db.TodoLists.RemoveRange(await db.TodoLists.Where(x => x.ProjectId == projectId).ToListAsync());
        db.LineAllocations.RemoveRange(await db.LineAllocations.Where(x => x.ProjectId == projectId).ToListAsync());
        db.WorkTimeEntries.RemoveRange(await db.WorkTimeEntries.Where(x => x.ProjectId == projectId).ToListAsync());
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/worktimeentries")]
public class WorkTimeEntriesController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTimeEntryListItemDto>>> GetEntries([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var query = db.WorkTimeEntries.Include(x => x.Customer).Include(x => x.Project).AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        var entries = await query.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.StartTime)
            .Select(x => new WorkTimeEntryListItemDto
            {
                WorkTimeEntryId = x.WorkTimeEntryId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.Name : null,
                WorkDate = x.WorkDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                BreakMinutes = x.BreakMinutes,
                HoursWorked = x.HoursWorked,
                HourlyRate = x.HourlyRate,
                TravelKilometers = x.TravelKilometers,
                TravelRatePerKilometer = x.TravelRatePerKilometer,
                Description = x.Description,
                Comment = x.Comment,
                CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                CustomerInvoicedAt = x.CustomerInvoicedAt,
                IsPaid = x.IsPaid,
                PaidAt = x.PaidAt,
                LineTotal = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer)
            }).ToListAsync();

        return Ok(entries);
    }

    [HttpPost]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> CreateEntry([FromBody] SaveWorkTimeEntryRequest request)
    {
        var duration = request.EndTime - request.StartTime - TimeSpan.FromMinutes(request.BreakMinutes);
        if (duration <= TimeSpan.Zero)
        {
            return ValidationProblem("Start, Ende und Pause ergeben keine positive Arbeitszeit.");
        }

        var entry = new WorkTimeEntry
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            WorkDate = request.WorkDate.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakMinutes = request.BreakMinutes,
            HoursWorked = Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero),
            HourlyRate = request.HourlyRate,
            TravelKilometers = request.TravelKilometers,
            TravelRatePerKilometer = request.TravelRatePerKilometer,
            Description = request.Description.Trim(),
            Comment = request.Comment.Trim()
        };

        db.WorkTimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId));
    }

    [HttpPut("{entryId:int}/status")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> UpdateStatus(int entryId, [FromBody] UpdateWorkTimeStatusRequest request)
    {
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId);
        if (entry is null)
        {
            return NotFound();
        }

        var invoiceNumber = string.IsNullOrWhiteSpace(request.CustomerInvoiceNumber) ? null : request.CustomerInvoiceNumber.Trim();
        if (request.MarkInvoiced)
        {
            entry.CustomerInvoiceNumber = invoiceNumber ?? entry.CustomerInvoiceNumber;
            entry.CustomerInvoicedAt ??= DateTime.UtcNow;
        }

        if (request.MarkPaid)
        {
            entry.CustomerInvoiceNumber = invoiceNumber ?? entry.CustomerInvoiceNumber;
            entry.CustomerInvoicedAt ??= DateTime.UtcNow;
            entry.IsPaid = true;
            entry.PaidAt = DateTime.UtcNow;
        }

        if (!request.MarkInvoiced && !request.MarkPaid && invoiceNumber != null)
        {
            entry.CustomerInvoiceNumber = invoiceNumber;
        }

        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entryId));
    }

    [HttpDelete("{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int entryId)
    {
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId);
        if (entry is null)
        {
            return NotFound();
        }

        db.WorkTimeEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<WorkTimeEntryListItemDto> GetMappedEntryAsync(int entryId)
    {
        return db.WorkTimeEntries.Include(x => x.Customer).Include(x => x.Project)
            .Where(x => x.WorkTimeEntryId == entryId)
            .Select(x => new WorkTimeEntryListItemDto
            {
                WorkTimeEntryId = x.WorkTimeEntryId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.Name : null,
                WorkDate = x.WorkDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                BreakMinutes = x.BreakMinutes,
                HoursWorked = x.HoursWorked,
                HourlyRate = x.HourlyRate,
                TravelKilometers = x.TravelKilometers,
                TravelRatePerKilometer = x.TravelRatePerKilometer,
                Description = x.Description,
                Comment = x.Comment,
                CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                CustomerInvoicedAt = x.CustomerInvoicedAt,
                IsPaid = x.IsPaid,
                PaidAt = x.PaidAt,
                LineTotal = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer)
            }).FirstAsync();
    }
}

[ApiController]
[Route("api/dashboard")]
public class DashboardController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var summary = new DashboardSummaryDto
        {
            CustomerCount = await db.Customers.CountAsync(),
            ProjectCount = await db.Projects.CountAsync(),
            OpenMaterialItemCount = await db.LineAllocations.CountAsync(x => !x.IsPaid),
            OpenWorkItemCount = await db.WorkTimeEntries.CountAsync(x => !x.IsPaid),
            LoggedHoursCurrentMonth = await db.WorkTimeEntries.Where(x => x.WorkDate >= monthStart).SumAsync(x => (decimal?)x.HoursWorked) ?? 0m,
            PaidRevenue = await db.LineAllocations.Where(x => x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice)) ?? 0m,
            OpenRevenue = await db.LineAllocations.Where(x => !x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice)) ?? 0m
        };

        summary.PaidRevenue += await db.WorkTimeEntries.Where(x => x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer))) ?? 0m;
        summary.OpenRevenue += await db.WorkTimeEntries.Where(x => !x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer))) ?? 0m;
        return Ok(summary);
    }
}
