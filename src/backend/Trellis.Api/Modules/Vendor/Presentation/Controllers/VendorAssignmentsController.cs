using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;

namespace Trellis.Api.Modules.Vendor.Presentation.Controllers;

/// <summary>
/// Tenant-side — creating and browsing this tenant's own assignments. The vendor-side view
/// (a vendor's own cross-tenant list/detail/actions) lives in
/// VendorPortalAssignmentsController under a distinct route prefix — the two can't share a
/// controller since [Authorize(Roles=...)] at class and action level ANDs together rather than
/// ORing, and a shared "{id}" route shape between "assignment PK" (tenant side) and "work item
/// id" (vendor side, matching VendorAssignedToWorkItemAttribute's own param) would collide.
/// </summary>
[ApiController]
[Route("api/v1/vendor-assignments")]
[ApiExplorerSettings(GroupName = "vendor")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class VendorAssignmentsController(IVendorAssignmentService assignmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<VendorAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await assignmentService.ListForTenantAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<VendorAssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] VendorAssignmentCreateRequest request) =>
        (await assignmentService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VendorAssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await assignmentService.GetByIdAsync(id)).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
