using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Tenant.Presentation.Controllers;

/// <summary>
/// The caller's own tenant's settings. Any staff role may READ them (the forms need the tenant's default
/// currency); only a TENANT_ADMIN may change who is notified.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[ApiExplorerSettings(GroupName = "tenant")]
[Authorize(Roles = "TENANT_ADMIN,DEPARTMENT_STAFF")]
public sealed class SettingsController(ITenantSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TenantSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => (await settingsService.GetAsync()).ToActionResult(HttpContext);

    [HttpPut("notifications")]
    [Authorize(Roles = "TENANT_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<TenantSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationSettingsDto request) =>
        (await settingsService.UpdateNotificationsAsync(request, Guid.Parse(User.FindFirstValue("sub")!))).ToActionResult(HttpContext);
}
