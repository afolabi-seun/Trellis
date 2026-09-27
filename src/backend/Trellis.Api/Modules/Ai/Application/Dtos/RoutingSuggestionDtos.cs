namespace Trellis.Api.Modules.Ai.Application.Dtos;

/// <summary>BDR-AI-003's own literal text — "based on request description," a field WorkflowInstanceCreateRequest never had (decision 6: this is a standalone surface, not a change to that request).</summary>
public sealed record RoutingSuggestionCreateRequest(string Description, Guid? PropertyId);

public sealed record RoutingSuggestionResponse(
    Guid RoutingSuggestionId,
    string Description,
    Guid? PropertyId,
    Guid? SuggestedDepartmentId,
    string? SuggestedVendorCategory,
    decimal? Confidence,
    string Status,
    Guid? FinalDepartmentId,
    Guid? WorkflowInstanceId,
    DateTimeOffset CreatedAt);

/// <summary>Called once the human has created the real WorkflowInstance elsewhere — records which department was actually chosen, resolving Accepted/Overridden.</summary>
public sealed record RoutingSuggestionLinkRequest(Guid WorkflowInstanceId, Guid? FinalDepartmentId);
