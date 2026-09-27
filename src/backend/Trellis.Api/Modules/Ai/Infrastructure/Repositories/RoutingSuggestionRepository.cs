using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Ai.Infrastructure.Data;

namespace Trellis.Api.Modules.Ai.Infrastructure.Repositories;

public sealed class RoutingSuggestionRepository(AiDbContext dbContext) : TenantAwareRepository<RoutingSuggestion>, IRoutingSuggestionRepository
{
    public async Task AddAsync(RoutingSuggestion suggestion) => await dbContext.RoutingSuggestions.AddAsync(suggestion);

    public Task<RoutingSuggestion?> GetByIdAsync(Guid routingSuggestionId) =>
        Scoped(dbContext.RoutingSuggestions).FirstOrDefaultAsync(r => r.RoutingSuggestionId == routingSuggestionId);

    public async Task<(IReadOnlyList<RoutingSuggestion> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.RoutingSuggestions);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(r => r.CreatedAt > c.CreatedAt || (r.CreatedAt == c.CreatedAt && r.RoutingSuggestionId > c.Id));
        }

        var page = await query.OrderBy(r => r.CreatedAt).ThenBy(r => r.RoutingSuggestionId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
