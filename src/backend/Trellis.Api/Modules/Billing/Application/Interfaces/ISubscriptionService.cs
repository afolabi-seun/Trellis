using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface ISubscriptionService
{
    Task<ServiceResult<SubscriptionSummaryResponse>> GetSummaryForTenantAsync(Guid tenantId);

    /// <summary>UsageSnapshotBackgroundService's bootstrap step — creates a Trialing subscription if the tenant doesn't have one yet. Returns the domain entity, not a Response — the job needs to act on it further within the same poll iteration.</summary>
    Task<Subscription> GetOrCreateForTenantAsync(Guid tenantId, string countryCode, DateTimeOffset tenantCreatedAt);

    Task<ServiceResult<PagedResponse<SubscriptionResponse>>> ListAllAsync(string? cursor, int limit);

    Task<ServiceResult<PlatformRevenueSummaryResponse>> GetPlatformRevenueSummaryAsync();
}
