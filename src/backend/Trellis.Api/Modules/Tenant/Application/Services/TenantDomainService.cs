using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Cors;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Core.Security;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Domain.Entities;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;

namespace Trellis.Api.Modules.Tenant.Application.Services;

/// <summary>
/// Custom domains for a tenant's public pages. ENTERPRISE only. A domain is unusable until DNS proves
/// the tenant controls it; only then does it resolve to the tenant, get echoed by CORS, and become the
/// base of emailed application links. Host resolution and the link base are anonymous-path methods:
/// they take an explicit host / tenantId, query unscoped (under SystemContext for the cross-tenant
/// host lookup), and never depend on an ambient tenant.
/// </summary>
public sealed class TenantDomainService(
    ITenantDomainRepository repository,
    ITenantRepository tenantRepository,
    IDomainOwnershipVerifier verifier,
    ICustomDomainOriginProvider originProvider,
    IAuditService auditService,
    IOptions<AppOptions> appOptions) : ITenantDomainService
{
    public const string VerificationRecordPrefix = "_trellis-verify.";

    public async Task<ServiceResult<TenantDomainResponse>> AddAsync(TenantDomainCreateRequest request, Guid actorUserId)
    {
        var tenantId = TenantContext.Current!.TenantId;
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
        {
            return ServiceResult<TenantDomainResponse>.NotFound();
        }

        if (tenant.SubscriptionTier != SubscriptionTier.Enterprise)
        {
            return ServiceResult<TenantDomainResponse>.Forbidden(ErrorCodeRegistry.Tenant.CustomDomainRequiresEnterprise);
        }

        var host = DomainPolicy.Normalize(request.Domain);
        if (host is null || !DomainPolicy.IsValid(host, appOptions.Value.GetPlatformHosts()))
        {
            return ServiceResult<TenantDomainResponse>.BusinessRuleViolation(ErrorCodeRegistry.Tenant.InvalidDomain);
        }

        var domain = TenantDomain.Create(tenantId, host, OpaqueTokenGenerator.GenerateRawToken());
        await repository.AddAsync(domain);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainAdded,
            $"{{\"tenantDomainId\":\"{domain.TenantDomainId}\",\"domain\":\"{host}\"}}");

        return ServiceResult<TenantDomainResponse>.Success(ToResponse(domain));
    }

    public async Task<ServiceResult<IReadOnlyList<TenantDomainResponse>>> ListAsync() =>
        ServiceResult<IReadOnlyList<TenantDomainResponse>>.Success((await repository.ListForTenantAsync()).Select(ToResponse).ToList());

    public async Task<ServiceResult<TenantDomainResponse>> VerifyAsync(Guid tenantDomainId, Guid actorUserId)
    {
        var domain = await repository.GetByIdAsync(tenantDomainId);
        if (domain is null)
        {
            return ServiceResult<TenantDomainResponse>.NotFound();
        }

        if (domain.Status == TenantDomainStatus.Verified)
        {
            return ServiceResult<TenantDomainResponse>.Success(ToResponse(domain));
        }

        if (!await verifier.HasTxtRecordAsync(VerificationRecordPrefix + domain.Domain, domain.ExpectedTxtValue))
        {
            return ServiceResult<TenantDomainResponse>.BusinessRuleViolation(ErrorCodeRegistry.Tenant.DomainVerificationFailed);
        }

        domain.MarkVerified();

        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another tenant verified the same domain first — the partial unique index on VERIFIED rows decides.
            return ServiceResult<TenantDomainResponse>.BusinessRuleViolation(ErrorCodeRegistry.Tenant.DomainVerificationFailed);
        }

        originProvider.Invalidate();

        await auditService.RecordAsync(
            domain.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainVerified,
            $"{{\"tenantDomainId\":\"{domain.TenantDomainId}\",\"domain\":\"{domain.Domain}\"}}");

        return ServiceResult<TenantDomainResponse>.Success(ToResponse(domain));
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid tenantDomainId, Guid actorUserId)
    {
        var domain = await repository.GetByIdAsync(tenantDomainId);
        if (domain is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        repository.Remove(domain);
        await repository.SaveChangesAsync();
        originProvider.Invalidate();

        await auditService.RecordAsync(
            domain.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainRemoved,
            $"{{\"tenantDomainId\":\"{domain.TenantDomainId}\",\"domain\":\"{domain.Domain}\"}}");

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<TenantPublicInfo>> ResolveHostAsync(string host)
    {
        var normalized = DomainPolicy.Normalize(host);
        if (normalized is null)
        {
            return ServiceResult<TenantPublicInfo>.NotFound();
        }

        TenantDomain? domain;
        using (SystemContext.Enter())
        {
            domain = await repository.GetVerifiedByHostAsync(normalized);
        }

        if (domain is null)
        {
            return ServiceResult<TenantPublicInfo>.NotFound();
        }

        var tenant = await tenantRepository.GetByIdAsync(domain.TenantId);
        return tenant is { Status: TenantStatus.Onboarding or TenantStatus.Active }
            ? ServiceResult<TenantPublicInfo>.Success(new TenantPublicInfo(tenant.TenantId, tenant.TenantName, tenant.Slug))
            : ServiceResult<TenantPublicInfo>.NotFound();
    }

    public async Task<string> GetLinkBaseUrlAsync(Guid tenantId)
    {
        TenantDomain? domain;
        using (SystemContext.Enter())
        {
            domain = await repository.GetPrimaryVerifiedForTenantAsync(tenantId);
        }

        return domain is null ? appOptions.Value.BaseUrl.TrimEnd('/') : $"https://{domain.Domain}";
    }

    private static TenantDomainResponse ToResponse(TenantDomain domain)
    {
        var pending = domain.Status == TenantDomainStatus.PendingVerification;
        return new TenantDomainResponse(
            domain.TenantDomainId, domain.Domain, domain.Status.ToUpperSnakeCase(),
            pending ? VerificationRecordPrefix + domain.Domain : null,
            pending ? domain.ExpectedTxtValue : null,
            domain.VerifiedAt, domain.CreatedAt);
    }
}
