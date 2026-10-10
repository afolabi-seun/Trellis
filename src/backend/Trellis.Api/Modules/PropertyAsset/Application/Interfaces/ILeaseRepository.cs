using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface ILeaseRepository
{
    Task AddAsync(Lease lease);

    Task<Lease?> GetByIdAsync(Guid leaseId);

    Task<Lease?> GetByLeaseApplicationIdAsync(Guid leaseApplicationId);

    Task<Lease?> GetActiveForUnitAsync(Guid unitAssetId);

    /// <summary>BDR-LSE-009 — active fixed-term leases ending on or before <paramref name="endingOnOrBefore"/>, including any whose end date has already passed, soonest first.</summary>
    Task<IReadOnlyList<Lease>> ListActiveFixedEndingOnOrBeforeAsync(DateOnly endingOnOrBefore);

    Task<IReadOnlyList<Lease>> ListActiveWithTermNotRecordedAsync();

    string GetRowVersion(Lease lease);

    void SetExpectedRowVersion(Lease lease, string rowVersion);

    Task SaveChangesAsync();
}
