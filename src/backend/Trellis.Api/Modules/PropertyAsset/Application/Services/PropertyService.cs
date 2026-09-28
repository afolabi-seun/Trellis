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

public sealed class PropertyService(IPropertyRepository repository) : IPropertyService
{
    public async Task<ServiceResult<PropertyResponse>> CreateAsync(PropertyCreateRequest request)
    {
        var property = Property.Create(
            TenantContext.Current!.TenantId,
            request.OwnerId,
            request.Address,
            ParsePropertyType(request.PropertyType),
            request.SizeSqft,
            request.AcquisitionDate,
            request.AcquisitionValue,
            request.CurrentValuation);

        await repository.AddAsync(property);
        await repository.SaveChangesAsync();

        return ServiceResult<PropertyResponse>.Success(ToResponse(property));
    }

    public async Task<ServiceResult<PropertyResponse>> GetByIdAsync(Guid propertyId)
    {
        var property = await repository.GetByIdAsync(propertyId);
        return property is null
            ? ServiceResult<PropertyResponse>.NotFound()
            : ServiceResult<PropertyResponse>.Success(ToResponse(property));
    }

    public async Task<ServiceResult<PagedResponse<PropertyResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].PropertyId)
            : null;

        return ServiceResult<PagedResponse<PropertyResponse>>.Success(
            new PagedResponse<PropertyResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<PropertyResponse>> UpdateAsync(Guid propertyId, PropertyUpdateRequest request)
    {
        var property = await repository.GetByIdAsync(propertyId);
        if (property is null)
        {
            return ServiceResult<PropertyResponse>.NotFound();
        }

        property.Update(
            request.OwnerId,
            request.Address,
            ParsePropertyType(request.PropertyType),
            request.SizeSqft,
            request.AcquisitionDate,
            request.AcquisitionValue,
            request.CurrentValuation);

        try
        {
            // SetExpectedRowVersion parses the client-presented RowVersion — a malformed value
            // (tampered with, or just a client bug) throws FormatException/OverflowException,
            // not DbUpdateConcurrencyException, and needs the same 409 outcome, not a 500. Found
            // live: the first version of this had SetExpectedRowVersion outside the try block.
            repository.SetExpectedRowVersion(property, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<PropertyResponse>.ConcurrencyConflict();
        }

        return ServiceResult<PropertyResponse>.Success(ToResponse(property));
    }

    public async Task<ServiceResult<PropertyResponse>> ArchiveAsync(Guid propertyId)
    {
        var property = await repository.GetByIdAsync(propertyId);
        if (property is null)
        {
            return ServiceResult<PropertyResponse>.NotFound();
        }

        if (await repository.HasActiveUnitAssetsAsync(propertyId))
        {
            return ServiceResult<PropertyResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.PropertyHasActiveUnitAssets);
        }

        try
        {
            property.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<PropertyResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<PropertyResponse>.Success(ToResponse(property));
    }

    private PropertyResponse ToResponse(Property property) => new(
        property.PropertyId,
        property.OwnerId,
        property.Address,
        PropertyTypeToString(property.PropertyType),
        property.Status.ToString().ToUpperInvariant(),
        property.SizeSqft,
        property.AcquisitionDate,
        property.AcquisitionValue,
        property.CurrentValuation,
        repository.GetRowVersion(property),
        property.CreatedAt);

    // data_model.md stores property_type as RESIDENTIAL|COMMERCIAL|MIXED_USE — the underscore in
    // MIXED_USE means a bare .ToString().ToUpperInvariant() (which works for every other enum in
    // this codebase) would produce "MIXEDUSE" instead, and Enum.Parse the reverse wouldn't match
    // it back. Explicit mapping, same pattern JwtTokenService.IssueAccessToken uses for role names.
    private static string PropertyTypeToString(PropertyType type) => type switch
    {
        PropertyType.MixedUse => "MIXED_USE",
        var other => other.ToString().ToUpperInvariant(),
    };

    private static PropertyType ParsePropertyType(string value) =>
        value.ToUpperInvariant() switch
        {
            "MIXED_USE" => PropertyType.MixedUse,
            var other => Enum.Parse<PropertyType>(other, true),
        };

    public Task<IReadOnlyList<Guid>> ListPropertyIdsByOwnerAsync(Guid ownerId) => repository.ListIdsByOwnerAsync(ownerId);
}
