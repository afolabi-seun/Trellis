using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IRoutingSuggestionRepository
{
    Task AddAsync(RoutingSuggestion suggestion);

    Task<RoutingSuggestion?> GetByIdAsync(Guid routingSuggestionId);

    Task<(IReadOnlyList<RoutingSuggestion> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
