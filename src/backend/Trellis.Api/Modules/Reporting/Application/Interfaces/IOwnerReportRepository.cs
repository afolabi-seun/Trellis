using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

public interface IOwnerReportRepository
{
    Task AddAsync(OwnerReport report);

    /// <summary>The normal, authenticated path — Scoped() to TenantContext.Current.</summary>
    Task<OwnerReport?> GetByIdAsync(Guid ownerReportId);

    /// <summary>
    /// The anonymous share-link resolution path only — takes an explicit tenantId rather than
    /// using Scoped()/TenantContext.Current, exact precedent of PaymentMethodRepository's own
    /// GetDefaultAsync(Guid tenantId) (Billing, used by DunningService under SystemContext).
    /// Wrapping the whole share-link request in SystemContext.Enter() and also trying to "switch
    /// into" TenantContext.Current mid-request would be redundant (IsSystem already satisfies RLS
    /// for the whole block) and a latent NullReferenceException risk if TenantContext.Current is
    /// never actually set — this method sidesteps that by never depending on it at all.
    /// </summary>
    Task<OwnerReport?> GetByIdForTenantAsync(Guid tenantId, Guid ownerReportId);

    Task<(IReadOnlyList<OwnerReport> Items, bool HasMore, int TotalCount)> ListAsync(
        Guid? ownerId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>POPIA erasure — every report this owner has, unpaged (an owner's own report count is bounded by their tenancy, never large enough to need paging for this one-off operation).</summary>
    Task<IReadOnlyList<OwnerReport>> ListAllForOwnerAsync(Guid ownerId);

    Task SaveChangesAsync();
}
