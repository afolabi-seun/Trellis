using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Domain.Entities;
using Trellis.Api.Modules.Document.Domain.Exceptions;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.Document.Application.Services;

public sealed class DocumentService(
    IDocumentRepository repository,
    IDocumentVersionRepository versionRepository,
    IValidator<DocumentUploadRequest> uploadValidator,
    IFileStorage fileStorage,
    IFileScanner fileScanner,
    IAuditService auditService,
    IPropertyService propertyService,
    IUnitAssetService unitAssetService,
    IOwnerService ownerService) : IDocumentService
{
    public async Task<ServiceResult<DocumentResponse>> CreateAsync(
        DocumentUploadRequest request, Stream fileContent, string fileName, string declaredContentType, long fileSizeBytes, Guid uploadedBy)
    {
        var validation = await uploadValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ServiceResult<DocumentResponse>.ValidationFailed(
                validation.Errors.Select(e => new ValidationDetail(e.PropertyName, e.ErrorMessage)).ToList());
        }

        var targetCount = new[] { request.PropertyId, request.UnitAssetId, request.OwnerId }.Count(id => id is not null);
        if (targetCount > 1)
        {
            return ServiceResult<DocumentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Document.MultipleAttachmentTargets);
        }

        if (request.PropertyId is { } propertyId && (await propertyService.GetByIdAsync(propertyId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<DocumentResponse>.NotFound();
        }

        if (request.UnitAssetId is { } unitAssetId && (await unitAssetService.GetByIdAsync(unitAssetId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<DocumentResponse>.NotFound();
        }

        if (request.OwnerId is { } ownerId && (await ownerService.GetByIdAsync(ownerId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<DocumentResponse>.NotFound();
        }

        if (!await FileTypeValidator.IsAllowedDocumentTypeAsync(fileContent))
        {
            return ServiceResult<DocumentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Document.FileTypeNotAllowed);
        }

        var tenantId = TenantContext.Current!.TenantId;
        var documentType = EnumStringConverter.ParseUpperSnakeCase<DocumentType>(request.DocumentType);
        var document = DocumentRecord.Create(tenantId, request.PropertyId, request.UnitAssetId, request.OwnerId, documentType, request.ExpiresAt);

        var storageKey = await fileStorage.SaveAsync(fileContent, fileName);
        var version = DocumentVersion.Create(tenantId, document.DocumentId, 1, fileName, declaredContentType, fileSizeBytes, storageKey, uploadedBy);
        document.SetCurrentVersion(version.DocumentVersionId);

        await repository.AddAsync(document);
        await versionRepository.AddAsync(version);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, uploadedBy, AuditActorType.HumanUser, AuditTypes.Document.DocumentUploaded,
            $"{{\"documentId\":\"{document.DocumentId}\"}}");

        await ScanAsync(version);
        await versionRepository.SaveChangesAsync();

        return ServiceResult<DocumentResponse>.Success(await ToResponseAsync(document));
    }

    public async Task<ServiceResult<DocumentResponse>> GetByIdAsync(Guid documentId)
    {
        var document = await repository.GetByIdAsync(documentId);
        return document is null
            ? ServiceResult<DocumentResponse>.NotFound()
            : ServiceResult<DocumentResponse>.Success(await ToResponseAsync(document));
    }

    public async Task<ServiceResult<PagedResponse<DocumentResponse>>> ListAsync(string? cursor, int limit) =>
        await ToPagedResultAsync(await repository.ListAsync(CursorCodec.Decode(cursor), limit));

    public async Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByPropertyAsync(Guid propertyId, string? cursor, int limit)
    {
        if ((await propertyService.GetByIdAsync(propertyId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<PagedResponse<DocumentResponse>>.NotFound();
        }

        return await ToPagedResultAsync(await repository.ListByPropertyAsync(propertyId, CursorCodec.Decode(cursor), limit));
    }

    public async Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByUnitAssetAsync(Guid unitAssetId, string? cursor, int limit)
    {
        if ((await unitAssetService.GetByIdAsync(unitAssetId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<PagedResponse<DocumentResponse>>.NotFound();
        }

        return await ToPagedResultAsync(await repository.ListByUnitAssetAsync(unitAssetId, CursorCodec.Decode(cursor), limit));
    }

    public async Task<ServiceResult<PagedResponse<DocumentResponse>>> ListByOwnerAsync(Guid ownerId, string? cursor, int limit)
    {
        if ((await ownerService.GetByIdAsync(ownerId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<PagedResponse<DocumentResponse>>.NotFound();
        }

        return await ToPagedResultAsync(await repository.ListByOwnerAsync(ownerId, CursorCodec.Decode(cursor), limit));
    }

    public async Task<ServiceResult<DocumentResponse>> UpdateAsync(Guid documentId, DocumentUpdateRequest request)
    {
        var document = await repository.GetByIdAsync(documentId);
        if (document is null)
        {
            return ServiceResult<DocumentResponse>.NotFound();
        }

        document.Update(EnumStringConverter.ParseUpperSnakeCase<DocumentType>(request.DocumentType), request.ExpiresAt);

        try
        {
            repository.SetExpectedRowVersion(document, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<DocumentResponse>.ConcurrencyConflict();
        }

        return ServiceResult<DocumentResponse>.Success(await ToResponseAsync(document));
    }

    public async Task<ServiceResult<DocumentResponse>> ArchiveAsync(Guid documentId)
    {
        var document = await repository.GetByIdAsync(documentId);
        if (document is null)
        {
            return ServiceResult<DocumentResponse>.NotFound();
        }

        try
        {
            document.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<DocumentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Document.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<DocumentResponse>.Success(await ToResponseAsync(document));
    }

    public async Task<ServiceResult<DocumentDownloadResult>> DownloadAsync(Guid documentId, Guid callerUserId)
    {
        var document = await repository.GetByIdAsync(documentId);
        if (document?.CurrentVersionId is null)
        {
            return ServiceResult<DocumentDownloadResult>.NotFound();
        }

        var version = await versionRepository.GetByIdAsync(document.CurrentVersionId.Value);
        if (version is null)
        {
            return ServiceResult<DocumentDownloadResult>.NotFound();
        }

        // security_guide.md's one exception: invisible to everyone but the uploader until scan clears.
        if (version.ScanStatus != DocumentScanStatus.Clean && version.UploadedBy != callerUserId)
        {
            return ServiceResult<DocumentDownloadResult>.BusinessRuleViolation(ErrorCodeRegistry.Document.DocumentNotYetVisible);
        }

        var content = await fileStorage.OpenReadAsync(version.StorageKey);
        return ServiceResult<DocumentDownloadResult>.Success(new DocumentDownloadResult(content, version.FileName, version.ContentType));
    }

    private async Task ScanAsync(DocumentVersion version)
    {
        try
        {
            await using var scanStream = await fileStorage.OpenReadAsync(version.StorageKey);
            var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                fileScanner.ScanAsync(scanStream, version.FileName, version.ContentType));

            version.MarkScanResult(result.Outcome == ScanOutcome.Clean ? DocumentScanStatus.Clean : DocumentScanStatus.Infected);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // error_handling.md: fail-closed — stays SCAN_FAILED, invisible to everyone but the
            // uploader, retried on the same backoff policy as other external dependencies rather
            // than treated as clean.
            version.MarkScanResult(DocumentScanStatus.ScanFailed);
        }
    }

    private async Task<ServiceResult<PagedResponse<DocumentResponse>>> ToPagedResultAsync(
        (IReadOnlyList<DocumentRecord> Items, bool HasMore, int TotalCount) page)
    {
        var (items, hasMore, totalCount) = page;
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].DocumentId) : null;
        var responses = await Task.WhenAll(items.Select(ToResponseAsync));

        return ServiceResult<PagedResponse<DocumentResponse>>.Success(
            new PagedResponse<DocumentResponse>(responses.ToList(), nextCursor, hasMore, totalCount));
    }

    private async Task<DocumentResponse> ToResponseAsync(DocumentRecord document)
    {
        DocumentVersionResponse? currentVersion = null;
        if (document.CurrentVersionId is { } versionId)
        {
            var version = await versionRepository.GetByIdAsync(versionId);
            if (version is not null)
            {
                currentVersion = new DocumentVersionResponse(
                    version.DocumentVersionId, version.VersionNumber, version.FileName, version.ContentType,
                    version.FileSizeBytes, version.ScanStatus.ToUpperSnakeCase(), version.CreatedAt);
            }
        }

        return new DocumentResponse(
            document.DocumentId,
            document.PropertyId,
            document.UnitAssetId,
            document.OwnerId,
            document.DocumentType.ToUpperSnakeCase(),
            document.ExpiresAt,
            document.Status.ToString().ToUpperInvariant(),
            currentVersion,
            repository.GetRowVersion(document),
            document.CreatedAt);
    }
}
