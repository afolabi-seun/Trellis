using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IOverrideLogRepository
{
    Task AddAsync(OverrideLogEntry entry);

    Task<(IReadOnlyList<OverrideLogEntry> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task SaveChangesAsync();
}
