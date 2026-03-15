using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace InvoiceWizard.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/customers")]
public class CustomersController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> GetCustomers()
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var customers = await db.Customers.Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new CustomerListItemDto
            {
                CustomerId = x.CustomerId,
                Name = x.Name,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Street = x.Street,
                HouseNumber = x.HouseNumber,
                PostalCode = x.PostalCode,
                City = x.City,
                EmailAddress = x.EmailAddress,
                PhoneNumber = x.PhoneNumber,
                DefaultMarkupPercent = x.DefaultMarkupPercent,
                ProjectCount = x.Projects.Count,
                OpenWorkItems = x.WorkTimeEntries.Count(w => !w.IsPaid)
            }).ToListAsync();
        return Ok(customers);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerListItemDto>> SaveCustomer([FromBody] SaveCustomerRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var displayName = BuildCustomerDisplayName(request);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ValidationProblem("Bitte mindestens Vorname/Nachname oder einen Kundennamen angeben.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == displayName);
        if (customer is null)
        {
            customer = new Customer { TenantId = tenantId };
            ApplyCustomer(customer, request, displayName);
            db.Customers.Add(customer);
        }
        else
        {
            ApplyCustomer(customer, request, displayName);
        }

        await db.SaveChangesAsync();
        return Ok(MapCustomer(customer));
    }

    [HttpPut("{customerId:int}")]
    public async Task<ActionResult<CustomerListItemDto>> UpdateCustomer(int customerId, [FromBody] SaveCustomerRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.TenantId == tenantId);
        if (customer is null)
        {
            return NotFound();
        }

        var displayName = BuildCustomerDisplayName(request);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ValidationProblem("Bitte mindestens Vorname/Nachname oder einen Kundennamen angeben.");
        }

        ApplyCustomer(customer, request, displayName);
        await db.SaveChangesAsync();
        return Ok(MapCustomer(customer));
    }

    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> DeleteCustomer(int customerId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.TenantId == tenantId);
        if (customer is null)
        {
            return NotFound();
        }

        var todoListIds = await db.TodoLists.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).Select(x => x.TodoListId).ToListAsync();
        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => todoListIds.Contains(x.TodoListId) && x.TenantId == tenantId).ToListAsync());
        db.TodoAttachments.RemoveRange(await db.TodoAttachments.Where(x => todoListIds.Contains(x.TodoListId) && x.TenantId == tenantId).ToListAsync());
        db.TodoLists.RemoveRange(await db.TodoLists.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).ToListAsync());
        db.LineAllocations.RemoveRange(await db.LineAllocations.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).ToListAsync());
        db.WorkTimeEntries.RemoveRange(await db.WorkTimeEntries.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).ToListAsync());
        db.Projects.RemoveRange(await db.Projects.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).ToListAsync());
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{customerId:int}/projects")]
    public async Task<ActionResult<IReadOnlyList<ProjectListItemDto>>> GetProjectsForCustomer(int customerId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var projects = await db.Projects.Where(x => x.CustomerId == customerId && x.TenantId == tenantId).OrderBy(x => x.Name)
            .Select(x => new ProjectListItemDto
            {
                ProjectId = x.ProjectId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.Name,
                Name = x.Name,
                OpenWorkItems = x.WorkTimeEntries.Count(w => !w.IsPaid),
                LoggedHours = x.WorkTimeEntries.Sum(w => (decimal?)w.HoursWorked) ?? 0m
            })
            .ToListAsync();

        return Ok(projects);
    }

    [HttpPost("{customerId:int}/projects")]
    public async Task<ActionResult<ProjectListItemDto>> SaveProject(int customerId, [FromBody] SaveProjectRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.TenantId == tenantId);
        if (customer is null)
        {
            return NotFound();
        }

        var projectName = request.Name.Trim();
        var project = await db.Projects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == customerId && x.Name == projectName);
        if (project is null)
        {
            project = new Project { TenantId = tenantId, CustomerId = customerId, Name = projectName };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }

        return Ok(MapProjectListItem(project, customer.Name));
    }

    private static string BuildCustomerDisplayName(SaveCustomerRequest request)
    {
        var composed = string.Join(" ", new[] { request.FirstName?.Trim(), request.LastName?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(composed) ? (request.Name ?? string.Empty).Trim() : composed.Trim();
    }

    private static void ApplyCustomer(Customer customer, SaveCustomerRequest request, string displayName)
    {
        customer.Name = displayName;
        customer.FirstName = (request.FirstName ?? string.Empty).Trim();
        customer.LastName = (request.LastName ?? string.Empty).Trim();
        customer.Street = (request.Street ?? string.Empty).Trim();
        customer.HouseNumber = (request.HouseNumber ?? string.Empty).Trim();
        customer.PostalCode = (request.PostalCode ?? string.Empty).Trim();
        customer.City = (request.City ?? string.Empty).Trim();
        customer.EmailAddress = (request.EmailAddress ?? string.Empty).Trim();
        customer.PhoneNumber = (request.PhoneNumber ?? string.Empty).Trim();
        customer.DefaultMarkupPercent = request.DefaultMarkupPercent;
    }

    internal static CustomerListItemDto MapCustomer(Customer customer)
    {
        return new CustomerListItemDto
        {
            CustomerId = customer.CustomerId,
            Name = customer.Name,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Street = customer.Street,
            HouseNumber = customer.HouseNumber,
            PostalCode = customer.PostalCode,
            City = customer.City,
            EmailAddress = customer.EmailAddress,
            PhoneNumber = customer.PhoneNumber,
            DefaultMarkupPercent = customer.DefaultMarkupPercent
        };
    }

    internal static ProjectListItemDto MapProjectListItem(Project project, string? customerName = null)
    {
        return new ProjectListItemDto
        {
            ProjectId = project.ProjectId,
            CustomerId = project.CustomerId,
            CustomerName = customerName ?? project.Customer.Name,
            Name = project.Name,
            OpenWorkItems = project.WorkTimeEntries.Count(w => !w.IsPaid),
            LoggedHours = project.WorkTimeEntries.Sum(w => (decimal?)w.HoursWorked) ?? 0m
        };
    }
}

[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectsController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectListItemDto>>> GetProjects([FromQuery] int? customerId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var query = db.Projects.Where(x => x.TenantId == tenantId).AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        var projects = await query.Include(x => x.Customer)
            .Include(x => x.WorkTimeEntries)
            .OrderBy(x => x.Customer.Name).ThenBy(x => x.Name)
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

    [HttpGet("{projectId:int}")]
    public async Task<ActionResult<ProjectDetailsDto>> GetProjectDetails(int projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var project = await db.Projects.Include(x => x.Customer).FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == tenantId);
        if (project is null)
        {
            return NotFound();
        }

        return Ok(MapProjectDetails(project));
    }

    [HttpPut("{projectId:int}/details")]
    public async Task<ActionResult<ProjectDetailsDto>> UpdateProjectDetails(int projectId, [FromBody] SaveProjectDetailsRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var project = await db.Projects.Include(x => x.Customer).FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == tenantId);
        if (project is null)
        {
            return NotFound();
        }

        ApplyProjectDetails(project, request);
        await db.SaveChangesAsync();
        return Ok(MapProjectDetails(project));
    }

    [HttpDelete("{projectId:int}")]
    public async Task<IActionResult> DeleteProject(int projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var project = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == tenantId);
        if (project is null)
        {
            return NotFound();
        }

        var todoListIds = await db.TodoLists.Where(x => x.ProjectId == projectId && x.TenantId == tenantId).Select(x => x.TodoListId).ToListAsync();
        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => todoListIds.Contains(x.TodoListId) && x.TenantId == tenantId).ToListAsync());
        db.TodoAttachments.RemoveRange(await db.TodoAttachments.Where(x => todoListIds.Contains(x.TodoListId) && x.TenantId == tenantId).ToListAsync());
        db.TodoLists.RemoveRange(await db.TodoLists.Where(x => x.ProjectId == projectId && x.TenantId == tenantId).ToListAsync());
        db.LineAllocations.RemoveRange(await db.LineAllocations.Where(x => x.ProjectId == projectId && x.TenantId == tenantId).ToListAsync());
        db.WorkTimeEntries.RemoveRange(await db.WorkTimeEntries.Where(x => x.ProjectId == projectId && x.TenantId == tenantId).ToListAsync());
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void ApplyProjectDetails(Project project, SaveProjectDetailsRequest request)
    {
        project.ConnectionUserSameAsCustomer = request.ConnectionUserSameAsCustomer;
        project.ConnectionUserFirstName = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserFirstName ?? string.Empty).Trim();
        project.ConnectionUserLastName = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserLastName ?? string.Empty).Trim();
        project.ConnectionUserStreet = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserStreet ?? string.Empty).Trim();
        project.ConnectionUserHouseNumber = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserHouseNumber ?? string.Empty).Trim();
        project.ConnectionUserPostalCode = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserPostalCode ?? string.Empty).Trim();
        project.ConnectionUserCity = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserCity ?? string.Empty).Trim();
        project.ConnectionUserParcelNumber = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserParcelNumber ?? string.Empty).Trim();
        project.ConnectionUserEmailAddress = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserEmailAddress ?? string.Empty).Trim();
        project.ConnectionUserPhoneNumber = request.ConnectionUserSameAsCustomer ? string.Empty : (request.ConnectionUserPhoneNumber ?? string.Empty).Trim();

        project.PropertyOwnerSameAsCustomer = request.PropertyOwnerSameAsCustomer;
        project.PropertyOwnerFirstName = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerFirstName ?? string.Empty).Trim();
        project.PropertyOwnerLastName = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerLastName ?? string.Empty).Trim();
        project.PropertyOwnerStreet = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerStreet ?? string.Empty).Trim();
        project.PropertyOwnerHouseNumber = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerHouseNumber ?? string.Empty).Trim();
        project.PropertyOwnerPostalCode = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerPostalCode ?? string.Empty).Trim();
        project.PropertyOwnerCity = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerCity ?? string.Empty).Trim();
        project.PropertyOwnerEmailAddress = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerEmailAddress ?? string.Empty).Trim();
        project.PropertyOwnerPhoneNumber = request.PropertyOwnerSameAsCustomer ? string.Empty : (request.PropertyOwnerPhoneNumber ?? string.Empty).Trim();
    }

    private static ProjectDetailsDto MapProjectDetails(Project project)
    {
        return new ProjectDetailsDto
        {
            ProjectId = project.ProjectId,
            CustomerId = project.CustomerId,
            CustomerName = project.Customer.Name,
            Name = project.Name,
            ConnectionUserSameAsCustomer = project.ConnectionUserSameAsCustomer,
            ConnectionUserFirstName = project.ConnectionUserSameAsCustomer ? project.Customer.FirstName : project.ConnectionUserFirstName,
            ConnectionUserLastName = project.ConnectionUserSameAsCustomer ? project.Customer.LastName : project.ConnectionUserLastName,
            ConnectionUserStreet = project.ConnectionUserSameAsCustomer ? project.Customer.Street : project.ConnectionUserStreet,
            ConnectionUserHouseNumber = project.ConnectionUserSameAsCustomer ? project.Customer.HouseNumber : project.ConnectionUserHouseNumber,
            ConnectionUserPostalCode = project.ConnectionUserSameAsCustomer ? project.Customer.PostalCode : project.ConnectionUserPostalCode,
            ConnectionUserCity = project.ConnectionUserSameAsCustomer ? project.Customer.City : project.ConnectionUserCity,
            ConnectionUserParcelNumber = project.ConnectionUserSameAsCustomer ? string.Empty : project.ConnectionUserParcelNumber,
            ConnectionUserEmailAddress = project.ConnectionUserSameAsCustomer ? project.Customer.EmailAddress : project.ConnectionUserEmailAddress,
            ConnectionUserPhoneNumber = project.ConnectionUserSameAsCustomer ? project.Customer.PhoneNumber : project.ConnectionUserPhoneNumber,
            PropertyOwnerSameAsCustomer = project.PropertyOwnerSameAsCustomer,
            PropertyOwnerFirstName = project.PropertyOwnerSameAsCustomer ? project.Customer.FirstName : project.PropertyOwnerFirstName,
            PropertyOwnerLastName = project.PropertyOwnerSameAsCustomer ? project.Customer.LastName : project.PropertyOwnerLastName,
            PropertyOwnerStreet = project.PropertyOwnerSameAsCustomer ? project.Customer.Street : project.PropertyOwnerStreet,
            PropertyOwnerHouseNumber = project.PropertyOwnerSameAsCustomer ? project.Customer.HouseNumber : project.PropertyOwnerHouseNumber,
            PropertyOwnerPostalCode = project.PropertyOwnerSameAsCustomer ? project.Customer.PostalCode : project.PropertyOwnerPostalCode,
            PropertyOwnerCity = project.PropertyOwnerSameAsCustomer ? project.Customer.City : project.PropertyOwnerCity,
            PropertyOwnerEmailAddress = project.PropertyOwnerSameAsCustomer ? project.Customer.EmailAddress : project.PropertyOwnerEmailAddress,
            PropertyOwnerPhoneNumber = project.PropertyOwnerSameAsCustomer ? project.Customer.PhoneNumber : project.PropertyOwnerPhoneNumber
        };
    }
}

