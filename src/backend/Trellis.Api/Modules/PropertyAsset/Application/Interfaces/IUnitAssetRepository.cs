using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IUnitAssetRepository
{
    Task AddAsync(UnitAsset unitAsset);

    Task<UnitAsset?> GetByIdAsync(Guid unitAssetId);

    /// <summary>Keyset pagination scoped to one property — units/assets are always listed within their parent property, never globally.</summary>
    Task<(IReadOnlyList<UnitAsset> Items, bool HasMore, int TotalCount)> ListByPropertyAsync(
        Guid propertyId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task<IReadOnlyList<Guid>> ListIdsByPropertiesAsync(IReadOnlyCollection<Guid> propertyIds);

    Task<IReadOnlyDictionary<Guid, Guid>> MapUnitsToPropertiesAsync(IReadOnlyCollection<Guid> propertyIds);

    /// <summary>billing_design.md's usage metering (BDR-BIL-001) — an explicit tenantId param, not Scoped()/TenantContext.Current, since the only caller (UsageSnapshotBackgroundService) runs under SystemContext with no ambient tenant during its cross-tenant batch poll.</summary>
    Task<int> CountActiveForTenantAsync(Guid tenantId);

    string GetRowVersion(UnitAsset unitAsset);

    void SetExpectedRowVersion(UnitAsset unitAsset, string rowVersion);

    Task SaveChangesAsync();
}
