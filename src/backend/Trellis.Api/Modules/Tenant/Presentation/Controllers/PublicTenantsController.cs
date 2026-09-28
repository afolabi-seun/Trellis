using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Presentation.Controllers;

/// <summary>
/// What an anonymous visitor may learn about a tenant: its display name and slug — never its id
/// (BDR-SEC-004: no tenant-identifying internals in public responses). Every failure, including a
/// suspended tenant or an unverified domain, is the same plain 404.
/// </summary>
[ApiController]
[Route("api/v1/public")]
[ApiExplorerSettings(GroupName = "tenant")]
[AllowAnonymous]
public sealed class PublicTenantsController(ITenantService tenantService, ITenantDomainService domainService) : ControllerBase
{
    [HttpGet("tenants/{tenantSlug}")]
    [ProducesResponseType(typeof(ApiResponse<PublicTenantResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySlug(string tenantSlug) => ToPublic(await tenantService.ResolveSlugAsync(tenantSlug));

    /// <summary>For a visitor on a tenant's own domain: which tenant's listings does this host serve?</summary>
    [HttpGet("domain-resolution")]
    [ProducesResponseType(typeof(ApiResponse<PublicTenantResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveHost([FromQuery] string host) => ToPublic(await domainService.ResolveHostAsync(host));

    private IActionResult ToPublic(ServiceResult<TenantPublicInfo> result) =>
        result.Outcome == ServiceResultOutcome.Success
            ? ServiceResult<PublicTenantResponse>.Success(new PublicTenantResponse(result.Data!.Name, result.Data.Slug)).ToActionResult(HttpContext)
            : ServiceResult<PublicTenantResponse>.NotFound().ToActionResult(HttpContext);
}
