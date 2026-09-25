using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface ITenantVendorRelationshipRepository
{
    Task AddAsync(TenantVendorRelationship relationship);

    Task<TenantVendorRelationship?> GetByIdAsync(Guid tenantVendorRelationshipId);

    Task<TenantVendorRelationship?> GetByTenantAndVendorAsync(Guid tenantId, Guid vendorId);

    Task<(IReadOnlyList<TenantVendorRelationship> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
