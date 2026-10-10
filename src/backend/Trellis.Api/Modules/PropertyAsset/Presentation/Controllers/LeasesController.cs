using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.PropertyAsset.Presentation.Controllers;

/// <summary>
/// Lease records (BDR-LSE-009/011) — Core, so no module entitlement: a tenant with only Leasing or
/// only Rent Collection still has leases. There is deliberately no create or end route here: a
/// lease starts when Leasing executes one or Rent Collection starts a schedule, and ends through
/// those same modules, which own the rest of what ending a tenancy involves.
/// </summary>
[ApiController]
[Route("api/v1/leases")]
[ApiExplorerSettings(GroupName = "propertyasset")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class LeasesController(ILeaseService leaseService) : ControllerBase
{
    /// <summary>BDR-LSE-009 — active fixed-term leases ending within withinDays (1–365, default 90), plus any already past their end date, soonest first.</summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaseResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExpiring([FromQuery] int withinDays = 90) =>
        (await leaseService.ListExpiringAsync(Math.Clamp(withinDays, 1, 365))).ToActionResult(HttpContext);

    /// <summary>Active leases that predate term capture — the list staff work through to record each real term.</summary>
    [HttpGet("term-not-recorded")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LeaseResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTermNotRecorded() =>
        (await leaseService.ListTermNotRecordedAsync()).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LeaseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await leaseService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPut("{id:guid}/term")]
    [ProducesResponseType(typeof(ApiResponse<LeaseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordTerm(Guid id, [FromBody] LeaseTermRequest request) =>
        (await leaseService.RecordTermAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
