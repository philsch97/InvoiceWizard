using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceWizard.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/sonepar")]
public class SoneparController(ICurrentTenantAccessor tenantAccessor, ISoneparService soneparService) : ControllerBase
{
    [HttpGet("connection")]
    public async Task<ActionResult<SoneparConnectionStatusDto>> GetConnection(CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        return Ok(await soneparService.GetStatusAsync(tenantId, cancellationToken));
    }

    [HttpPost("connection")]
    public async Task<ActionResult<SoneparConnectionStatusDto>> SaveConnection([FromBody] SaveSoneparConnectionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        return Ok(await soneparService.SaveConnectionAsync(tenantId, request, cancellationToken));
    }

    [HttpDelete("connection")]
    public async Task<IActionResult> DeleteConnection(CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        await soneparService.DeleteConnectionAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpPost("search")]
    public async Task<ActionResult<SoneparProductSearchResponse>> SearchProducts([FromBody] SoneparProductSearchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await tenantAccessor.GetTenantIdAsync(cancellationToken);
        return Ok(await soneparService.SearchProductsAsync(tenantId, request, cancellationToken));
    }
}
