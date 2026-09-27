using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Presentation.Filters;

namespace Trellis.Api.Modules.Ai.Presentation.Controllers;

/// <summary>BDR-AI-004's "log the override for future model improvement" — read-only, TENANT_ADMIN only.</summary>
[ApiController]
[Route("api/v1/ai/override-log")]
[ApiExplorerSettings(GroupName = "ai")]
[Authorize(Roles = "TENANT_ADMIN")]
[RequiresProfessionalTier]
public sealed class AiOverrideLogController(IOverrideLogService overrideLogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<OverrideLogResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 25) =>
        (await overrideLogService.ListAsync(cursor, Math.Clamp(limit, 1, 100))).ToActionResult(HttpContext);
}
