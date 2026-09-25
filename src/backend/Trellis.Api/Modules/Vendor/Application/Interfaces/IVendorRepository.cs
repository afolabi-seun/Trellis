using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface IVendorRepository
{
    Task AddAsync(VendorRecord vendor);

    Task<VendorRecord?> GetByIdAsync(Guid vendorId);

    /// <summary>Platform-wide — vendor has no tenant_id to scope by, and BDR-VEN-004 depends on a second tenant being able to find an already-onboarded vendor by name.</summary>
    Task<(IReadOnlyList<VendorRecord> Items, bool HasMore, int TotalCount)> SearchAsync(
        string? nameFilter, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
