using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class SubscriptionRepository(BillingDbContext dbContext) : TenantAwareRepository<Subscription>, ISubscriptionRepository
{
    public async Task AddAsync(Subscription subscription) => await dbContext.Subscriptions.AddAsync(subscription);

    public Task<Subscription?> GetByIdAsync(Guid subscriptionId) =>
        dbContext.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

    // Not Scoped() — the only callers (UsageSnapshotBackgroundService, DunningRetryBackgroundService,
    // GetSummaryForTenantAsync) either run under SystemContext with no ambient TenantContext, or
    // already know the exact tenantId they're asking about.
    public Task<Subscription?> GetByTenantIdAsync(Guid tenantId) =>
        dbContext.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId);

    public async Task<(IReadOnlyList<Subscription> Items, bool HasMore, int TotalCount)> ListAllAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = dbContext.Subscriptions.AsQueryable();
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(s => s.CreatedAt > c.CreatedAt || (s.CreatedAt == c.CreatedAt && s.SubscriptionId > c.Id));
        }

        var page = await query.OrderBy(s => s.CreatedAt).ThenBy(s => s.SubscriptionId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public async Task<IReadOnlyList<Subscription>> ListPastDueAsync() =>
        await dbContext.Subscriptions.Where(s => s.Status == SubscriptionStatus.PastDue).ToListAsync();

    public async Task<IReadOnlyDictionary<SubscriptionStatus, int>> CountByStatusAsync()
    {
        var grouped = await dbContext.Subscriptions.GroupBy(s => s.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return grouped.ToDictionary(g => g.Key, g => g.Count);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
