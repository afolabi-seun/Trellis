using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;

namespace Trellis.Api.Modules.Vendor.Application.Services;

public sealed class VendorService(
    IVendorRepository repository, ITenantVendorRelationshipRepository relationshipRepository, IAuthService authService)
    : IVendorService
{
    public async Task<ServiceResult<VendorResponse>> CreateAsync(VendorCreateRequest request)
    {
        var category = EnumStringConverter.ParseUpperSnakeCase<VendorCategory>(request.Category);
        var vendor = VendorRecord.Create(request.VendorName, category);
        await repository.AddAsync(vendor);
        await repository.SaveChangesAsync();

        var relationship = TenantVendorRelationship.Create(TenantContext.Current!.TenantId, vendor.VendorId);
        await relationshipRepository.AddAsync(relationship);
        await relationshipRepository.SaveChangesAsync();

        return ServiceResult<VendorResponse>.Success(ToResponse(vendor));
    }

    public async Task<ServiceResult<VendorResponse>> GetByIdAsync(Guid vendorId)
    {
        var vendor = await repository.GetByIdAsync(vendorId);
        return vendor is null
            ? ServiceResult<VendorResponse>.NotFound()
            : ServiceResult<VendorResponse>.Success(ToResponse(vendor));
    }

    public async Task<ServiceResult<PagedResponse<VendorResponse>>> SearchAsync(string? nameFilter, string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.SearchAsync(nameFilter, decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].VendorId)
            : null;

        return ServiceResult<PagedResponse<VendorResponse>>.Success(
            new PagedResponse<VendorResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<VendorResponse>> InviteUserAsync(Guid vendorId, VendorInviteUserRequest request)
    {
        var vendor = await repository.GetByIdAsync(vendorId);
        if (vendor is null)
        {
            return ServiceResult<VendorResponse>.NotFound();
        }

        var result = await authService.CreateVendorUserAsync(vendorId, request.Email);
        return result.Outcome == ServiceResultOutcome.Success
            ? ServiceResult<VendorResponse>.Success(ToResponse(vendor))
            : ServiceResult<VendorResponse>.Failure(ErrorCodeRegistry.Vendor.VendorUserCreationFailed, "Could not create the vendor's login.");
    }

    private static VendorResponse ToResponse(VendorRecord vendor) => new(
        vendor.VendorId, vendor.VendorName, vendor.Category.ToUpperSnakeCase(), vendor.CreatedAt);
}
