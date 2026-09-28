using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

namespace Trellis.Api.Modules.PropertyAsset.Application.Services;

/// <summary>
/// Depends on <see cref="IPropertyRepository"/> too — not a cross-module boundary
/// ("own your schema, expose a service" applies between modules, not within one), just orchestrating
/// the parent lookup a URL like <c>/api/v1/properties/{propertyId}/units</c> needs: a
/// <paramref name="propertyId"/> naming a property outside the caller's tenant is indistinguishable
/// from one that doesn't exist (RLS), so it surfaces as the same 404 either way.
/// </summary>
public sealed class UnitAssetService(IUnitAssetRepository repository, IPropertyRepository propertyRepository) : IUnitAssetService
{
    public async Task<ServiceResult<UnitAssetResponse>> CreateAsync(Guid propertyId, UnitAssetCreateRequest request)
    {
        if (await propertyRepository.GetByIdAsync(propertyId) is null)
        {
            return ServiceResult<UnitAssetResponse>.NotFound();
        }

        var unitAsset = UnitAsset.Create(
            TenantContext.Current!.TenantId, propertyId, request.Label, request.SizeSqft, request.AcquisitionValue);
        await repository.AddAsync(unitAsset);
        await repository.SaveChangesAsync();

        return ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public async Task<ServiceResult<UnitAssetResponse>> GetByIdAsync(Guid unitAssetId)
    {
        var unitAsset = await repository.GetByIdAsync(unitAssetId);
        return unitAsset is null
            ? ServiceResult<UnitAssetResponse>.NotFound()
            : ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public async Task<ServiceResult<PagedResponse<UnitAssetResponse>>> ListByPropertyAsync(Guid propertyId, string? cursor, int limit)
    {
        if (await propertyRepository.GetByIdAsync(propertyId) is null)
        {
            return ServiceResult<PagedResponse<UnitAssetResponse>>.NotFound();
        }

        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListByPropertyAsync(propertyId, decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].UnitAssetId)
            : null;

        return ServiceResult<PagedResponse<UnitAssetResponse>>.Success(
            new PagedResponse<UnitAssetResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<UnitAssetResponse>> UpdateAsync(Guid unitAssetId, UnitAssetUpdateRequest request)
    {
        var unitAsset = await repository.GetByIdAsync(unitAssetId);
        if (unitAsset is null)
        {
            return ServiceResult<UnitAssetResponse>.NotFound();
        }

        unitAsset.Update(request.Label, request.SizeSqft, request.AcquisitionValue);

        try
        {
            // See PropertyService.UpdateAsync's identical comment: a malformed RowVersion throws
            // FormatException/OverflowException, not DbUpdateConcurrencyException, and needs the
            // same 409 outcome rather than an unhandled 500.
            repository.SetExpectedRowVersion(unitAsset, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<UnitAssetResponse>.ConcurrencyConflict();
        }

        return ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public async Task<ServiceResult<UnitAssetResponse>> ArchiveAsync(Guid unitAssetId)
    {
        var unitAsset = await repository.GetByIdAsync(unitAssetId);
        if (unitAsset is null)
        {
            return ServiceResult<UnitAssetResponse>.NotFound();
        }

        try
        {
            unitAsset.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<UnitAssetResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public async Task<ServiceResult<UnitAssetResponse>> MarkOccupiedAsync(Guid unitAssetId)
    {
        var unitAsset = await repository.GetByIdAsync(unitAssetId);
        if (unitAsset is null)
        {
            return ServiceResult<UnitAssetResponse>.NotFound();
        }

        try
        {
            unitAsset.MarkOccupied();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<UnitAssetResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public async Task<ServiceResult<UnitAssetResponse>> MarkVacantAsync(Guid unitAssetId)
    {
        var unitAsset = await repository.GetByIdAsync(unitAssetId);
        if (unitAsset is null)
        {
            return ServiceResult<UnitAssetResponse>.NotFound();
        }

        try
        {
            unitAsset.MarkVacant();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<UnitAssetResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<UnitAssetResponse>.Success(ToResponse(unitAsset));
    }

    public Task<IReadOnlyList<Guid>> ListUnitAssetIdsByPropertiesAsync(IReadOnlyCollection<Guid> propertyIds) =>
        repository.ListIdsByPropertiesAsync(propertyIds);

    public Task<int> CountActiveForTenantAsync(Guid tenantId) => repository.CountActiveForTenantAsync(tenantId);

    private UnitAssetResponse ToResponse(UnitAsset unitAsset) => new(
        unitAsset.UnitAssetId,
        unitAsset.PropertyId,
        unitAsset.Label,
        unitAsset.Status.ToString().ToUpperInvariant(),
        unitAsset.OccupancyStatus.ToString().ToUpperInvariant(),
        unitAsset.SizeSqft,
        unitAsset.AcquisitionValue,
        repository.GetRowVersion(unitAsset),
        unitAsset.CreatedAt);
}
