using Trellis.Api.Modules.PropertyAsset.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IOwnerRepository
{
    Task AddAsync(Owner owner);

    Task<Owner?> GetByIdAsync(Guid ownerId);

    /// <summary>Keyset pagination ordered by (created_at, owner_id), scoped to the caller's tenant — mirrors ITenantRepository.ListAsync's shape.</summary>
    Task<(IReadOnlyList<Owner> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>
    /// True if this owner still has any non-archived property — blocks Archive() when true.
    /// Queries `property` directly (same PropertyDbContext, same module — not a cross-module
    /// boundary the "own your schema" rule applies to) rather than depending on
    /// IPropertyRepository.
    /// </summary>
    Task<bool> HasActivePropertiesAsync(Guid ownerId);

    string GetRowVersion(Owner owner);

    void SetExpectedRowVersion(Owner owner, string rowVersion);

    Task SaveChangesAsync();
}
