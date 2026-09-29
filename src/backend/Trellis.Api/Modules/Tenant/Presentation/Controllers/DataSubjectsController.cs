using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Presentation.Controllers;

/// <summary>
/// security_guide.md's POPIA data-subject export/erasure — TENANT_ADMIN only, the caller's own
/// tenant only. Both actions are rate-limited on the same policy as login: erasure is
/// irreversible and password-gated, and export hands back a full PII bundle in one response, so
/// neither belongs unthrottled.
/// </summary>
[ApiController]
[Route("api/v1/data-subjects")]
[ApiExplorerSettings(GroupName = "tenant")]
[Authorize(Roles = "TENANT_ADMIN")]
[EnableRateLimiting(AuthModuleExtensions.AuthSensitiveRateLimitPolicy)]
public sealed class DataSubjectsController(IDataSubjectService dataSubjectService) : ControllerBase
{
    [HttpPost("export")]
    [ProducesResponseType(typeof(ApiResponse<DataSubjectExportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromBody] DataSubjectExportRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        return (await dataSubjectService.ExportAsync(request, CurrentUserId())).ToActionResult(HttpContext);
    }

    [HttpPost("erase")]
    [ProducesResponseType(typeof(ApiResponse<DataSubjectErasureResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Erase([FromBody] EraseDataSubjectRequest request) =>
        (await dataSubjectService.EraseAsync(request, CurrentUserId())).ToActionResult(HttpContext);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);
}
