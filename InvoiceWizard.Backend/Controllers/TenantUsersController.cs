using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using InvoiceWizard.Backend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[ApiController]
[Route("api/tenant-users")]
[Authorize]
public class TenantUsersController(
    InvoiceWizardDbContext db,
    ICurrentTenantAccessor currentTenantAccessor,
    IPasswordHashService passwordHashService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        var tenantId = await currentTenantAccessor.GetTenantIdAsync(cancellationToken);
        var users = await db.UserTenantMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.AppUser)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.AppUser.DisplayName)
            .Select(x => new TenantUserDto
            {
                AppUserId = x.AppUserId,
                Email = x.AppUser.Email,
                DisplayName = x.AppUser.DisplayName,
                Role = x.Role,
                IsActive = x.IsActive && x.AppUser.IsActive,
                IsDefault = x.IsDefault,
                CreatedAt = x.AppUser.CreatedAt,
                LastLoginAt = x.AppUser.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<TenantUserDto>> CreateUser([FromBody] CreateTenantUserRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        if (!IsValidRole(request.Role))
        {
            return ValidationProblem("Die Rolle ist ungueltig.");
        }

        var tenantId = await currentTenantAccessor.GetTenantIdAsync(cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.AppUsers.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            return Conflict(new { message = "Diese E-Mail-Adresse ist bereits vergeben." });
        }

        var license = await db.TenantLicenses
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (license is not null)
        {
            var activeUsers = await db.UserTenantMemberships.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (activeUsers >= license.SubscriptionPlan.MaxUsers)
            {
                return Conflict(new { message = $"Das Benutzerlimit fuer den Tarif {license.SubscriptionPlan.Name} ist erreicht." });
            }
        }

        var user = new AppUser
        {
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password)
        };

        var membership = new UserTenantMembership
        {
            AppUser = user,
            TenantId = tenantId,
            Role = request.Role.Trim(),
            IsDefault = true,
            IsActive = true
        };

        db.UserTenantMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(user, membership));
    }

    [HttpPut("{appUserId:int}")]
    public async Task<ActionResult<TenantUserDto>> UpdateUser(int appUserId, [FromBody] UpdateTenantUserRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(TenantRoles.Admin))
        {
            return Forbid();
        }

        if (!IsValidRole(request.Role))
        {
            return ValidationProblem("Die Rolle ist ungueltig.");
        }

        var tenantId = await currentTenantAccessor.GetTenantIdAsync(cancellationToken);
        var membership = await db.UserTenantMemberships
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.AppUserId == appUserId, cancellationToken);

        if (membership is null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId == appUserId && (!request.IsActive || !string.Equals(request.Role, TenantRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationProblem("Der eigene Admin-Zugang kann nicht deaktiviert oder auf Mitarbeiter heruntergestuft werden.");
        }

        if (!request.IsActive && string.Equals(membership.Role, TenantRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var otherActiveAdmins = await db.UserTenantMemberships.CountAsync(x => x.TenantId == tenantId && x.AppUserId != appUserId && x.IsActive && x.Role == TenantRoles.Admin, cancellationToken);
            if (otherActiveAdmins == 0)
            {
                return ValidationProblem("Es muss mindestens ein aktiver Admin in der Firma vorhanden bleiben.");
            }
        }

        membership.AppUser.DisplayName = request.DisplayName.Trim();
        membership.Role = request.Role.Trim();
        membership.IsActive = request.IsActive;
        membership.AppUser.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            membership.AppUser.PasswordHash = passwordHashService.HashPassword(request.Password.Trim());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(membership.AppUser, membership));
    }

    private static bool IsValidRole(string role)
        => string.Equals(role, TenantRoles.Admin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, TenantRoles.Employee, StringComparison.OrdinalIgnoreCase);

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private static TenantUserDto ToDto(AppUser user, UserTenantMembership membership)
        => new()
        {
            AppUserId = user.AppUserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = membership.Role,
            IsActive = membership.IsActive && user.IsActive,
            IsDefault = membership.IsDefault,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
}
