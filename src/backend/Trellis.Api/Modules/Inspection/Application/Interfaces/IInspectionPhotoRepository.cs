using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Application.Interfaces;

public interface IInspectionPhotoRepository
{
    Task AddAsync(InspectionPhoto photo);

    /// <summary>Not tenant-scoped — reachable from both a staff and a vendor caller; inspection_photo's own tenant_or_vendor_isolation policy (layer 1) is what actually restricts rows.</summary>
    Task<InspectionPhoto?> GetByIdAsync(Guid inspectionPhotoId);

    Task<IReadOnlyList<InspectionPhoto>> ListByInspectionAsync(Guid inspectionId);

    Task SaveChangesAsync();
}
