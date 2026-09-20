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
}
