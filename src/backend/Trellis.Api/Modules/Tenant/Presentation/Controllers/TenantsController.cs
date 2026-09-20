using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Presentation.Controllers;

/// <summary>
/// api_standards.md's "Tenant Management Endpoints (Platform)" — the `/platform/` prefix, not
/// `/api/v1/`: these are cross-tenant by nature, `SUPER_ADMIN` only, with no "caller's own
/// tenant" to scope to.
/// </summary>
[ApiController]
[Route("platform/tenants")]
[ApiExplorerSettings(GroupName = "tenant")]
[Authorize(Roles = "SUPER_ADMIN")]
public sealed class TenantsController(ITenantService tenantService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await tenantService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    public async Task<IActionResult> Provision([FromBody] TenantProvisionRequest request) =>
        (await tenantService.ProvisionTenantAsync(request)).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) =>
        (await tenantService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/offboard")]
    public async Task<IActionResult> Offboard(Guid id) =>
        (await tenantService.OffboardTenantAsync(id)).ToActionResult(HttpContext);
}
