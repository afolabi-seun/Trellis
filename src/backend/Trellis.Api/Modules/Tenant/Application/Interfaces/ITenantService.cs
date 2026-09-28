using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

public interface ITenantService
{
    /// <summary>multi_tenant_design.md's `ProvisionTenantAsync` — one transaction: creates the tenant, its first Tenant Admin (via <c>IAuthService</c>), and the provisioning audit record.</summary>
    Task<ServiceResult<TenantResponse>> ProvisionTenantAsync(TenantProvisionRequest request);

    Task<ServiceResult<TenantResponse>> GetByIdAsync(Guid tenantId);

    Task<ServiceResult<PagedResponse<TenantResponse>>> ListAsync(string? cursor, int limit);

    /// <summary>multi_tenant_design.md's Tenant Offboarding steps 1-2: status transition, session revocation, audit. Steps 3/5 (export job, scheduled erasure) are a tracked follow-up, not built this phase.</summary>
    Task<ServiceResult<TenantResponse>> OffboardTenantAsync(Guid tenantId);

    /// <summary>billing_design.md's dunning state machine — the one place Billing reaches into Tenant, never by writing to trellis_tenant.tenant directly. Also revokes every active session, the same trust-boundary reasoning OffboardTenantAsync already applies: a tenant suspended for non-payment shouldn't retain live access on a valid refresh token while unable to act.</summary>
    Task<ServiceResult<TenantResponse>> SuspendAsync(Guid tenantId, string reason);

    /// <summary>billing_design.md: "reactivation on a subsequent successful charge reverses both the subscription and tenant status automatically." A no-op (still Success) if the tenant isn't currently Suspended — DunningService.ReactivateOnSuccessAsync calls this unconditionally on every successful charge, not just ones following a suspension.</summary>
    Task<ServiceResult<TenantResponse>> ReactivateAsync(Guid tenantId);
}
