using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Ai.Infrastructure.Data;

namespace Trellis.Api.Modules.Ai.Infrastructure.Repositories;

public sealed class ExtractionJobRepository(AiDbContext dbContext) : TenantAwareRepository<ExtractionJob>, IExtractionJobRepository
{
    public async Task AddAsync(ExtractionJob job) => await dbContext.ExtractionJobs.AddAsync(job);

    public Task<ExtractionJob?> GetByIdAsync(Guid extractionJobId) =>
        Scoped(dbContext.ExtractionJobs).FirstOrDefaultAsync(j => j.ExtractionJobId == extractionJobId);

    public async Task<(IReadOnlyList<ExtractionJob> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.ExtractionJobs);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(j => j.CreatedAt > c.CreatedAt || (j.CreatedAt == c.CreatedAt && j.ExtractionJobId > c.Id));
        }

        var page = await query.OrderBy(j => j.CreatedAt).ThenBy(j => j.ExtractionJobId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public string GetRowVersion(ExtractionJob job) => RowVersionHelper.Get(dbContext, job);

    public void SetExpectedRowVersion(ExtractionJob job, string rowVersion) => RowVersionHelper.SetExpected(dbContext, job, rowVersion);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
