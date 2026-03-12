using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using InvoiceWizard.Backend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceWizard.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    InvoiceWizardDbContext db,
    IPasswordHashService passwordHashService,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [AllowAnonymous]
    [HttpGet("bootstrap-state")]
    public async Task<ActionResult<BootstrapStateDto>> GetBootstrapState()
    {
        return Ok(new BootstrapStateDto
        {
            HasUsers = await db.AppUsers.AnyAsync(),
            HasTenants = await db.Tenants.AnyAsync()
        });
    }

    [AllowAnonymous]
    [HttpPost("bootstrap-admin")]
    public async Task<ActionResult<AuthResponseDto>> BootstrapAdmin([FromBody] BootstrapAdminRequest request)
    {
        if (await db.AppUsers.AnyAsync())
        {
            return Conflict(new { message = "Initial admin was already created." });
        }

        var slug = DatabaseInitializer.CreateSlug(request.TenantName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ValidationProblem("Bitte einen gueltigen Firmennamen angeben.");
        }

        Tenant tenant;
        var reusableTenants = await db.Tenants.Include(x => x.Memberships).Where(x => x.IsActive).OrderBy(x => x.TenantId).Take(2).ToListAsync();
        if (reusableTenants.Count == 1 && reusableTenants[0].Memberships.Count == 0)
        {
            tenant = reusableTenants[0];
            tenant.Name = request.TenantName.Trim();
            tenant.Slug = slug;
        }
        else
        {
            tenant = new Tenant
            {
                Name = request.TenantName.Trim(),
                Slug = slug
            };
        }

        var user = new AppUser
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password)
        };

        var membership = new UserTenantMembership
        {
            AppUser = user,
            Tenant = tenant,
            Role = TenantRoles.Admin,
            IsDefault = true,
            IsActive = true
        };

        var defaultPlan = await db.SubscriptionPlans.OrderByDescending(x => x.MaxUsers).FirstOrDefaultAsync(x => x.Code == "business")
            ?? await db.SubscriptionPlans.OrderByDescending(x => x.MaxUsers).FirstAsync();

        var license = await db.TenantLicenses.FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId && x.IsActive);
        if (license is null)
        {
            license = new TenantLicense
            {
                Tenant = tenant,
                SubscriptionPlan = defaultPlan,
                ValidFrom = DateTime.UtcNow,
                IsActive = true
            };
            db.TenantLicenses.Add(license);
        }

        db.UserTenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        return Ok(CreateAuthResponse(user, tenant, membership.Role, license));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.AppUsers
            .Include(x => x.Memberships.Where(m => m.IsActive))
                .ThenInclude(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive);

        if (user is null || !passwordHashService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "E-Mail oder Passwort ist ungueltig." });
        }

        var membership = user.Memberships
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.UserTenantMembershipId)
            .FirstOrDefault();

        if (membership is null || !membership.Tenant.IsActive)
        {
            return Forbid();
        }

        var license = await db.TenantLicenses
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == membership.TenantId && x.IsActive)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync();

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(CreateAuthResponse(user, membership.Tenant, membership.Role, license));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponseDto>> Me()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var tenantIdClaim = User.FindFirstValue("tenant_id");
        if (!int.TryParse(userIdClaim, out var userId) || !int.TryParse(tenantIdClaim, out var tenantId))
        {
            return Unauthorized();
        }

        var user = await db.AppUsers.FirstOrDefaultAsync(x => x.AppUserId == userId && x.IsActive);
        var membership = await db.UserTenantMemberships
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.AppUserId == userId && x.TenantId == tenantId && x.IsActive);

        if (user is null || membership is null)
        {
            return Unauthorized();
        }

        var license = await db.TenantLicenses
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync();

        return Ok(CreateAuthResponse(user, membership.Tenant, membership.Role, license));
    }

    private AuthResponseDto CreateAuthResponse(AppUser user, Tenant tenant, string role, TenantLicense? license)
    {
        return new AuthResponseDto
        {
            AccessToken = jwtTokenService.CreateAccessToken(user, tenant, role),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            User = new AuthUserDto
            {
                AppUserId = user.AppUserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = role
            },
            Tenant = new AuthTenantDto
            {
                TenantId = tenant.TenantId,
                Name = tenant.Name,
                Slug = tenant.Slug
            },
            License = license is null ? null : new AuthLicenseDto
            {
                TenantLicenseId = license.TenantLicenseId,
                PlanCode = license.SubscriptionPlan.Code,
                PlanName = license.SubscriptionPlan.Name,
                MaxUsers = license.SubscriptionPlan.MaxUsers,
                MaxProjects = license.SubscriptionPlan.MaxProjects,
                MaxCustomers = license.SubscriptionPlan.MaxCustomers,
                IncludesMobileAccess = license.SubscriptionPlan.IncludesMobileAccess,
                ValidFrom = license.ValidFrom,
                ValidUntil = license.ValidUntil,
                IsActive = license.IsActive
            }
        };
    }
}
