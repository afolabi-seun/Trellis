using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Ai.Domain.Exceptions;

namespace Trellis.Api.Modules.Ai.Application.Services;

public sealed class ExtractionService(
    IExtractionJobRepository repository,
    IOverrideLogRepository overrideLogRepository,
    IAiProvider aiProvider,
    IFileStorage fileStorage,
    IAuditService auditService,
    IAiTierGate tierGate) : IExtractionService
{
    public async Task ExtractAsync(
        Guid tenantId, Guid documentId, Guid documentVersionId, string documentType, string storageKey, string fileName, Guid initiatedBy)
    {
        // A STARTER tenant's document upload must never even know Ai tiers exist — silent no-op,
        // not an error, not a row.
        if (!await tierGate.IsEntitledAsync(tenantId))
        {
            return;
        }

        var job = ExtractionJob.Create(tenantId, documentId, documentVersionId, documentType);
        await repository.AddAsync(job);
        await repository.SaveChangesAsync();

        try
        {
            await using var content = await fileStorage.OpenReadAsync(storageKey);
            var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                aiProvider.ExtractAsync(content, fileName, documentType));

            job.MarkExtracted(JsonSerializer.Serialize(ToDtos(result.Fields)), result.Confidence);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // error_handling.md's exit criterion: degrade to manual review, never throw past the
            // caller — the document upload this was triggered from must complete successfully
            // regardless of whether the AI provider is reachable.
            job.MarkManualReviewRequired(ex.Message);
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, initiatedBy, AuditActorType.System, AuditTypes.Ai.ExtractionCompleted,
            $"{{\"extractionJobId\":\"{job.ExtractionJobId}\",\"status\":\"{job.Status.ToUpperSnakeCase()}\"}}");
    }

    public async Task<ServiceResult<ExtractionJobResponse>> GetByIdAsync(Guid extractionJobId)
    {
        var job = await repository.GetByIdAsync(extractionJobId);
        return job is null
            ? ServiceResult<ExtractionJobResponse>.NotFound()
            : ServiceResult<ExtractionJobResponse>.Success(ToResponse(job));
    }

    public async Task<ServiceResult<PagedResponse<ExtractionJobResponse>>> ListAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await repository.ListAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].ExtractionJobId) : null;

        return ServiceResult<PagedResponse<ExtractionJobResponse>>.Success(
            new PagedResponse<ExtractionJobResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<ExtractionJobResponse>> ConfirmAsync(Guid extractionJobId, ExtractionConfirmRequest request, Guid reviewedBy)
    {
        var job = await repository.GetByIdAsync(extractionJobId);
        if (job is null)
        {
            return ServiceResult<ExtractionJobResponse>.NotFound();
        }

        var confirmedJson = JsonSerializer.Serialize(request.Fields);
        var changed = job.ExtractedFieldsJson != confirmedJson;

        try
        {
            job.Confirm(confirmedJson, reviewedBy);
            repository.SetExpectedRowVersion(job, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<ExtractionJobResponse>.BusinessRuleViolation(ErrorCodeRegistry.Ai.ExtractionAlreadyReviewed);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<ExtractionJobResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            job.TenantId, reviewedBy, AuditActorType.HumanUser, AuditTypes.Ai.ExtractionConfirmed,
            $"{{\"extractionJobId\":\"{job.ExtractionJobId}\"}}");

        if (changed)
        {
            var entry = OverrideLogEntry.Create(
                job.TenantId, SuggestionType.Extraction, job.ExtractionJobId, job.ExtractedFieldsJson ?? "[]", confirmedJson, reviewedBy);
            await overrideLogRepository.AddAsync(entry);
            await overrideLogRepository.SaveChangesAsync();

            await auditService.RecordAsync(
                job.TenantId, reviewedBy, AuditActorType.HumanUser, AuditTypes.Ai.SuggestionOverridden,
                $"{{\"suggestionType\":\"EXTRACTION\",\"suggestionId\":\"{job.ExtractionJobId}\"}}");
        }

        return ServiceResult<ExtractionJobResponse>.Success(ToResponse(job));
    }

    private static List<ExtractedFieldDto> ToDtos(IReadOnlyList<ExtractedField> fields) =>
        fields.Select(f => new ExtractedFieldDto(f.FieldName, f.Value)).ToList();

    private ExtractionJobResponse ToResponse(ExtractionJob job) => new(
        job.ExtractionJobId,
        job.DocumentId,
        job.DocumentVersionId,
        job.DocumentType,
        job.Status.ToUpperSnakeCase(),
        Deserialize(job.ExtractedFieldsJson),
        job.Confidence,
        Deserialize(job.ConfirmedFieldsJson),
        job.FailureReason,
        job.ReviewedBy,
        job.ReviewedAt,
        repository.GetRowVersion(job),
        job.CreatedAt);

    private static List<ExtractedFieldDto>? Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<ExtractedFieldDto>>(json);
}
