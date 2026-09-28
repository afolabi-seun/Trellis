using System.Text.Json;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Ai.Domain.Exceptions;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Ai.Application.Services;

public sealed class RoutingSuggestionService(
    IRoutingSuggestionRepository repository,
    IOverrideLogRepository overrideLogRepository,
    IAiProvider aiProvider,
    IDepartmentService departmentService,
    IAuditService auditService,
    IAiTierGate tierGate) : IRoutingSuggestionService
{
    public async Task<ServiceResult<RoutingSuggestionResponse>> CreateAsync(RoutingSuggestionCreateRequest request, Guid createdBy)
    {
        var tenantId = TenantContext.Current!.TenantId;

        if (!await tierGate.IsEntitledAsync(tenantId))
        {
            return ServiceResult<RoutingSuggestionResponse>.Forbidden(ErrorCodeRegistry.Ai.TierNotEntitled);
        }

        var suggestion = RoutingSuggestion.Create(tenantId, request.Description, request.PropertyId, createdBy);
        await repository.AddAsync(suggestion);
        await repository.SaveChangesAsync();

        var departments = await departmentService.ListAsync(null, 100);
        var candidates = (departments.Data?.Items ?? [])
            .Where(d => d.Status == "ACTIVE")
            .Select(d => new DepartmentCandidate(d.DepartmentId, d.Name))
            .ToList();

        try
        {
            var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                aiProvider.ClassifyRoutingAsync(request.Description, candidates));

            suggestion.MarkClassified(result.SuggestedDepartmentId, result.SuggestedVendorCategory, result.Confidence);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // error_handling.md's exit criterion: the suggestion simply carries no destination —
            // the human still creates the WorkflowInstance through the unchanged, existing flow.
            suggestion.MarkFailed();
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, createdBy, AuditActorType.System, AuditTypes.Ai.RoutingSuggestionCreated,
            $"{{\"routingSuggestionId\":\"{suggestion.RoutingSuggestionId}\"}}");

        return ServiceResult<RoutingSuggestionResponse>.Success(ToResponse(suggestion));
    }

    public async Task<ServiceResult<RoutingSuggestionResponse>> GetByIdAsync(Guid routingSuggestionId)
    {
        var suggestion = await repository.GetByIdAsync(routingSuggestionId);
        return suggestion is null
            ? ServiceResult<RoutingSuggestionResponse>.NotFound()
            : ServiceResult<RoutingSuggestionResponse>.Success(ToResponse(suggestion));
    }

    public async Task<ServiceResult<PagedResponse<RoutingSuggestionResponse>>> ListAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await repository.ListAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].RoutingSuggestionId) : null;

        return ServiceResult<PagedResponse<RoutingSuggestionResponse>>.Success(
            new PagedResponse<RoutingSuggestionResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<RoutingSuggestionResponse>> LinkAsync(Guid routingSuggestionId, RoutingSuggestionLinkRequest request, Guid linkedBy)
    {
        var suggestion = await repository.GetByIdAsync(routingSuggestionId);
        if (suggestion is null)
        {
            return ServiceResult<RoutingSuggestionResponse>.NotFound();
        }

        var originalDepartmentId = suggestion.SuggestedDepartmentId;

        bool wasOverridden;
        try
        {
            wasOverridden = suggestion.Link(request.WorkflowInstanceId, request.FinalDepartmentId);
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<RoutingSuggestionResponse>.BusinessRuleViolation(ErrorCodeRegistry.Ai.RoutingSuggestionAlreadyLinked);
        }

        await auditService.RecordAsync(
            suggestion.TenantId, linkedBy, AuditActorType.HumanUser, AuditTypes.Ai.RoutingSuggestionLinked,
            $"{{\"routingSuggestionId\":\"{suggestion.RoutingSuggestionId}\",\"workflowInstanceId\":\"{suggestion.WorkflowInstanceId}\"}}");

        if (wasOverridden)
        {
            var entry = OverrideLogEntry.Create(
                suggestion.TenantId, SuggestionType.Routing, suggestion.RoutingSuggestionId,
                JsonSerializer.Serialize(new { departmentId = originalDepartmentId }),
                JsonSerializer.Serialize(new { departmentId = suggestion.FinalDepartmentId }),
                linkedBy);
            await overrideLogRepository.AddAsync(entry);
            await overrideLogRepository.SaveChangesAsync();

            await auditService.RecordAsync(
                suggestion.TenantId, linkedBy, AuditActorType.HumanUser, AuditTypes.Ai.SuggestionOverridden,
                $"{{\"suggestionType\":\"ROUTING\",\"suggestionId\":\"{suggestion.RoutingSuggestionId}\"}}");
        }

        return ServiceResult<RoutingSuggestionResponse>.Success(ToResponse(suggestion));
    }

    private static RoutingSuggestionResponse ToResponse(RoutingSuggestion suggestion) => new(
        suggestion.RoutingSuggestionId,
        suggestion.Description,
        suggestion.PropertyId,
        suggestion.SuggestedDepartmentId,
        suggestion.SuggestedVendorCategory,
        suggestion.Confidence,
        suggestion.Status.ToUpperSnakeCase(),
        suggestion.FinalDepartmentId,
        suggestion.WorkflowInstanceId,
        suggestion.CreatedAt);
}
