using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Infrastructure.Data;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Repositories;

public sealed class OwnerReportRepository(ReportingDbContext dbContext) : TenantAwareRepository<OwnerReport>, IOwnerReportRepository
{
    public async Task AddAsync(OwnerReport report) => await dbContext.OwnerReports.AddAsync(report);

    public Task<OwnerReport?> GetByIdAsync(Guid ownerReportId) =>
        Scoped(dbContext.OwnerReports).FirstOrDefaultAsync(r => r.OwnerReportId == ownerReportId);

    public Task<OwnerReport?> GetByIdForTenantAsync(Guid tenantId, Guid ownerReportId) =>
        dbContext.OwnerReports.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.OwnerReportId == ownerReportId);

    public async Task<(IReadOnlyList<OwnerReport> Items, bool HasMore, int TotalCount)> ListAsync(
        Guid? ownerId, (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.OwnerReports);
        if (ownerId is { } o)
        {
            baseQuery = baseQuery.Where(r => r.OwnerId == o);
        }

        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(r => r.CreatedAt > c.CreatedAt || (r.CreatedAt == c.CreatedAt && r.OwnerReportId > c.Id));
        }

        var page = await query.OrderBy(r => r.CreatedAt).ThenBy(r => r.OwnerReportId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public async Task<IReadOnlyList<OwnerReport>> ListAllForOwnerAsync(Guid ownerId) =>
        await Scoped(dbContext.OwnerReports).Where(r => r.OwnerId == ownerId).ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
