using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Data;

public interface ICurrentTenantAccessor
{
    Task<int> GetTenantIdAsync(CancellationToken cancellationToken = default);
}

public class CurrentTenantAccessor(IHttpContextAccessor httpContextAccessor, InvoiceWizardDbContext db) : ICurrentTenantAccessor
{
    public async Task<int> GetTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var claimValue = user?.FindFirstValue("tenant_id") ?? user?.FindFirstValue(ClaimTypes.GroupSid) ?? user?.FindFirstValue(JwtRegisteredClaimNames.Aud);
        if (int.TryParse(claimValue, out var tenantId))
        {
            return tenantId;
        }

        var tenantIds = await db.Tenants.Where(x => x.IsActive)
            .OrderBy(x => x.TenantId)
            .Select(x => x.TenantId)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 1)
        {
            return tenantIds[0];
        }

        throw new InvalidOperationException("Kein Mandant konnte aus dem aktuellen Kontext bestimmt werden.");
    }
}
