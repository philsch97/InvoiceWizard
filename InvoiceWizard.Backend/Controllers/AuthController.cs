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

        var slug = await BuildUniqueSlugAsync(request.TenantName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ValidationProblem("Bitte einen gueltigen Firmennamen angeben.");
        }

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = slug
        };

        var user = new AppUser
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password),
            IsPlatformAdmin = true
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

        var license = new TenantLicense
        {
            Tenant = tenant,
            SubscriptionPlan = defaultPlan,
            ValidFrom = DateTime.UtcNow,
            BillingCycle = "Yearly",
            PriceNet = 0,
            RenewsAutomatically = false,
            NextBillingDate = DateTime.UtcNow.Date.AddYears(1),
            IsActive = true
        };

        db.TenantLicenses.Add(license);
        db.UserTenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        return Ok(CreateAuthResponse(user, tenant, membership.Role, license));
    }

    [AllowAnonymous]
    [HttpPost("activate-license")]
    public async Task<ActionResult<AuthResponseDto>> ActivateLicense([FromBody] ActivateLicenseRequest request)
    {
        var code = NormalizeActivationCode(request.ActivationCode);
        var activation = await db.LicenseActivations
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.ActivationCode == code);

        if (activation is null)
        {
            return NotFound(new { message = "Der Aktivierungscode wurde nicht gefunden." });
        }

        if (activation.IsUsed)
        {
            return Conflict(new { message = "Der Aktivierungscode wurde bereits verwendet." });
        }

        if (activation.ValidUntil.HasValue && activation.ValidUntil.Value.Date < DateTime.UtcNow.Date)
        {
            return ValidationProblem("Der Aktivierungscode ist abgelaufen.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(activation.CustomerEmail) && !string.Equals(activation.CustomerEmail.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Dieser Aktivierungscode ist fuer eine andere E-Mail-Adresse reserviert.");
        }

        if (await db.AppUsers.AnyAsync(x => x.Email == normalizedEmail))
        {
            return Conflict(new { message = "Diese E-Mail-Adresse ist bereits vergeben." });
        }

        var slug = await BuildUniqueSlugAsync(request.TenantName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ValidationProblem("Bitte einen gueltigen Firmennamen angeben.");
        }

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = slug
        };

        var user = new AppUser
        {
            Email = normalizedEmail,
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

        var license = new TenantLicense
        {
            Tenant = tenant,
            SubscriptionPlanId = activation.SubscriptionPlanId,
            SubscriptionPlan = activation.SubscriptionPlan,
            ValidFrom = DateTime.UtcNow,
            ValidUntil = activation.ValidUntil,
            MaxUsersOverride = activation.MaxUsersOverride,
            MaxProjectsOverride = activation.MaxProjectsOverride,
            MaxCustomersOverride = activation.MaxCustomersOverride,
            IncludesMobileAccessOverride = activation.IncludesMobileAccessOverride,
            BillingCycle = NormalizeBillingCycle(activation.BillingCycle),
            PriceNet = activation.PriceNet,
            RenewsAutomatically = activation.RenewsAutomatically,
            NextBillingDate = CalculateNextBillingDate(DateTime.UtcNow, activation.BillingCycle),
            IsActive = true
        };

        activation.IsUsed = true;
        activation.UsedAt = DateTime.UtcNow;
        activation.UsedByAppUser = user;

        db.TenantLicenses.Add(license);
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

        var license = await GetCurrentLicenseAsync(membership.TenantId);
        if (license is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Fuer diese Firma ist aktuell keine gueltige Lizenz aktiv." });
        }

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

        var license = await GetCurrentLicenseAsync(tenantId);
        return Ok(CreateAuthResponse(user, membership.Tenant, membership.Role, license));
    }

    private async Task<string> BuildUniqueSlugAsync(string tenantName)
    {
        var baseSlug = DatabaseInitializer.CreateSlug(tenantName);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            return string.Empty;
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await db.Tenants.AnyAsync(x => x.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private Task<TenantLicense?> GetCurrentLicenseAsync(int tenantId)
    {
        var now = DateTime.UtcNow;
        return db.TenantLicenses
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == tenantId && x.IsActive && (!x.ValidUntil.HasValue || x.ValidUntil.Value >= now || (x.GraceUntil.HasValue && x.GraceUntil.Value >= now)))
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync();
    }

    internal static int GetEffectiveMaxUsers(TenantLicense license) => license.MaxUsersOverride ?? license.SubscriptionPlan.MaxUsers;
    internal static int GetEffectiveMaxProjects(TenantLicense license) => license.MaxProjectsOverride ?? license.SubscriptionPlan.MaxProjects;
    internal static int GetEffectiveMaxCustomers(TenantLicense license) => license.MaxCustomersOverride ?? license.SubscriptionPlan.MaxCustomers;
    internal static bool GetEffectiveMobileAccess(TenantLicense license) => license.IncludesMobileAccessOverride ?? license.SubscriptionPlan.IncludesMobileAccess;
    internal static string GetLicenseStatus(TenantLicense license)
    {
        var now = DateTime.UtcNow;
        if (!license.IsActive)
        {
            return "Gesperrt";
        }

        if (license.ValidUntil.HasValue && license.ValidUntil.Value < now)
        {
            if (license.GraceUntil.HasValue && license.GraceUntil.Value >= now)
            {
                return "Grace-Phase";
            }

            return "Abgelaufen";
        }

        if (license.CancelledAt.HasValue)
        {
            return "Gekuendigt";
        }

        return "Aktiv";
    }

    internal static string NormalizeBillingCycle(string? billingCycle)
    {
        var value = (billingCycle ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "yearly" or "jaehrlich" => "Yearly",
            "manual" or "manuell" => "Manual",
            _ => "Monthly"
        };
    }

    internal static DateTime? CalculateNextBillingDate(DateTime validFromUtc, string? billingCycle)
    {
        return NormalizeBillingCycle(billingCycle) switch
        {
            "Yearly" => validFromUtc.Date.AddYears(1),
            "Manual" => null,
            _ => validFromUtc.Date.AddMonths(1)
        };
    }

    private static string NormalizeActivationCode(string value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

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
                Role = role,
                IsPlatformAdmin = user.IsPlatformAdmin
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
                MaxUsers = GetEffectiveMaxUsers(license),
                MaxProjects = GetEffectiveMaxProjects(license),
                MaxCustomers = GetEffectiveMaxCustomers(license),
                IncludesMobileAccess = GetEffectiveMobileAccess(license),
                BillingCycle = NormalizeBillingCycle(license.BillingCycle),
                PriceNet = license.PriceNet,
                RenewsAutomatically = license.RenewsAutomatically,
                NextBillingDate = license.NextBillingDate,
                CancelledAt = license.CancelledAt,
                GraceUntil = license.GraceUntil,
                Status = GetLicenseStatus(license),
                ValidFrom = license.ValidFrom,
                ValidUntil = license.ValidUntil,
                IsActive = license.IsActive
            }
        };
    }
}
