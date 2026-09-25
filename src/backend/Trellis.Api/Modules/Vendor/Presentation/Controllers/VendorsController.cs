using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;

namespace Trellis.Api.Modules.Vendor.Presentation.Controllers;

/// <summary>BDR-VEN-001 — onboarding is a Tenant Admin action; the platform-wide search lets a second, unrelated tenant find an already-onboarded vendor instead of duplicating it (BDR-VEN-004).</summary>
[ApiController]
[Route("api/v1/vendors")]
[ApiExplorerSettings(GroupName = "vendor")]
[Authorize(Roles = "TENANT_ADMIN")]
public sealed class VendorsController(IVendorService vendorService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<VendorResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await vendorService.SearchAsync(search, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<VendorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] VendorCreateRequest request) =>
        (await vendorService.CreateAsync(request)).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VendorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await vendorService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPost("{id:guid}/invite-user")]
    [ProducesResponseType(typeof(ApiResponse<VendorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteUser(Guid id, [FromBody] VendorInviteUserRequest request) =>
        (await vendorService.InviteUserAsync(id, request)).ToActionResult(HttpContext);
}
