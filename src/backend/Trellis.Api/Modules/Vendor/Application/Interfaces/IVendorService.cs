using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface IVendorService
{
    /// <summary>Creates the VendorRecord and the caller's own first ACTIVE TenantVendorRelationship together (decision 2 of the Phase 4 plan).</summary>
    Task<ServiceResult<VendorResponse>> CreateAsync(VendorCreateRequest request);

    Task<ServiceResult<VendorResponse>> GetByIdAsync(Guid vendorId);

    /// <summary>Platform-wide (no tenant scoping) — lets a second, unrelated tenant find an already-onboarded vendor instead of duplicating it (BDR-VEN-004).</summary>
    Task<ServiceResult<PagedResponse<VendorResponse>>> SearchAsync(string? nameFilter, string? cursor, int limit);

    /// <summary>Creates the vendor's one login (IAuthService.CreateVendorUserAsync) — called once per vendor, by whichever tenant first invites them.</summary>
    Task<ServiceResult<VendorResponse>> InviteUserAsync(Guid vendorId, VendorInviteUserRequest request);
}
