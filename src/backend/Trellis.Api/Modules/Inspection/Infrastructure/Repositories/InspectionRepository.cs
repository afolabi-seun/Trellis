using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.Inspection.Infrastructure.Data;

namespace Trellis.Api.Modules.Inspection.Infrastructure.Repositories;

public sealed class InspectionRepository(InspectionDbContext dbContext) : TenantAwareRepository<InspectionRecord>, IInspectionRepository
{
    public async Task AddAsync(InspectionRecord inspection) => await dbContext.Inspections.AddAsync(inspection);

    public Task<InspectionRecord?> GetByIdAsync(Guid inspectionId) =>
        Scoped(dbContext.Inspections).FirstOrDefaultAsync(i => i.InspectionId == inspectionId);

    public Task<InspectionRecord?> GetForVendorAsync(Guid inspectionId) =>
        dbContext.Inspections.FirstOrDefaultAsync(i => i.InspectionId == inspectionId);

    public async Task<(IReadOnlyList<InspectionRecord> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.Inspections);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(i => i.CreatedAt > c.CreatedAt || (i.CreatedAt == c.CreatedAt && i.InspectionId > c.Id));
        }

        var page = await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.InspectionId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public string GetRowVersion(InspectionRecord inspection) => RowVersionHelper.Get(dbContext, inspection);

    public void SetExpectedRowVersion(InspectionRecord inspection, string rowVersion) => RowVersionHelper.SetExpected(dbContext, inspection, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
