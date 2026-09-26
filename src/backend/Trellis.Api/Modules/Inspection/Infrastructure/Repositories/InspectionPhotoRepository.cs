using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Repositories;

public sealed class InspectionPhotoRepository(InspectionDbContext dbContext) : IInspectionPhotoRepository
{
    public async Task AddAsync(InspectionPhoto photo) => await dbContext.InspectionPhotos.AddAsync(photo);

    // Not Scoped() — same dual-caller reasoning as InspectionItemRepository/
    // DocumentVersionRepository; inspection_photo's own tenant_or_vendor_isolation policy is what
    // actually restricts rows for either caller shape.
    public Task<InspectionPhoto?> GetByIdAsync(Guid inspectionPhotoId) =>
        dbContext.InspectionPhotos.FirstOrDefaultAsync(p => p.InspectionPhotoId == inspectionPhotoId);

    public async Task<IReadOnlyList<InspectionPhoto>> ListByInspectionAsync(Guid inspectionId) =>
        await dbContext.InspectionPhotos
            .Where(p => p.InspectionId == inspectionId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
