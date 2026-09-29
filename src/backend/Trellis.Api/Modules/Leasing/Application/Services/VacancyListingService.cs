using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Resilience;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Configuration;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// The public (anonymous) methods here take an explicit tenantId from the route and set
/// TenantContext.Current to it before touching anything — deliberately, and only for that tenant's
/// intentionally-public listing data: IUnitAssetService.GetByIdAsync (a cross-module call for the
/// unit's label) and Billing's entitlement row both sit behind tenant_isolation RLS and
/// Scoped(), which an anonymous request has no context for otherwise. Same reasoning as
/// LeasingWebhookService's own documented TenantContext set — the cross-module services being
/// called are ambient-TenantContext-shaped by existing, unchanged design.
/// </summary>
public sealed class VacancyListingService(
    IVacancyListingRepository repository,
    IVacancyListingPhotoRepository photoRepository,
    IUnitAssetService unitAssetService,
    IModuleEntitlementService moduleEntitlementService,
    IFileStorage fileStorage,
    IFileScanner fileScanner,
    IAuditService auditService,
    IOptions<LeasingOptions> options) : IVacancyListingService
{
    private const string LeasingModuleKey = "LEASING";

    public async Task<ServiceResult<VacancyListingResponse>> CreateAsync(VacancyListingCreateRequest request, Guid createdBy)
    {
        var unit = await unitAssetService.GetByIdAsync(request.UnitAssetId);
        if (unit.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<VacancyListingResponse>.NotFound();
        }

        // An occupied unit has a live lease — a fresh listing must wait until EndLeaseAsync frees it.
        if (unit.Data!.OccupancyStatus == "OCCUPIED")
        {
            return ServiceResult<VacancyListingResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.UnitNotVacant);
        }

        if (await repository.HasActiveListingForUnitAsync(request.UnitAssetId))
        {
            return ServiceResult<VacancyListingResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.UnitAssetAlreadyListed);
        }

        var tenantId = TenantContext.Current!.TenantId;
        var listing = VacancyListing.Create(
            tenantId, request.UnitAssetId, request.RentAmount, request.Currency, request.AvailableFrom, request.Description, createdBy);

        await repository.AddAsync(listing);

        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race against the partial unique index (the pre-check above isn't atomic).
            return ServiceResult<VacancyListingResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.UnitAssetAlreadyListed);
        }

        await auditService.RecordAsync(
            tenantId, createdBy, AuditActorType.HumanUser, AuditTypes.Leasing.VacancyListingCreated,
            $"{{\"vacancyListingId\":\"{listing.VacancyListingId}\"}}");

        return ServiceResult<VacancyListingResponse>.Success(await ToResponseAsync(listing));
    }

    public async Task<ServiceResult<VacancyListingResponse>> UpdateAsync(Guid vacancyListingId, VacancyListingUpdateRequest request)
    {
        var listing = await repository.GetByIdAsync(vacancyListingId);
        if (listing is null)
        {
            return ServiceResult<VacancyListingResponse>.NotFound();
        }

        try
        {
            listing.Update(request.RentAmount, request.Currency, request.AvailableFrom, request.Description);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VacancyListingResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        try
        {
            repository.SetExpectedRowVersion(listing, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<VacancyListingResponse>.ConcurrencyConflict();
        }

        return ServiceResult<VacancyListingResponse>.Success(await ToResponseAsync(listing));
    }

    public async Task<ServiceResult<VacancyListingResponse>> WithdrawAsync(Guid vacancyListingId)
    {
        var listing = await repository.GetByIdAsync(vacancyListingId);
        if (listing is null)
        {
            return ServiceResult<VacancyListingResponse>.NotFound();
        }

        try
        {
            listing.Withdraw();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VacancyListingResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            listing.TenantId, listing.CreatedBy, AuditActorType.HumanUser, AuditTypes.Leasing.VacancyListingWithdrawn,
            $"{{\"vacancyListingId\":\"{listing.VacancyListingId}\"}}");

        return ServiceResult<VacancyListingResponse>.Success(await ToResponseAsync(listing));
    }

    public async Task<ServiceResult<PagedResponse<VacancyListingResponse>>> ListForTenantAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await repository.ListForTenantAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].VacancyListingId) : null;

        // Sequential, not Task.WhenAll — ToResponseAsync shares this request's single scoped
        // LeasingDbContext across the photo repository (same hazard InspectionService documents).
        var responses = new List<VacancyListingResponse>();
        foreach (var item in items)
        {
            responses.Add(await ToResponseAsync(item));
        }

        return ServiceResult<PagedResponse<VacancyListingResponse>>.Success(
            new PagedResponse<VacancyListingResponse>(responses, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<VacancyListingResponse>> GetByIdAsync(Guid vacancyListingId)
    {
        var listing = await repository.GetByIdAsync(vacancyListingId);
        return listing is null
            ? ServiceResult<VacancyListingResponse>.NotFound()
            : ServiceResult<VacancyListingResponse>.Success(await ToResponseAsync(listing));
    }

    public async Task<ServiceResult<VacancyListingPhotoResponse>> UploadPhotoAsync(
        Guid vacancyListingId, Stream content, string fileName, string contentType, long fileSizeBytes, Guid uploadedBy)
    {
        var listing = await repository.GetByIdAsync(vacancyListingId);
        if (listing is null)
        {
            return ServiceResult<VacancyListingPhotoResponse>.NotFound();
        }

        var existingCount = await photoRepository.CountByListingAsync(vacancyListingId);
        if (existingCount >= options.Value.MaxPhotosPerListing
            || !await FileTypeValidator.IsAllowedInspectionPhotoTypeAsync(content))
        {
            return ServiceResult<VacancyListingPhotoResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.PhotoRejected);
        }

        var storageKey = await fileStorage.SaveAsync(content, fileName);
        var photo = VacancyListingPhoto.Create(
            listing.TenantId, vacancyListingId, fileName, contentType, fileSizeBytes, storageKey, existingCount, uploadedBy);

        await photoRepository.AddAsync(photo);
        await photoRepository.SaveChangesAsync();

        await ScanAsync(photo);
        await photoRepository.SaveChangesAsync();

        return ServiceResult<VacancyListingPhotoResponse>.Success(ToPhotoResponse(photo));
    }

    public async Task<ServiceResult<VacancyListingPhotoDownloadResult>> DownloadPhotoAsync(Guid photoId)
    {
        var photo = await photoRepository.GetByIdAsync(photoId);
        if (photo is null)
        {
            return ServiceResult<VacancyListingPhotoDownloadResult>.NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(photo.StorageKey);
        return ServiceResult<VacancyListingPhotoDownloadResult>.Success(
            new VacancyListingPhotoDownloadResult(stream, photo.FileName, photo.ContentType));
    }

    public async Task<ServiceResult<PagedResponse<PublicVacancyListingResponse>>> BrowsePublicAsync(Guid tenantId, string? cursor, int limit)
    {
        TenantContext.Current = new TenantContext(tenantId);
        if (!await IsPublicEntitledAsync(tenantId))
        {
            return ServiceResult<PagedResponse<PublicVacancyListingResponse>>.NotFound();
        }

        var (items, hasMore, totalCount) = await repository.ListPublicForTenantAsync(tenantId, CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].VacancyListingId) : null;

        var responses = new List<PublicVacancyListingResponse>();
        foreach (var item in items)
        {
            var label = await UnitLabelAsync(item.UnitAssetId);
            responses.Add(new PublicVacancyListingResponse(
                item.VacancyListingId, label, item.RentAmount, item.Currency, item.AvailableFrom, item.Description));
        }

        return ServiceResult<PagedResponse<PublicVacancyListingResponse>>.Success(
            new PagedResponse<PublicVacancyListingResponse>(responses, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<PublicVacancyListingDetailResponse>> GetPublicByIdAsync(Guid tenantId, Guid vacancyListingId)
    {
        TenantContext.Current = new TenantContext(tenantId);
        if (!await IsPublicEntitledAsync(tenantId))
        {
            return ServiceResult<PublicVacancyListingDetailResponse>.NotFound();
        }

        var listing = await repository.GetByIdForTenantAsync(tenantId, vacancyListingId);
        if (listing is null || !listing.AcceptsApplications())
        {
            return ServiceResult<PublicVacancyListingDetailResponse>.NotFound();
        }

        var photos = await photoRepository.ListPublicByListingAsync(tenantId, vacancyListingId);
        return ServiceResult<PublicVacancyListingDetailResponse>.Success(new PublicVacancyListingDetailResponse(
            listing.VacancyListingId, await UnitLabelAsync(listing.UnitAssetId), listing.RentAmount, listing.Currency,
            listing.AvailableFrom, listing.Description, photos.Select(p => p.VacancyListingPhotoId).ToList()));
    }

    public async Task<ServiceResult<VacancyListingPhotoDownloadResult>> GetPublicPhotoAsync(Guid tenantId, Guid photoId)
    {
        TenantContext.Current = new TenantContext(tenantId);
        if (!await IsPublicEntitledAsync(tenantId))
        {
            return ServiceResult<VacancyListingPhotoDownloadResult>.NotFound();
        }

        var photo = await photoRepository.GetPublicByIdAsync(tenantId, photoId);
        if (photo is null)
        {
            return ServiceResult<VacancyListingPhotoDownloadResult>.NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(photo.StorageKey);
        return ServiceResult<VacancyListingPhotoDownloadResult>.Success(
            new VacancyListingPhotoDownloadResult(stream, photo.FileName, photo.ContentType));
    }

    /// <summary>
    /// Decision C — RequiresModuleEntitlementAttribute can't run on anonymous routes, so entitlement
    /// (including revocation after listings already exist) is enforced here instead. Deliberately
    /// does NOT set TenantContext itself: an AsyncLocal write made inside an awaited async helper
    /// does not flow back to its caller, so every public method sets it in its own body first
    /// (found by the live smoke test — the helper-set version made the entitlement check pass while
    /// every later query silently saw no tenant and RLS returned zero rows).
    /// </summary>
    private Task<bool> IsPublicEntitledAsync(Guid tenantId) =>
        moduleEntitlementService.IsEntitledAsync(tenantId, LeasingModuleKey);

    private async Task<string> UnitLabelAsync(Guid unitAssetId)
    {
        var unit = await unitAssetService.GetByIdAsync(unitAssetId);
        return unit.Outcome == ServiceResultOutcome.Success ? unit.Data!.Label : string.Empty;
    }

    private async Task ScanAsync(VacancyListingPhoto photo)
    {
        try
        {
            await using var scanStream = await fileStorage.OpenReadAsync(photo.StorageKey);
            var result = await ResiliencePolicies.ExternalProviderPolicy.ExecuteAsync(() =>
                fileScanner.ScanAsync(scanStream, photo.FileName, photo.ContentType));

            photo.MarkScanResult(result.Outcome == ScanOutcome.Clean ? VacancyListingPhotoScanStatus.Clean : VacancyListingPhotoScanStatus.Infected);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            photo.MarkScanResult(VacancyListingPhotoScanStatus.ScanFailed);
        }
    }

    private async Task<VacancyListingResponse> ToResponseAsync(VacancyListing listing)
    {
        var photos = await photoRepository.ListByListingAsync(listing.VacancyListingId);
        return new VacancyListingResponse(
            listing.VacancyListingId, listing.UnitAssetId, listing.RentAmount, listing.Currency, listing.AvailableFrom,
            listing.Description, listing.Status.ToUpperSnakeCase(), photos.Select(ToPhotoResponse).ToList(),
            repository.GetRowVersion(listing), listing.CreatedAt);
    }

    private static VacancyListingPhotoResponse ToPhotoResponse(VacancyListingPhoto photo) =>
        new(photo.VacancyListingPhotoId, photo.FileName, photo.ScanStatus.ToUpperSnakeCase(), photo.DisplayOrder, photo.CreatedAt);
}
