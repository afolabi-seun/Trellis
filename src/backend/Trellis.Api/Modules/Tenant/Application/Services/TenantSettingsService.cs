using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;

namespace Trellis.Api.Modules.Tenant.Application.Services;

/// <summary>
/// A tenant's own settings, stored in <c>tenant.config_json</c>. Concurrent edits are caught by the
/// row's xmin concurrency token (a second admin saving over a stale read gets a conflict, not a
/// silent overwrite).
/// </summary>
public sealed class TenantSettingsService(ITenantRepository repository, IAuditService auditService) : ITenantSettingsService
{
    public async Task<ServiceResult<TenantSettingsResponse>> GetAsync()
    {
        var tenant = await repository.GetByIdAsync(TenantContext.Current!.TenantId);
        return tenant is null
            ? ServiceResult<TenantSettingsResponse>.NotFound()
            : ServiceResult<TenantSettingsResponse>.Success(ToResponse(tenant.CountryCode, tenant.ConfigJson));
    }

    public async Task<ServiceResult<TenantSettingsResponse>> UpdateNotificationsAsync(NotificationSettingsDto request, Guid actorUserId)
    {
        var tenant = await repository.GetByIdAsync(TenantContext.Current!.TenantId);
        if (tenant is null)
        {
            return ServiceResult<TenantSettingsResponse>.NotFound();
        }

        var settings = new NotificationRecipientSettings(request.ApplicationSubmittedRoles, request.RentArrearsRoles);
        tenant.SetConfigJson(TenantSettingsSerializer.WriteNotifications(tenant.ConfigJson, settings));

        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<TenantSettingsResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            tenant.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.TenantSettingsUpdated,
            $"{{\"applicationSubmittedRoles\":[{string.Join(',', request.ApplicationSubmittedRoles.Select(r => $"\"{r}\""))}]," +
            $"\"rentArrearsRoles\":[{string.Join(',', request.RentArrearsRoles.Select(r => $"\"{r}\""))}]}}");

        return ServiceResult<TenantSettingsResponse>.Success(ToResponse(tenant.CountryCode, tenant.ConfigJson));
    }

    public async Task<NotificationSettingsDto> GetNotificationSettingsAsync(Guid tenantId)
    {
        var tenant = await repository.GetByIdAsync(tenantId);
        var settings = TenantSettingsSerializer.ReadNotifications(tenant?.ConfigJson);
        return new NotificationSettingsDto(settings.ApplicationSubmittedRoles, settings.RentArrearsRoles);
    }

    private static TenantSettingsResponse ToResponse(string countryCode, string? configJson)
    {
        var notifications = TenantSettingsSerializer.ReadNotifications(configJson);
        return new TenantSettingsResponse(
            CurrencyDefaults.ForCountry(countryCode), countryCode,
            new NotificationSettingsDto(notifications.ApplicationSubmittedRoles, notifications.RentArrearsRoles));
    }
}
