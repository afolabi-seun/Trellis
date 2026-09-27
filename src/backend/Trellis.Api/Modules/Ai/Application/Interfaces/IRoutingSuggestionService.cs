using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IRoutingSuggestionService
{
    /// <summary>BDR-AI-003 — classifies free text against the tenant's own active departments. Never throws past the caller; a classifier failure marks the row Failed instead.</summary>
    Task<ServiceResult<RoutingSuggestionResponse>> CreateAsync(RoutingSuggestionCreateRequest request, Guid createdBy);

    Task<ServiceResult<RoutingSuggestionResponse>> GetByIdAsync(Guid routingSuggestionId);

    Task<ServiceResult<PagedResponse<RoutingSuggestionResponse>>> ListAsync(string? cursor, int limit);

    /// <summary>Called once the human has created the real WorkflowInstance elsewhere — resolves Accepted/Overridden and writes an override_log row iff the final department differed from the suggestion.</summary>
    Task<ServiceResult<RoutingSuggestionResponse>> LinkAsync(Guid routingSuggestionId, RoutingSuggestionLinkRequest request, Guid linkedBy);
}
