using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Presentation.Filters;

namespace Trellis.Api.Modules.Ai.Presentation.Controllers;

/// <summary>BDR-AI-003/004 — a destination classification for a free-text request description, surfaced as a hint before the human creates the real WorkflowInstance through the existing, unchanged WorkflowInstancesController.</summary>
[ApiController]
[Route("api/v1/ai/routing-suggestions")]
[ApiExplorerSettings(GroupName = "ai")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
[RequiresProfessionalTier]
public sealed class AiRoutingSuggestionsController(IRoutingSuggestionService routingSuggestionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoutingSuggestionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] RoutingSuggestionCreateRequest request) =>
        (await routingSuggestionService.CreateAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RoutingSuggestionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await routingSuggestionService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoutingSuggestionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id) =>
        (await routingSuggestionService.GetByIdAsync(id)).ToActionResult(HttpContext);

    [HttpPatch("{id:guid}/link")]
    [ProducesResponseType(typeof(ApiResponse<RoutingSuggestionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Link(Guid id, [FromBody] RoutingSuggestionLinkRequest request) =>
        (await routingSuggestionService.LinkAsync(id, request, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
