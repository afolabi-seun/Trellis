namespace Trellis.Api.Core.Dtos;

/// <summary>api_standards.md's cursor pagination pattern — the `data` payload of a paginated `ApiResponse&lt;T&gt;`.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore, int TotalCount);
