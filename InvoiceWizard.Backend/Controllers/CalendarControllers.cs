using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/calendar")]
public class CalendarController(InvoiceWizardDbContext db, ICurrentTenantAccessor tenantAccessor) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<CalendarUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();

        var users = await db.UserTenantMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.AppUser.IsActive)
            .Include(x => x.AppUser)
            .OrderBy(x => x.AppUser.DisplayName)
            .Select(x => new CalendarUserDto
            {
                AppUserId = x.AppUserId,
                DisplayName = x.AppUser.DisplayName,
                Role = x.Role,
                CanEdit = x.AppUserId == currentUserId,
                IsCurrentUser = x.AppUserId == currentUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("entries")]
    public async Task<ActionResult<IReadOnlyList<CalendarEntryDto>>> GetEntries([FromQuery] int? appUserId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();
        var targetUserId = appUserId.GetValueOrDefault(currentUserId);

        var userExists = await db.UserTenantMemberships.AnyAsync(x => x.TenantId == tenantId && x.AppUserId == targetUserId && x.IsActive && x.AppUser.IsActive, cancellationToken);
        if (!userExists)
        {
            return NotFound();
        }

        var from = fromDate.Date;
        var to = toDate.Date;
        var entries = await db.CalendarEntries
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.AppUserId == targetUserId && x.EntryDate >= from && x.EntryDate <= to)
            .Include(x => x.AppUser)
            .Include(x => x.Customer)
            .OrderBy(x => x.EntryDate)
            .ThenBy(x => x.StartTime)
            .Select(x => new CalendarEntryDto
            {
                CalendarEntryId = x.CalendarEntryId,
                AppUserId = x.AppUserId,
                UserDisplayName = x.AppUser.DisplayName,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                CustomerStreet = x.Customer != null ? x.Customer.Street : null,
                CustomerHouseNumber = x.Customer != null ? x.Customer.HouseNumber : null,
                CustomerPostalCode = x.Customer != null ? x.Customer.PostalCode : null,
                CustomerCity = x.Customer != null ? x.Customer.City : null,
                EntryDate = NormalizeCalendarDate(x.EntryDate),
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Title = x.Title,
                Description = x.Description,
                Location = x.Location,
                UpdatedAt = x.UpdatedAt,
                CanEdit = x.AppUserId == currentUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpGet("weekly-overview")]
    public async Task<ActionResult<IReadOnlyList<CalendarEntryDto>>> GetWeeklyOverview([FromQuery] DateTime weekStart, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();
        var start = weekStart.Date;
        var end = start.AddDays(6);

        var entries = await db.CalendarEntries
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EntryDate >= start && x.EntryDate <= end)
            .Include(x => x.AppUser)
            .Include(x => x.Customer)
            .OrderBy(x => x.EntryDate)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.AppUser.DisplayName)
            .Select(x => new CalendarEntryDto
            {
                CalendarEntryId = x.CalendarEntryId,
                AppUserId = x.AppUserId,
                UserDisplayName = x.AppUser.DisplayName,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                CustomerStreet = x.Customer != null ? x.Customer.Street : null,
                CustomerHouseNumber = x.Customer != null ? x.Customer.HouseNumber : null,
                CustomerPostalCode = x.Customer != null ? x.Customer.PostalCode : null,
                CustomerCity = x.Customer != null ? x.Customer.City : null,
                EntryDate = NormalizeCalendarDate(x.EntryDate),
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Title = x.Title,
                Description = x.Description,
                Location = x.Location,
                UpdatedAt = x.UpdatedAt,
                CanEdit = x.AppUserId == currentUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpPost("entries")]
    public async Task<ActionResult<CalendarEntryDto>> CreateEntry([FromBody] SaveCalendarEntryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return ValidationProblem(validationError);
        }

        if (request.CustomerId.HasValue)
        {
            var customerExists = await db.Customers.AnyAsync(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId.Value, cancellationToken);
            if (!customerExists)
            {
                return ValidationProblem("Der ausgewaehlte Kunde wurde nicht gefunden.");
            }
        }

        var entry = new CalendarEntry
        {
            TenantId = tenantId,
            AppUserId = currentUserId,
            CustomerId = request.CustomerId,
            EntryDate = NormalizeCalendarDate(request.EntryDate),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim()
        };

        db.CalendarEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(await MapEntry(entry.CalendarEntryId, tenantId, currentUserId, cancellationToken));
    }

    [HttpPut("entries/{calendarEntryId:int}")]
    public async Task<ActionResult<CalendarEntryDto>> UpdateEntry(int calendarEntryId, [FromBody] SaveCalendarEntryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return ValidationProblem(validationError);
        }

        if (request.CustomerId.HasValue)
        {
            var customerExists = await db.Customers.AnyAsync(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId.Value, cancellationToken);
            if (!customerExists)
            {
                return ValidationProblem("Der ausgewaehlte Kunde wurde nicht gefunden.");
            }
        }

        var entry = await db.CalendarEntries.FirstOrDefaultAsync(x => x.CalendarEntryId == calendarEntryId && x.TenantId == tenantId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        if (entry.AppUserId != currentUserId)
        {
            return Forbid();
        }

        entry.EntryDate = NormalizeCalendarDate(request.EntryDate);
        entry.CustomerId = request.CustomerId;
        entry.StartTime = request.StartTime;
        entry.EndTime = request.EndTime;
        entry.Title = request.Title.Trim();
        entry.Description = request.Description.Trim();
        entry.Location = request.Location.Trim();
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(await MapEntry(entry.CalendarEntryId, tenantId, currentUserId, cancellationToken));
    }

    [HttpDelete("entries/{calendarEntryId:int}")]
    public async Task<IActionResult> DeleteEntry(int calendarEntryId, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId();
        var entry = await db.CalendarEntries.FirstOrDefaultAsync(x => x.CalendarEntryId == calendarEntryId && x.TenantId == tenantId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        if (entry.AppUserId != currentUserId)
        {
            return Forbid();
        }

        db.CalendarEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<CalendarEntryDto> MapEntry(int calendarEntryId, int tenantId, int currentUserId, CancellationToken cancellationToken)
    {
        return await db.CalendarEntries
            .AsNoTracking()
            .Where(x => x.CalendarEntryId == calendarEntryId && x.TenantId == tenantId)
            .Include(x => x.AppUser)
            .Include(x => x.Customer)
            .Select(x => new CalendarEntryDto
            {
                CalendarEntryId = x.CalendarEntryId,
                AppUserId = x.AppUserId,
                UserDisplayName = x.AppUser.DisplayName,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                CustomerStreet = x.Customer != null ? x.Customer.Street : null,
                CustomerHouseNumber = x.Customer != null ? x.Customer.HouseNumber : null,
                CustomerPostalCode = x.Customer != null ? x.Customer.PostalCode : null,
                CustomerCity = x.Customer != null ? x.Customer.City : null,
                EntryDate = NormalizeCalendarDate(x.EntryDate),
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Title = x.Title,
                Description = x.Description,
                Location = x.Location,
                UpdatedAt = x.UpdatedAt,
                CanEdit = x.AppUserId == currentUserId
            })
            .FirstAsync(cancellationToken);
    }

    private static string? ValidateRequest(SaveCalendarEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Bitte einen Titel eingeben.";
        }

        if (request.EndTime <= request.StartTime)
        {
            return "Die Endzeit muss nach der Startzeit liegen.";
        }

        if (request.CustomerId.HasValue && request.CustomerId.Value <= 0)
        {
            return "Bitte einen gueltigen Kunden auswaehlen.";
        }

        return null;
    }

    private static DateTime NormalizeCalendarDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}
