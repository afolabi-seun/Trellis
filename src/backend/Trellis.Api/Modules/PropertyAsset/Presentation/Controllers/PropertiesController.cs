using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.PropertyAsset.Presentation.Controllers;

[ApiController]
[Route("api/v1/properties")]
[ApiExplorerSettings(GroupName = "propertyasset")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await propertyService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] PropertyCreateRequest request) =>
        (await propertyService.CreateAsync(request)).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await propertyService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PropertyUpdateRequest request) =>
        (await propertyService.UpdateAsync(id, request)).ToActionResult(HttpContext);

    /// <summary>Archives, never a hard delete — see PropertyService.ArchiveAsync.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id) =>
        (await propertyService.ArchiveAsync(id)).ToActionResult(HttpContext);
}
