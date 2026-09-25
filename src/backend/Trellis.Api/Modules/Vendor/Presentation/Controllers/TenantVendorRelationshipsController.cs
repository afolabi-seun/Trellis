using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;

namespace Trellis.Api.Modules.Vendor.Presentation.Controllers;

/// <summary>BDR-VEN-006 — a Tenant Admin's own engagement lifecycle with a vendor; VendorService.CreateAsync already creates the caller's first relationship, this covers every one after.</summary>
[ApiController]
[Route("api/v1/tenant-vendor-relationships")]
[ApiExplorerSettings(GroupName = "vendor")]
[Authorize(Roles = "TENANT_ADMIN")]
public sealed class TenantVendorRelationshipsController(ITenantVendorRelationshipService relationshipService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<TenantVendorRelationshipResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await relationshipService.ListForTenantAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantVendorRelationshipResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromQuery] Guid vendorId) =>
        (await relationshipService.CreateAsync(vendorId)).ToActionResult(HttpContext);

    /// <summary>Gates only new VendorAssignment rows for this tenant+vendor pair going forward (BDR-VEN-006) — never touches assignments that already exist.</summary>
    [HttpPost("{id:guid}/terminate")]
    [ProducesResponseType(typeof(ApiResponse<TenantVendorRelationshipResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Terminate(Guid id) =>
        (await relationshipService.TerminateAsync(id, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
