using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

public interface ITenantSettingsService
{
    /// <summary>The caller's own tenant (TenantContext.Current) — what staff may read.</summary>
    Task<ServiceResult<TenantSettingsResponse>> GetAsync();

    /// <summary>TENANT_ADMIN. Replaces both recipient lists; other keys already stored in the tenant's config are preserved.</summary>
    Task<ServiceResult<TenantSettingsResponse>> UpdateNotificationsAsync(NotificationSettingsDto request, Guid actorUserId);

    /// <summary>
    /// Explicit tenantId, no ambient context — the arrears job runs under SystemContext across every tenant.
    /// A tenant with nothing configured (or unreadable config) gets the defaults, so an alert is never silenced by absence.
    /// </summary>
    Task<NotificationSettingsDto> GetNotificationSettingsAsync(Guid tenantId);
}
