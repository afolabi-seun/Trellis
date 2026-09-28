using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IUsageSnapshotRepository
{
    Task AddAsync(UsageSnapshot snapshot);

    /// <summary>GET /api/v1/billing/subscription's "current usage" figure — the most recently captured snapshot, or null before the first one.</summary>
    Task<UsageSnapshot?> GetLatestForTenantAsync(Guid tenantId);

    Task SaveChangesAsync();
}
