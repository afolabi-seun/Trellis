using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Tenant.Application.Dtos;

namespace Trellis.Api.Modules.Tenant.Application.Interfaces;

public interface ITenantDomainService
{
    // Staff (TENANT_ADMIN) — the caller's own tenant, via TenantContext.Current.

    /// <summary>ENTERPRISE tier only (the Handbook's stated trigger for custom domains) — anything else is TRL-E-TEN-2002.</summary>
    Task<ServiceResult<TenantDomainResponse>> AddAsync(TenantDomainCreateRequest request, Guid actorUserId);

    Task<ServiceResult<IReadOnlyList<TenantDomainResponse>>> ListAsync();

    /// <summary>Checks the DNS TXT record and, only if it is there, marks the domain VERIFIED and refreshes the CORS snapshot.</summary>
    Task<ServiceResult<TenantDomainResponse>> VerifyAsync(Guid tenantDomainId, Guid actorUserId);

    Task<ServiceResult<bool>> RemoveAsync(Guid tenantDomainId, Guid actorUserId);

    // Anonymous / cross-module

    /// <summary>Public: which tenant serves this host? NotFound unless it is a VERIFIED domain of a live tenant.</summary>
    Task<ServiceResult<TenantPublicInfo>> ResolveHostAsync(string host);

    /// <summary>The origin emailed links to a tenant's public pages are built against: its VERIFIED custom domain (https) when it has one, else the platform's own frontend base URL. Explicit tenantId — safe on anonymous paths.</summary>
    Task<string> GetLinkBaseUrlAsync(Guid tenantId);
}
