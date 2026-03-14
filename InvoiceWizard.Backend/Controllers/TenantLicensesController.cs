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
[Route("api/tenant-licenses")]
public class TenantLicensesController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantLicenseAdminDto>>> GetTenantLicenses(CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var items = await db.TenantLicenses
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Include(x => x.SubscriptionPlan)
            .OrderBy(x => x.Tenant.Name)
            .ThenByDescending(x => x.ValidFrom)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapLicense).ToList());
    }

    [HttpPut("{tenantLicenseId:int}")]
    public async Task<ActionResult<TenantLicenseAdminDto>> UpdateTenantLicense(int tenantLicenseId, [FromBody] UpdateTenantLicenseRequest request, CancellationToken cancellationToken)
    {
        if (!await IsPlatformAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var planCode = (request.PlanCode ?? string.Empty).Trim().ToLowerInvariant();
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Code == planCode && x.IsActive, cancellationToken);
        if (plan is null)
        {
            return ValidationProblem("Der ausgewaehlte Tarif wurde nicht gefunden.");
        }

        var license = await db.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.TenantLicenseId == tenantLicenseId, cancellationToken);

        if (license is null)
        {
            return NotFound();
        }

        license.SubscriptionPlanId = plan.SubscriptionPlanId;
        license.SubscriptionPlan = plan;
        license.BillingCycle = AuthController.NormalizeBillingCycle(request.BillingCycle);
        license.PriceNet = request.PriceNet;
        license.RenewsAutomatically = request.RenewsAutomatically;
        license.ValidUntil = request.ValidUntil;
        license.NextBillingDate = request.NextBillingDate;
        license.CancelledAt = request.CancelledAt;
        license.GraceUntil = request.GraceUntil;
        license.MaxUsersOverride = request.MaxUsersOverride;
        license.MaxProjectsOverride = request.MaxProjectsOverride;
        license.MaxCustomersOverride = request.MaxCustomersOverride;
        license.IncludesMobileAccessOverride = request.IncludesMobileAccessOverride;
        license.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(MapLicense(license));
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

    private static TenantLicenseAdminDto MapLicense(TenantLicense x)
    {
        return new TenantLicenseAdminDto
        {
            TenantLicenseId = x.TenantLicenseId,
            TenantId = x.TenantId,
            TenantName = x.Tenant.Name,
            TenantSlug = x.Tenant.Slug,
            PlanCode = x.SubscriptionPlan.Code,
            PlanName = x.SubscriptionPlan.Name,
            MaxUsers = x.MaxUsersOverride ?? x.SubscriptionPlan.MaxUsers,
            MaxProjects = x.MaxProjectsOverride ?? x.SubscriptionPlan.MaxProjects,
            MaxCustomers = x.MaxCustomersOverride ?? x.SubscriptionPlan.MaxCustomers,
            IncludesMobileAccess = x.IncludesMobileAccessOverride ?? x.SubscriptionPlan.IncludesMobileAccess,
            BillingCycle = AuthController.NormalizeBillingCycle(x.BillingCycle),
            PriceNet = x.PriceNet,
            RenewsAutomatically = x.RenewsAutomatically,
            ValidFrom = x.ValidFrom,
            ValidUntil = x.ValidUntil,
            NextBillingDate = x.NextBillingDate,
            CancelledAt = x.CancelledAt,
            GraceUntil = x.GraceUntil,
            IsActive = x.IsActive,
            Status = AuthController.GetLicenseStatus(x)
        };
    }
}
