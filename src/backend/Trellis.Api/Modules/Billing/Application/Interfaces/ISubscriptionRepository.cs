using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task AddAsync(Subscription subscription);

    Task<Subscription?> GetByIdAsync(Guid subscriptionId);

    /// <summary>Runs under SystemContext (UsageSnapshotBackgroundService/DunningRetryBackgroundService) — an explicit tenantId param, not TenantContext.Current, since there's no ambient tenant during a cross-tenant batch poll.</summary>
    Task<Subscription?> GetByTenantIdAsync(Guid tenantId);

    /// <summary>Platform-wide list (GET /platform/billing/subscriptions) — no tenant scoping, SUPER_ADMIN only.</summary>
    Task<(IReadOnlyList<Subscription> Items, bool HasMore, int TotalCount)> ListAllAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>DunningRetryBackgroundService's own poll target — every subscription currently PAST_DUE, across every tenant.</summary>
    Task<IReadOnlyList<Subscription>> ListPastDueAsync();

    /// <summary>BDR-BIL-007's status breakdown (GET /platform/billing/summary).</summary>
    Task<IReadOnlyDictionary<SubscriptionStatus, int>> CountByStatusAsync();

    Task SaveChangesAsync();
}
