using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;

namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>
/// Auth's public contract with the rest of the app — the Tenant module calls
/// <see cref="CreateTenantAdminAsync"/> during provisioning (multi_tenant_design.md's
/// <c>ProvisionTenantAsync</c>), never `trellis_auth` directly. More members are added here as
/// each of Auth's own endpoints (login, MFA, password reset, session revocation) is built.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates the first <c>TENANT_ADMIN</c> user for a newly provisioned tenant, participating
    /// in the caller's own transaction — this does not commit independently.
    /// </summary>
    Task<ServiceResult<AppUserSummary>> CreateTenantAdminAsync(Guid tenantId, string adminEmail);
}
