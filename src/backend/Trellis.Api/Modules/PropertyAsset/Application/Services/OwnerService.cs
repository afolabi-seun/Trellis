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

public sealed class OwnerService(IOwnerRepository repository) : IOwnerService
{
    public async Task<ServiceResult<OwnerResponse>> CreateAsync(OwnerCreateRequest request)
    {
        var owner = Owner.Create(TenantContext.Current!.TenantId, request.Name, request.Email, request.Phone);
        await repository.AddAsync(owner);
        await repository.SaveChangesAsync();

        return ServiceResult<OwnerResponse>.Success(ToResponse(owner));
    }

    public async Task<ServiceResult<OwnerResponse>> GetByIdAsync(Guid ownerId)
    {
        var owner = await repository.GetByIdAsync(ownerId);
        return owner is null
            ? ServiceResult<OwnerResponse>.NotFound()
            : ServiceResult<OwnerResponse>.Success(ToResponse(owner));
    }

    public async Task<ServiceResult<OwnerResponse>> GetByEmailAsync(string email)
    {
        var owner = await repository.GetByEmailAsync(email);
        return owner is null
            ? ServiceResult<OwnerResponse>.NotFound()
            : ServiceResult<OwnerResponse>.Success(ToResponse(owner));
    }

    public async Task<ServiceResult<PagedResponse<OwnerResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].OwnerId)
            : null;

        return ServiceResult<PagedResponse<OwnerResponse>>.Success(
            new PagedResponse<OwnerResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<OwnerResponse>> UpdateAsync(Guid ownerId, OwnerUpdateRequest request)
    {
        var owner = await repository.GetByIdAsync(ownerId);
        if (owner is null)
        {
            return ServiceResult<OwnerResponse>.NotFound();
        }

        owner.Update(request.Name, request.Email, request.Phone);

        try
        {
            // See PropertyService.UpdateAsync's identical comment: a malformed RowVersion throws
            // FormatException/OverflowException, not DbUpdateConcurrencyException, and needs the
            // same 409 outcome rather than an unhandled 500.
            repository.SetExpectedRowVersion(owner, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<OwnerResponse>.ConcurrencyConflict();
        }

        return ServiceResult<OwnerResponse>.Success(ToResponse(owner));
    }

    public async Task<ServiceResult<OwnerResponse>> ArchiveAsync(Guid ownerId)
    {
        var owner = await repository.GetByIdAsync(ownerId);
        if (owner is null)
        {
            return ServiceResult<OwnerResponse>.NotFound();
        }

        if (await repository.HasActivePropertiesAsync(ownerId))
        {
            return ServiceResult<OwnerResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.OwnerHasActiveProperties);
        }

        try
        {
            owner.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<OwnerResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<OwnerResponse>.Success(ToResponse(owner));
    }

    private OwnerResponse ToResponse(Owner owner) => new(
        owner.OwnerId,
        owner.Name,
        owner.Email,
        owner.Phone,
        owner.Status.ToString().ToUpperInvariant(),
        repository.GetRowVersion(owner),
        owner.CreatedAt);
}
