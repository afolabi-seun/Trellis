using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Ai.Infrastructure.Data;

namespace Trellis.Api.Modules.Ai.Infrastructure.Repositories;

public sealed class OverrideLogRepository(AiDbContext dbContext) : TenantAwareRepository<OverrideLogEntry>, IOverrideLogRepository
{
    public async Task AddAsync(OverrideLogEntry entry) => await dbContext.OverrideLogEntries.AddAsync(entry);

    public async Task<(IReadOnlyList<OverrideLogEntry> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.OverrideLogEntries);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(e => e.CreatedAt > c.CreatedAt || (e.CreatedAt == c.CreatedAt && e.OverrideLogId > c.Id));
        }

        var page = await query.OrderBy(e => e.CreatedAt).ThenBy(e => e.OverrideLogId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
