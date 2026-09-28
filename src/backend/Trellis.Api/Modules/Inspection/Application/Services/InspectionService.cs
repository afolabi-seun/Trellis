using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.Inspection.Domain.Exceptions;
using Trellis.Api.Modules.Inspection.Domain.ValueObjects;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Application.Services;

public sealed class InspectionService(
    IInspectionRepository repository,
    IInspectionItemRepository itemRepository,
    IInspectionPhotoRepository photoRepository,
    IFileStorage fileStorage,
    IFileScanner fileScanner,
    IAuditService auditService,
    IPropertyService propertyService,
    IUnitAssetService unitAssetService,
    IVendorAssignmentReader vendorAssignmentReader) : IInspectionService
{
    public async Task<ServiceResult<InspectionReportResponse>> ScheduleAsync(InspectionCreateRequest request, Guid scheduledBy)
    {
        if (request.PropertyId is { } propertyId && (await propertyService.GetByIdAsync(propertyId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<InspectionReportResponse>.NotFound();
        }

        if (request.UnitAssetId is { } unitAssetId && (await unitAssetService.GetByIdAsync(unitAssetId)).Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<InspectionReportResponse>.NotFound();
        }

        var tenantId = TenantContext.Current!.TenantId;
        var inspection = Domain.Entities.InspectionRecord.Create(
            tenantId, request.PropertyId, request.UnitAssetId, request.AssignedToUserId, scheduledBy, request.ScheduledAt);

        await repository.AddAsync(inspection);

        var order = 0;
        foreach (var label in InspectionChecklistDefaults.BaselineLabels)
        {
            await itemRepository.AddAsync(InspectionItem.Create(tenantId, inspection.InspectionId, label, order++));
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, scheduledBy, AuditActorType.HumanUser, AuditTypes.Inspection.InspectionScheduled,
            $"{{\"inspectionId\":\"{inspection.InspectionId}\"}}");

        return ServiceResult<InspectionReportResponse>.Success(await ToReportResponseAsync(inspection));
    }

    public async Task<ServiceResult<InspectionReportResponse>> GetByIdAsync(Guid inspectionId)
    {
        var inspection = await repository.GetByIdAsync(inspectionId);
        return inspection is null
            ? ServiceResult<InspectionReportResponse>.NotFound()
            : ServiceResult<InspectionReportResponse>.Success(await ToReportResponseAsync(inspection));
    }

    public async Task<ServiceResult<PagedResponse<InspectionReportResponse>>> ListForTenantAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await repository.ListAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].InspectionId) : null;

        // Sequential, not Task.WhenAll — ToReportResponseAsync shares this request's single scoped
        // InspectionDbContext instance across itemRepository/photoRepository, same concurrency
        // hazard already found and fixed in Document's/Vendor's own list-mapping methods.
        var responses = new List<InspectionReportResponse>();
        foreach (var item in items)
        {
            responses.Add(await ToReportResponseAsync(item));
        }

        return ServiceResult<PagedResponse<InspectionReportResponse>>.Success(
            new PagedResponse<InspectionReportResponse>(responses, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<InspectionItemResponse>> AddItemAsync(Guid inspectionId, InspectionItemAddRequest request)
    {
        var inspection = await repository.GetByIdAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<InspectionItemResponse>.NotFound();
        }

        var existingCount = (await itemRepository.ListByInspectionAsync(inspectionId)).Count;
        var item = InspectionItem.Create(inspection.TenantId, inspectionId, request.Label, existingCount);

        await itemRepository.AddAsync(item);
        await itemRepository.SaveChangesAsync();

        return ServiceResult<InspectionItemResponse>.Success(ToItemResponse(item));
    }

    public async Task<ServiceResult<InspectionItemResponse>> UpdateItemAsync(Guid inspectionId, Guid inspectionItemId, InspectionItemUpdateRequest request)
    {
        var item = await itemRepository.GetByIdAsync(inspectionItemId);
        if (item is null || item.InspectionId != inspectionId)
        {
            return ServiceResult<InspectionItemResponse>.NotFound();
        }

        var status = EnumStringConverter.ParseUpperSnakeCase<InspectionItemStatus>(request.Status);
        item.Update(status, request.Notes);
        await itemRepository.SaveChangesAsync();

        return ServiceResult<InspectionItemResponse>.Success(ToItemResponse(item));
    }

    public async Task<ServiceResult<bool>> RemoveItemAsync(Guid inspectionId, Guid inspectionItemId)
    {
        var item = await itemRepository.GetByIdAsync(inspectionItemId);
        if (item is null || item.InspectionId != inspectionId)
        {
            return ServiceResult<bool>.NotFound();
        }

        itemRepository.Remove(item);
        await itemRepository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<InspectionPhotoResponse>> UploadPhotoAsync(
        Guid inspectionId, Guid? inspectionItemId, Stream content, string fileName, string contentType, long fileSizeBytes,
        decimal? latitude, decimal? longitude, DateTimeOffset capturedAt, Guid uploadedBy)
    {
        var inspection = await repository.GetByIdAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<InspectionPhotoResponse>.NotFound();
        }

        var photo = await CreateAndScanPhotoAsync(
            inspection.TenantId, inspectionId, inspectionItemId, content, fileName, contentType, fileSizeBytes,
            latitude, longitude, capturedAt, uploadedBy);

        return photo is null
            ? ServiceResult<InspectionPhotoResponse>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.FileTypeNotAllowed)
            : ServiceResult<InspectionPhotoResponse>.Success(ToPhotoResponse(photo));
    }

    public async Task<ServiceResult<InspectionPhotoDownloadResult>> DownloadPhotoAsync(Guid inspectionPhotoId, Guid callerUserId)
    {
        var photo = await photoRepository.GetByIdAsync(inspectionPhotoId);
        if (photo is null)
        {
            return ServiceResult<InspectionPhotoDownloadResult>.NotFound();
        }

        // security_guide.md's one exception: invisible to everyone but the uploader until scan clears.
        if (photo.ScanStatus != InspectionPhotoScanStatus.Clean && photo.UploadedBy != callerUserId)
        {
            return ServiceResult<InspectionPhotoDownloadResult>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.PhotoNotYetVisible);
        }

        var content = await fileStorage.OpenReadAsync(photo.StorageKey);
        return ServiceResult<InspectionPhotoDownloadResult>.Success(new InspectionPhotoDownloadResult(content, photo.FileName, photo.ContentType));
    }

    public async Task<ServiceResult<InspectionReportResponse>> StartAsync(Guid inspectionId, Guid actorId)
    {
        var inspection = await repository.GetByIdAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<InspectionReportResponse>.NotFound();
        }

        try
        {
            inspection.MarkInProgress();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<InspectionReportResponse>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<InspectionReportResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            inspection.TenantId, actorId, AuditActorType.HumanUser, AuditTypes.Inspection.InspectionStatusUpdated,
            $"{{\"inspectionId\":\"{inspection.InspectionId}\",\"status\":\"IN_PROGRESS\"}}");

        return ServiceResult<InspectionReportResponse>.Success(await ToReportResponseAsync(inspection));
    }

    public async Task<ServiceResult<InspectionReportResponse>> CompleteAsync(Guid inspectionId, Guid actorId, decimal? latitude, decimal? longitude)
    {
        var inspection = await repository.GetByIdAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<InspectionReportResponse>.NotFound();
        }

        try
        {
            inspection.Complete(latitude, longitude);
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<InspectionReportResponse>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<InspectionReportResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            inspection.TenantId, actorId, AuditActorType.HumanUser, AuditTypes.Inspection.InspectionStatusUpdated,
            $"{{\"inspectionId\":\"{inspection.InspectionId}\",\"status\":\"CLOSED\"}}");

        return ServiceResult<InspectionReportResponse>.Success(await ToReportResponseAsync(inspection));
    }

    public async Task<ServiceResult<VendorInspectionView>> GetForVendorAsync(Guid vendorId, Guid inspectionId)
    {
        // Layer 2 check before the query, not just relied on via RLS alone — same belt+braces
        // every vendor-facing read in this codebase already has (WorkflowInstanceService.GetForVendorAsync).
        if (!await vendorAssignmentReader.IsAssignedAsync(vendorId, WorkItemTypes.Inspection, inspectionId))
        {
            return ServiceResult<VendorInspectionView>.NotFound();
        }

        var inspection = await repository.GetForVendorAsync(inspectionId);
        return inspection is null
            ? ServiceResult<VendorInspectionView>.NotFound()
            : ServiceResult<VendorInspectionView>.Success(await ToVendorViewAsync(inspection));
    }

    public async Task<ServiceResult<VendorInspectionView>> StartForVendorAsync(Guid vendorId, Guid inspectionId)
    {
        if (!await vendorAssignmentReader.IsAssignedAsync(vendorId, WorkItemTypes.Inspection, inspectionId))
        {
            return ServiceResult<VendorInspectionView>.NotFound();
        }

        var inspection = await repository.GetForVendorAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<VendorInspectionView>.NotFound();
        }

        try
        {
            inspection.MarkInProgress();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VendorInspectionView>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<VendorInspectionView>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            inspection.TenantId, vendorId, AuditActorType.VendorUser, AuditTypes.Inspection.InspectionStatusUpdated,
            $"{{\"inspectionId\":\"{inspection.InspectionId}\",\"status\":\"IN_PROGRESS\"}}");

        return ServiceResult<VendorInspectionView>.Success(await ToVendorViewAsync(inspection));
    }

    public async Task<ServiceResult<VendorInspectionView>> CompleteFromVendorAsync(
        Guid vendorId, Guid inspectionId, InspectionCompleteRequest request, Guid uploadedBy)
    {
        if (!await vendorAssignmentReader.IsAssignedAsync(vendorId, WorkItemTypes.Inspection, inspectionId))
        {
            return ServiceResult<VendorInspectionView>.NotFound();
        }

        var inspection = await repository.GetForVendorAsync(inspectionId);
        if (inspection is null)
        {
            return ServiceResult<VendorInspectionView>.NotFound();
        }

        foreach (var itemUpdate in request.Items)
        {
            var item = await itemRepository.GetByIdAsync(itemUpdate.InspectionItemId);
            if (item is null || item.InspectionId != inspectionId
                || !EnumStringConverter.TryParseUpperSnakeCase<InspectionItemStatus>(itemUpdate.Status, out var status))
            {
                continue;
            }

            item.Update(status, itemUpdate.Notes);
        }

        await itemRepository.SaveChangesAsync();

        // A photo whose type fails validation is skipped, not a reason to fail the whole
        // completion — the exit criterion's retry-safety is about network flakiness, not per-photo
        // client bugs, and skipping here keeps a valid-but-partial submission from getting
        // permanently stuck behind a cached idempotency failure (decision 8's own caveat: only a
        // fresh idempotency key re-attempts a genuinely different payload).
        foreach (var upload in request.Photos)
        {
            await CreateAndScanPhotoAsync(
                inspection.TenantId, inspectionId, upload.InspectionItemId, upload.Content, upload.FileName,
                upload.ContentType, upload.Length, upload.Latitude, upload.Longitude, upload.CapturedAt, uploadedBy);
        }

        try
        {
            inspection.Complete(request.Location?.Latitude, request.Location?.Longitude);
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VendorInspectionView>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<VendorInspectionView>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            inspection.TenantId, vendorId, AuditActorType.VendorUser, AuditTypes.Inspection.InspectionStatusUpdated,
            $"{{\"inspectionId\":\"{inspection.InspectionId}\",\"status\":\"CLOSED\"}}");

        return ServiceResult<VendorInspectionView>.Success(await ToVendorViewAsync(inspection));
    }

    private async Task<InspectionPhoto?> CreateAndScanPhotoAsync(
        Guid tenantId, Guid inspectionId, Guid? inspectionItemId, Stream content, string fileName, string contentType,
        long fileSizeBytes, decimal? latitude, decimal? longitude, DateTimeOffset capturedAt, Guid uploadedBy)
    {
        if (!await FileTypeValidator.IsAllowedInspectionPhotoTypeAsync(content))
        {
            return null;
        }

        var storageKey = await fileStorage.SaveAsync(content, fileName);
        var photo = InspectionPhoto.Create(
            tenantId, inspectionId, inspectionItemId, fileName, contentType, fileSizeBytes, storageKey, latitude, longitude, capturedAt, uploadedBy);

        await photoRepository.AddAsync(photo);
        await photoRepository.SaveChangesAsync();

        await ScanAsync(photo);
        await photoRepository.SaveChangesAsync();

        return photo;
    }

    private async Task ScanAsync(InspectionPhoto photo)
    {
        try
        {
            await using var scanStream = await fileStorage.OpenReadAsync(photo.StorageKey);
            var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                fileScanner.ScanAsync(scanStream, photo.FileName, photo.ContentType));

            photo.MarkScanResult(result.Outcome == ScanOutcome.Clean ? InspectionPhotoScanStatus.Clean : InspectionPhotoScanStatus.Infected);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            photo.MarkScanResult(InspectionPhotoScanStatus.ScanFailed);
        }
    }

    private static InspectionItemResponse ToItemResponse(InspectionItem item) => new(
        item.InspectionItemId, item.Label, item.Status.ToUpperSnakeCase(), item.Notes, item.SortOrder);

    private static InspectionPhotoResponse ToPhotoResponse(InspectionPhoto photo) => new(
        photo.InspectionPhotoId, photo.InspectionItemId, photo.FileName, photo.ContentType, photo.FileSizeBytes,
        photo.ScanStatus.ToUpperSnakeCase(), photo.Latitude, photo.Longitude, photo.CapturedAt, photo.CreatedAt);

    private async Task<InspectionReportResponse> ToReportResponseAsync(Domain.Entities.InspectionRecord inspection)
    {
        var items = await itemRepository.ListByInspectionAsync(inspection.InspectionId);
        var photos = await photoRepository.ListByInspectionAsync(inspection.InspectionId);

        return new InspectionReportResponse(
            inspection.InspectionId,
            inspection.PropertyId,
            inspection.UnitAssetId,
            inspection.AssignedToUserId,
            inspection.Status.ToUpperSnakeCase(),
            inspection.ScheduledAt,
            inspection.StartedAt,
            inspection.CompletedAt,
            inspection.Latitude,
            inspection.Longitude,
            items.Select(ToItemResponse).ToList(),
            photos.Select(ToPhotoResponse).ToList(),
            repository.GetRowVersion(inspection),
            inspection.CreatedAt);
    }

    private async Task<VendorInspectionView> ToVendorViewAsync(Domain.Entities.InspectionRecord inspection)
    {
        var items = await itemRepository.ListByInspectionAsync(inspection.InspectionId);
        var photos = await photoRepository.ListByInspectionAsync(inspection.InspectionId);

        return new VendorInspectionView(
            inspection.InspectionId,
            inspection.PropertyId,
            inspection.UnitAssetId,
            inspection.Status.ToUpperSnakeCase(),
            inspection.ScheduledAt,
            inspection.StartedAt,
            inspection.Latitude,
            inspection.Longitude,
            items.Select(ToItemResponse).ToList(),
            photos.Select(ToPhotoResponse).ToList(),
            inspection.CreatedAt);
    }

    public Task<int> CountUpcomingAsync(int lookaheadDays)
    {
        var now = DateTimeOffset.UtcNow;
        return repository.CountUpcomingAsync(now, now.AddDays(lookaheadDays));
    }
}
