using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.PropertyAsset.Presentation.Controllers;

/// <summary>Tenant-scoped (not /platform/) — api_standards.md's prefix rule: these are the caller's own tenant's records, resolved from their JWT.</summary>
[ApiController]
[Route("api/v1/owners")]
[ApiExplorerSettings(GroupName = "propertyasset")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class OwnersController(IOwnerService ownerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<OwnerResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await ownerService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] OwnerCreateRequest request) =>
        (await ownerService.CreateAsync(request)).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await ownerService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] OwnerUpdateRequest request) =>
        (await ownerService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — see OwnerService.ArchiveAsync.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await ownerService.ArchiveAsync(id)).ToActionResult(HttpContext);
}
