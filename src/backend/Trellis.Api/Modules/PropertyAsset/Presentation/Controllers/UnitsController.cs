using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.PropertyAsset.Presentation.Controllers;

/// <summary>
/// No class-level [Route] — a unit/asset is always created/listed within its parent property
/// (BDR-AST-001's hierarchy), but read/updated/archived by its own flat id, so the two action
/// groups below carry their own full route templates instead of sharing one base.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "propertyasset")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class UnitsController(IUnitAssetService unitAssetService) : ControllerBase
{
    [HttpGet("api/v1/properties/{propertyId:guid}/units")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<UnitAssetResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByProperty(Guid propertyId, [FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await unitAssetService.ListByPropertyAsync(propertyId, cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost("api/v1/properties/{propertyId:guid}/units")]
    [ProducesResponseType(typeof(ApiResponse<UnitAssetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid propertyId, [FromBody] UnitAssetCreateRequest request) =>
        (await unitAssetService.CreateAsync(propertyId, request)).ToActionResult(HttpContext);

    [HttpGet("api/v1/units/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UnitAssetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await unitAssetService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("api/v1/units/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UnitAssetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UnitAssetUpdateRequest request) =>
        (await unitAssetService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — see UnitAssetService.ArchiveAsync.</summary>
    [HttpDelete("api/v1/units/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UnitAssetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await unitAssetService.ArchiveAsync(id)).ToActionResult(HttpContext);
}
