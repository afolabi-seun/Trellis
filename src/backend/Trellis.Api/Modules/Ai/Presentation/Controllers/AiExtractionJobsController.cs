using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Presentation.Filters;
using Trellis.Api.Modules.Billing.Presentation.Filters;

namespace Trellis.Api.Modules.Ai.Presentation.Controllers;

/// <summary>BDR-AI-001/002 — the extraction review queue. Rows are created inline by DocumentService, never directly by a client.</summary>
[ApiController]
[Route("api/v1/ai/extraction-jobs")]
[ApiExplorerSettings(GroupName = "ai")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresProfessionalTier]
[RequiresModuleEntitlement("AI")]
public sealed class AiExtractionJobsController(IExtractionService extractionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ExtractionJobResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await extractionService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ExtractionJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await extractionService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ExtractionJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ExtractionConfirmRequest request) =>
        (await extractionService.ConfirmAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
