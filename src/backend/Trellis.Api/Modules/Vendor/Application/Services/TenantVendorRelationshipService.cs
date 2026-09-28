using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;

namespace Trellis.Api.Modules.Vendor.Application.Services;

public sealed class TenantVendorRelationshipService(ITenantVendorRelationshipRepository repository, IVendorRepository vendorRepository, IAuditService auditService)
    : ITenantVendorRelationshipService
{
    public async Task<ServiceResult<TenantVendorRelationshipResponse>> CreateAsync(Guid vendorId)
    {
        if (await vendorRepository.GetByIdAsync(vendorId) is null)
        {
            return ServiceResult<TenantVendorRelationshipResponse>.NotFound();
        }

        var tenantId = TenantContext.Current!.TenantId;
        if (await repository.GetByTenantAndVendorAsync(tenantId, vendorId) is not null)
        {
            return ServiceResult<TenantVendorRelationshipResponse>.Conflict(
                ErrorCodeRegistry.Platform.ValidationFailed, "A relationship with this vendor already exists.");
        }

        var relationship = TenantVendorRelationship.Create(tenantId, vendorId);
        await repository.AddAsync(relationship);
        await repository.SaveChangesAsync();

        return ServiceResult<TenantVendorRelationshipResponse>.Success(ToResponse(relationship));
    }

    public async Task<ServiceResult<PagedResponse<TenantVendorRelationshipResponse>>> ListForTenantAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListForTenantAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].TenantVendorRelationshipId)
            : null;

        return ServiceResult<PagedResponse<TenantVendorRelationshipResponse>>.Success(
            new PagedResponse<TenantVendorRelationshipResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<TenantVendorRelationshipResponse>> TerminateAsync(Guid tenantVendorRelationshipId, Guid terminatedByUserId)
    {
        var relationship = await repository.GetByIdAsync(tenantVendorRelationshipId);
        if (relationship is null)
        {
            return ServiceResult<TenantVendorRelationshipResponse>.NotFound();
        }

        try
        {
            relationship.Terminate(terminatedByUserId);
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<TenantVendorRelationshipResponse>.BusinessRuleViolation(ErrorCodeRegistry.Vendor.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<TenantVendorRelationshipResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            relationship.TenantId, terminatedByUserId, AuditActorType.HumanUser, AuditTypes.Vendor.VendorRelationshipTerminated,
            $"{{\"tenantVendorRelationshipId\":\"{relationship.TenantVendorRelationshipId}\",\"vendorId\":\"{relationship.VendorId}\"}}");

        return ServiceResult<TenantVendorRelationshipResponse>.Success(ToResponse(relationship));
    }

    private static TenantVendorRelationshipResponse ToResponse(TenantVendorRelationship relationship) => new(
        relationship.TenantVendorRelationshipId,
        relationship.TenantId,
        relationship.VendorId,
        relationship.Status.ToString().ToUpperInvariant(),
        relationship.TerminatedAt,
        relationship.TerminatedByUserId,
        relationship.CreatedAt);
}
