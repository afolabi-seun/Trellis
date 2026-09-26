using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Repositories;

public sealed class InspectionItemRepository(InspectionDbContext dbContext) : IInspectionItemRepository
{
    public async Task AddAsync(InspectionItem item) => await dbContext.InspectionItems.AddAsync(item);

    public void Remove(InspectionItem item) => dbContext.InspectionItems.Remove(item);

    // Not Scoped() — reachable from both a staff and a vendor caller (inspection_item's own
    // tenant_or_vendor_isolation policy is what actually restricts rows), same reasoning
    // DocumentVersionRepository.GetByIdAsync already established.
    public Task<InspectionItem?> GetByIdAsync(Guid inspectionItemId) =>
        dbContext.InspectionItems.FirstOrDefaultAsync(i => i.InspectionItemId == inspectionItemId);

    public async Task<IReadOnlyList<InspectionItem>> ListByInspectionAsync(Guid inspectionId) =>
        await dbContext.InspectionItems
            .Where(i => i.InspectionId == inspectionId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
