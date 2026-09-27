namespace Trellis.Api.Modules.Ai.Application.Dtos;

public sealed record OverrideLogResponse(
    Guid OverrideLogId,
    string SuggestionType,
    Guid SuggestionId,
    string OriginalValueJson,
    string CorrectedValueJson,
    Guid OverriddenBy,
    DateTimeOffset CreatedAt);
