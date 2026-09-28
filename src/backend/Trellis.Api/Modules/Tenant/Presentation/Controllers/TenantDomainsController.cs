using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Presentation.Controllers;

/// <summary>
/// A tenant's own custom domains — the caller's own tenant via TenantContext, TENANT_ADMIN only
/// (DNS and public identity are an administrative decision). ENTERPRISE tier is enforced by the
/// service (TRL-E-TEN-2002), not by a role attribute, so the frontend can show an upsell.
/// </summary>
[ApiController]
[Route("api/v1/settings/domains")]
[ApiExplorerSettings(GroupName = "tenant")]
[Authorize(Roles = "TENANT_ADMIN")]
public sealed class TenantDomainsController(ITenantDomainService domainService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantDomainResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List() => (await domainService.ListAsync()).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] TenantDomainCreateRequest request) =>
        (await domainService.AddAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(Guid id) =>
        (await domainService.VerifyAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Remove(Guid id) =>
        (await domainService.RemoveAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
