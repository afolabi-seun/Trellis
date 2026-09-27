using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Application.Services;

public sealed class OverrideLogService(IOverrideLogRepository repository) : IOverrideLogService
{
    public async Task<ServiceResult<PagedResponse<OverrideLogResponse>>> ListAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await repository.ListAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].OverrideLogId) : null;

        return ServiceResult<PagedResponse<OverrideLogResponse>>.Success(
            new PagedResponse<OverrideLogResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    private static OverrideLogResponse ToResponse(OverrideLogEntry entry) => new(
        entry.OverrideLogId,
        entry.SuggestionType.ToUpperSnakeCase(),
        entry.SuggestionId,
        entry.OriginalValueJson,
        entry.CorrectedValueJson,
        entry.OverriddenBy,
        entry.CreatedAt);
}
