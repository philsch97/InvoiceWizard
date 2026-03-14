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
[Route("api/license-activations")]
public class LicenseActivationsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanListItemDto>>> GetPlans(CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var plans = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SubscriptionPlanListItemDto
            {
                SubscriptionPlanId = x.SubscriptionPlanId,
                Code = x.Code,
                Name = x.Name,
                MaxUsers = x.MaxUsers,
                MaxProjects = x.MaxProjects,
                MaxCustomers = x.MaxCustomers,
                IncludesMobileAccess = x.IncludesMobileAccess
            })
            .ToListAsync(cancellationToken);

        return Ok(plans);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LicenseActivationDto>>> GetActivations(CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var items = await db.LicenseActivations
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.CreatedByAppUser)
            .Include(x => x.UsedByAppUser)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LicenseActivationDto
            {
                LicenseActivationId = x.LicenseActivationId,
                ActivationCode = x.ActivationCode,
                PlanCode = x.SubscriptionPlan.Code,
                PlanName = x.SubscriptionPlan.Name,
                CustomerEmail = x.CustomerEmail,
                ValidUntil = x.ValidUntil,
                IsUsed = x.IsUsed,
                UsedAt = x.UsedAt,
                UsedByDisplayName = x.UsedByAppUser != null ? x.UsedByAppUser.DisplayName : null,
                CreatedAt = x.CreatedAt,
                CreatedByDisplayName = x.CreatedByAppUser.DisplayName,
                MaxUsers = x.MaxUsersOverride ?? x.SubscriptionPlan.MaxUsers,
                MaxProjects = x.MaxProjectsOverride ?? x.SubscriptionPlan.MaxProjects,
                MaxCustomers = x.MaxCustomersOverride ?? x.SubscriptionPlan.MaxCustomers,
                IncludesMobileAccess = x.IncludesMobileAccessOverride ?? x.SubscriptionPlan.IncludesMobileAccess
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<LicenseActivationDto>> CreateActivation([FromBody] CreateLicenseActivationRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var currentUserId = GetCurrentUserId();
        var planCode = (request.PlanCode ?? string.Empty).Trim().ToLowerInvariant();
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Code == planCode && x.IsActive, cancellationToken);
        if (plan is null)
        {
            return ValidationProblem("Der ausgewaehlte Tarif wurde nicht gefunden.");
        }

        var activation = new LicenseActivation
        {
            ActivationCode = GenerateActivationCode(),
            SubscriptionPlanId = plan.SubscriptionPlanId,
            CustomerEmail = (request.CustomerEmail ?? string.Empty).Trim().ToLowerInvariant(),
            ValidUntil = request.ValidUntil?.Date,
            MaxUsersOverride = request.MaxUsersOverride,
            MaxProjectsOverride = request.MaxProjectsOverride,
            MaxCustomersOverride = request.MaxCustomersOverride,
            IncludesMobileAccessOverride = request.IncludesMobileAccessOverride,
            CreatedByAppUserId = currentUserId
        };

        db.LicenseActivations.Add(activation);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.LicenseActivations
            .AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.CreatedByAppUser)
            .Where(x => x.LicenseActivationId == activation.LicenseActivationId)
            .Select(x => new LicenseActivationDto
            {
                LicenseActivationId = x.LicenseActivationId,
                ActivationCode = x.ActivationCode,
                PlanCode = x.SubscriptionPlan.Code,
                PlanName = x.SubscriptionPlan.Name,
                CustomerEmail = x.CustomerEmail,
                ValidUntil = x.ValidUntil,
                IsUsed = x.IsUsed,
                UsedAt = x.UsedAt,
                UsedByDisplayName = null,
                CreatedAt = x.CreatedAt,
                CreatedByDisplayName = x.CreatedByAppUser.DisplayName,
                MaxUsers = x.MaxUsersOverride ?? x.SubscriptionPlan.MaxUsers,
                MaxProjects = x.MaxProjectsOverride ?? x.SubscriptionPlan.MaxProjects,
                MaxCustomers = x.MaxCustomersOverride ?? x.SubscriptionPlan.MaxCustomers,
                IncludesMobileAccess = x.IncludesMobileAccessOverride ?? x.SubscriptionPlan.IncludesMobileAccess
            })
            .FirstAsync(cancellationToken);

        return Ok(dto);
    }

    private async Task<bool> IsPlatformAdminAsync(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return false;
        }

        return await db.AppUsers.AnyAsync(x => x.AppUserId == currentUserId && x.IsActive && x.IsPlatformAdmin, cancellationToken);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private static string GenerateActivationCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> buffer = stackalloc byte[16];
        Random.Shared.NextBytes(buffer);
        var chars = new char[19];
        var charIndex = 0;
        for (var i = 0; i < buffer.Length && charIndex < chars.Length; i++)
        {
            if (charIndex > 0 && charIndex % 5 == 4)
            {
                chars[charIndex++] = '-';
            }

            chars[charIndex++] = alphabet[buffer[i] % alphabet.Length];
        }

        return new string(chars, 0, charIndex);
    }
}