[Authorize]
[ApiController]
[Route("api/worktimeentries")]
public class WorkTimeEntriesController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTimeEntryListItemDto>>> GetEntries([FromQuery] int? customerId, [FromQuery] int? projectId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var query = db.WorkTimeEntries.Include(x => x.Customer).Include(x => x.Project).Where(x => x.TenantId == tenantId).AsQueryable();
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
                AppUserId = x.AppUserId,
                UserDisplayName = x.AppUser != null ? x.AppUser.DisplayName : "",
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
                IsClockActive = x.IsClockActive,
                PauseStartedAtUtc = x.PauseStartedAtUtc,
                LineTotal = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer)
            }).ToListAsync();

        return Ok(entries);
    }

    [HttpGet("clock/active")]
    public async Task<ActionResult<WorkTimeEntryListItemDto?>> GetActiveClockEntry()
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        var entry = await GetActiveClockEntryAsync(tenantId, currentUserId);
        if (entry is null)
        {
            return Ok(null);
        }

        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPost]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> CreateEntry([FromBody] SaveWorkTimeEntryRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        var duration = request.EndTime - request.StartTime - TimeSpan.FromMinutes(request.BreakMinutes);
        if (duration <= TimeSpan.Zero)
        {
            return ValidationProblem("Start, Ende und Pause ergeben keine positive Arbeitszeit.");
        }

        var customerExists = await db.Customers.AnyAsync(x => x.CustomerId == request.CustomerId && x.TenantId == tenantId);
        if (!customerExists)
        {
            return NotFound();
        }

        if (request.ProjectId.HasValue)
        {
            var projectExists = await db.Projects.AnyAsync(x => x.ProjectId == request.ProjectId.Value && x.CustomerId == request.CustomerId && x.TenantId == tenantId);
            if (!projectExists)
            {
                return ValidationProblem("Das ausgewaehlte Projekt gehoert nicht zum Kunden.");
            }
        }

        var entry = new WorkTimeEntry
        {
            TenantId = tenantId,
            AppUserId = currentUserId > 0 ? currentUserId : null,
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
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPost("clock/start")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> StartClock([FromBody] StartWorkTimeClockRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized();
        }

        if (await GetActiveClockEntryAsync(tenantId, currentUserId) is not null)
        {
            return Conflict("Es laeuft bereits eine aktive Projektzeit. Bitte stoppe oder pausiere sie zuerst.");
        }

        var validationError = await ValidateCustomerProjectAsync(tenantId, request.CustomerId, request.ProjectId);
        if (validationError is not null)
        {
            return validationError;
        }

        var startedAt = request.StartedAt;
        var entry = new WorkTimeEntry
        {
            TenantId = tenantId,
            AppUserId = currentUserId,
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            WorkDate = startedAt.Date,
            StartTime = startedAt.TimeOfDay,
            EndTime = startedAt.TimeOfDay,
            BreakMinutes = 0,
            HoursWorked = 0m,
            HourlyRate = request.HourlyRate,
            TravelKilometers = 0m,
            TravelRatePerKilometer = request.TravelRatePerKilometer,
            Description = request.Description.Trim(),
            Comment = "",
            IsClockActive = true
        };

        db.WorkTimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPost("clock/pause/start")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> StartPause([FromBody] ChangeWorkTimePauseRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        var entry = await GetActiveClockEntryAsync(tenantId, currentUserId);
        if (entry is null)
        {
            return NotFound("Es gibt keine aktive Projektzeit zum Pausieren.");
        }

        if (entry.PauseStartedAtUtc.HasValue)
        {
            return Conflict("Die Pause laeuft bereits.");
        }

        entry.PauseStartedAtUtc = request.ChangedAt.UtcDateTime;
        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPost("clock/pause/stop")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> StopPause([FromBody] ChangeWorkTimePauseRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        var entry = await GetActiveClockEntryAsync(tenantId, currentUserId);
        if (entry is null)
        {
            return NotFound("Es gibt keine aktive Projektzeit fuer das Pausenende.");
        }

        if (!entry.PauseStartedAtUtc.HasValue)
        {
            return Conflict("Es laeuft aktuell keine Pause.");
        }

        entry.BreakMinutes += CalculatePauseMinutes(entry.PauseStartedAtUtc.Value, request.ChangedAt.UtcDateTime);
        entry.PauseStartedAtUtc = null;
        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPost("clock/stop")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> StopClock([FromBody] StopWorkTimeClockRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var currentUserId = GetCurrentUserId();
        var entry = await GetActiveClockEntryAsync(tenantId, currentUserId);
        if (entry is null)
        {
            return NotFound("Es gibt keine aktive Projektzeit zum Beenden.");
        }

        var endedAt = request.EndedAt;
        if (endedAt.Date != entry.WorkDate.Date)
        {
            return ValidationProblem("Projektzeiten ueber mehrere Tage werden aktuell nicht unterstuetzt. Bitte vor Mitternacht stoppen.");
        }

        if (entry.PauseStartedAtUtc.HasValue)
        {
            entry.BreakMinutes += CalculatePauseMinutes(entry.PauseStartedAtUtc.Value, endedAt.UtcDateTime);
            entry.PauseStartedAtUtc = null;
        }

        entry.EndTime = endedAt.TimeOfDay;
        if (entry.EndTime <= entry.StartTime)
        {
            return ValidationProblem("Die Endzeit muss nach der Startzeit liegen.");
        }

        var duration = entry.EndTime - entry.StartTime - TimeSpan.FromMinutes(entry.BreakMinutes);
        if (duration <= TimeSpan.Zero)
        {
            return ValidationProblem("Die berechnete Arbeitszeit muss groesser als 0 sein.");
        }

        entry.HoursWorked = Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
        entry.TravelKilometers = request.TravelKilometers;
        entry.Comment = request.Comment.Trim();
        entry.IsClockActive = false;
        await db.SaveChangesAsync();
        return Ok(await GetMappedEntryAsync(entry.WorkTimeEntryId, tenantId));
    }

    [HttpPut("{entryId:int}/status")]
    public async Task<ActionResult<WorkTimeEntryListItemDto>> UpdateStatus(int entryId, [FromBody] UpdateWorkTimeStatusRequest request)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId && x.TenantId == tenantId);
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
        return Ok(await GetMappedEntryAsync(entryId, tenantId));
    }

    [HttpDelete("{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int entryId)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var entry = await db.WorkTimeEntries.FirstOrDefaultAsync(x => x.WorkTimeEntryId == entryId && x.TenantId == tenantId);
        if (entry is null)
        {
            return NotFound();
        }

        db.WorkTimeEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<WorkTimeEntryListItemDto> GetMappedEntryAsync(int entryId, int tenantId)
    {
        return db.WorkTimeEntries.Include(x => x.Customer).Include(x => x.Project)
            .Where(x => x.WorkTimeEntryId == entryId && x.TenantId == tenantId)
            .Select(x => new WorkTimeEntryListItemDto
            {
                WorkTimeEntryId = x.WorkTimeEntryId,
                AppUserId = x.AppUserId,
                UserDisplayName = x.AppUser != null ? x.AppUser.DisplayName : "",
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
                IsClockActive = x.IsClockActive,
                PauseStartedAtUtc = x.PauseStartedAtUtc,
                LineTotal = x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer)
            }).FirstAsync();
    }

    private async Task<WorkTimeEntry?> GetActiveClockEntryAsync(int tenantId, int currentUserId)
    {
        if (currentUserId <= 0)
        {
            return null;
        }

        return await db.WorkTimeEntries
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.AppUserId == currentUserId && x.IsClockActive);
    }

    private async Task<ActionResult?> ValidateCustomerProjectAsync(int tenantId, int customerId, int? projectId)
    {
        var customerExists = await db.Customers.AnyAsync(x => x.CustomerId == customerId && x.TenantId == tenantId);
        if (!customerExists)
        {
            return NotFound();
        }

        if (projectId.HasValue)
        {
            var projectExists = await db.Projects.AnyAsync(x => x.ProjectId == projectId.Value && x.CustomerId == customerId && x.TenantId == tenantId);
            if (!projectExists)
            {
                return ValidationProblem("Das ausgewaehlte Projekt gehoert nicht zum Kunden.");
            }
        }

        return null;
    }

    private static int CalculatePauseMinutes(DateTime pauseStartedAtUtc, DateTime pauseEndedAtUtc)
    {
        if (pauseEndedAtUtc <= pauseStartedAtUtc)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Round((pauseEndedAtUtc - pauseStartedAtUtc).TotalMinutes, MidpointRounding.AwayFromZero));
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(HttpContext.RequestAborted);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var summary = new DashboardSummaryDto
        {
            CustomerCount = await db.Customers.CountAsync(x => x.TenantId == tenantId),
            ProjectCount = await db.Projects.CountAsync(x => x.TenantId == tenantId),
            OpenMaterialItemCount = await db.LineAllocations.CountAsync(x => x.TenantId == tenantId && !x.IsPaid),
            OpenWorkItemCount = await db.WorkTimeEntries.CountAsync(x => x.TenantId == tenantId && !x.IsPaid),
            LoggedHoursCurrentMonth = await db.WorkTimeEntries.Where(x => x.TenantId == tenantId && x.WorkDate >= monthStart).SumAsync(x => (decimal?)x.HoursWorked) ?? 0m,
            PaidRevenue = await db.LineAllocations.Where(x => x.TenantId == tenantId && x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice)) ?? 0m,
            OpenRevenue = await db.LineAllocations.Where(x => x.TenantId == tenantId && !x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : x.AllocatedQuantity * x.CustomerUnitPrice)) ?? 0m
        };

        summary.PaidRevenue += await db.WorkTimeEntries.Where(x => x.TenantId == tenantId && x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer))) ?? 0m;
        summary.OpenRevenue += await db.WorkTimeEntries.Where(x => x.TenantId == tenantId && !x.IsPaid).SumAsync(x => (decimal?)(x.ExportedLineTotal > 0m ? x.ExportedLineTotal : (x.HoursWorked * x.HourlyRate) + (x.TravelKilometers * x.TravelRatePerKilometer))) ?? 0m;
        return Ok(summary);
    }
}


