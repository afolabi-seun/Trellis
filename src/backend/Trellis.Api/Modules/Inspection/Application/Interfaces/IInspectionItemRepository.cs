using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Application.Interfaces;

public interface IInspectionItemRepository
{
    Task AddAsync(InspectionItem item);

    void Remove(InspectionItem item);

    /// <summary>Not tenant-scoped — reachable from both a staff and a vendor caller; inspection_item's own tenant_or_vendor_isolation policy (layer 1) is what actually restricts rows.</summary>
    Task<InspectionItem?> GetByIdAsync(Guid inspectionItemId);

    Task<IReadOnlyList<InspectionItem>> ListByInspectionAsync(Guid inspectionId);

    Task SaveChangesAsync();
}
