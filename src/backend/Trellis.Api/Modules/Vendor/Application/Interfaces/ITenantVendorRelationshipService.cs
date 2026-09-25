using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Vendor.Application.Dtos;

namespace Trellis.Api.Modules.Vendor.Application.Interfaces;

public interface ITenantVendorRelationshipService
{
    /// <summary>Lets a second, unrelated tenant attach a relationship to an already-onboarded vendor without duplicating it (BDR-VEN-004) — VendorService.CreateAsync already creates the caller's first relationship, so this covers every relationship after the first.</summary>
    Task<ServiceResult<TenantVendorRelationshipResponse>> CreateAsync(Guid vendorId);

    Task<ServiceResult<PagedResponse<TenantVendorRelationshipResponse>>> ListForTenantAsync(string? cursor, int limit);

    /// <summary>BDR-VEN-006 — gates only new VendorAssignment rows for this tenant+vendor pair going forward; never touches assignments that already exist.</summary>
    Task<ServiceResult<TenantVendorRelationshipResponse>> TerminateAsync(Guid tenantVendorRelationshipId, Guid terminatedByUserId);
}
