using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IPropertyRepository
{
    Task AddAsync(Property property);

    Task<Property?> GetByIdAsync(Guid propertyId);

    Task<(IReadOnlyList<Property> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>True if this property still has any non-archived unit/asset — blocks Archive() when true.</summary>
    Task<bool> HasActiveUnitAssetsAsync(Guid propertyId);

    /// <summary>Reporting's Owner report (BDR-OWN-002) — every property this Owner holds, tenant-scoped via Scoped(). Includes archived properties: a report's period may predate an archival, and excluding them would silently drop real maintenance history.</summary>
    Task<IReadOnlyList<Guid>> ListIdsByOwnerAsync(Guid ownerId);

    string GetRowVersion(Property property);

    void SetExpectedRowVersion(Property property, string rowVersion);

    Task SaveChangesAsync();
}
