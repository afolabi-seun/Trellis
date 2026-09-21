using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IUnitAssetRepository
{
    Task AddAsync(UnitAsset unitAsset);

    Task<UnitAsset?> GetByIdAsync(Guid unitAssetId);

    /// <summary>Keyset pagination scoped to one property — units/assets are always listed within their parent property, never globally.</summary>
    Task<(IReadOnlyList<UnitAsset> Items, bool HasMore, int TotalCount)> ListByPropertyAsync(
        Guid propertyId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(UnitAsset unitAsset);

    void SetExpectedRowVersion(UnitAsset unitAsset, string rowVersion);

    Task SaveChangesAsync();
}
