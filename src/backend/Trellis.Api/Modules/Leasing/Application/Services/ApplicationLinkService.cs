using Microsoft.Extensions.Options;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Configuration;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;
using Trellis.Api.Modules.Leasing.Infrastructure.Security;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// BDR-LSE-002/008 — the whole surface here is anonymous. Every failure mode (unknown listing,
/// module not entitled, unknown/expired/consumed token) is the same NotFound — no existence
/// disclosure, matching ReportShareLinkService.ResolveAsync's own convention.
/// </summary>
public sealed class ApplicationLinkService(
    IApplicationLinkTokenRepository tokenRepository,
    IVacancyListingRepository listingRepository,
    IModuleEntitlementService moduleEntitlementService,
    IUnitAssetService unitAssetService,
    INotificationService notificationService,
    IOptions<LeasingOptions> leasingOptions,
    ITenantDomainService tenantDomainService) : IApplicationLinkService
{
    private const string LeasingModuleKey = "LEASING";

    public async Task<ServiceResult<string>> IssueApplicationLinkAsync(Guid tenantId, Guid vacancyListingId, string applicantEmail)
    {
        // The route's tenantId is user-supplied, and only ever used to read that tenant's
        // intentionally-public listing data plus write one link row against it — see
        // VacancyListingService's class doc for why TenantContext is set here at all.
        TenantContext.Current = new TenantContext(tenantId);

        if (!await moduleEntitlementService.IsEntitledAsync(tenantId, LeasingModuleKey))
        {
            return ServiceResult<string>.NotFound();
        }

        var listing = await listingRepository.GetByIdForTenantAsync(tenantId, vacancyListingId);
        if (listing is null || !listing.AcceptsApplications())
        {
            return ServiceResult<string>.NotFound();
        }

        var rawToken = ApplicationLinkTokenGenerator.GenerateRawToken();
        var expiryDays = leasingOptions.Value.ApplicationLinkExpiryDays;
        var token = ApplicationLinkToken.Create(
            tenantId, vacancyListingId, applicantEmail, ApplicationLinkTokenGenerator.Hash(rawToken),
            DateTimeOffset.UtcNow.AddDays(expiryDays));

        await tokenRepository.AddAsync(token);
        await tokenRepository.SaveChangesAsync();

        var unit = await unitAssetService.GetByIdAsync(listing.UnitAssetId);
        await notificationService.SendAsync(
            tenantId,
            "APPLICATION_LINK",
            applicantEmail,
            new
            {
                ExpiryDays = expiryDays,
                UnitLabel = unit.Outcome == ServiceResultOutcome.Success ? unit.Data!.Label : string.Empty,
                // The tenant's own verified domain when it has one, else the platform frontend — so the applicant lands on the brand they browsed.
                ApplicationLinkUrl = $"{await tenantDomainService.GetLinkBaseUrlAsync(tenantId)}/apply/{rawToken}",
            },
            new RelatedEntityRef("VacancyListing", vacancyListingId),
            NotificationRecipientType.ProspectiveTenant);

        return ServiceResult<string>.Success(rawToken);
    }

    public async Task<ServiceResult<ApplicationLinkContext>> ResolveAsync(string rawToken)
    {
        // The token lookup is genuinely cross-tenant — the tenant isn't known until the row is
        // found — so this one query runs under SystemContext, scoped to just the lookup.
        ApplicationLinkToken? token;
        using (SystemContext.Enter())
        {
            token = await tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash(rawToken));
        }

        if (token is null || !token.IsUsable())
        {
            return ServiceResult<ApplicationLinkContext>.NotFound();
        }

        // Tenant now resolved from the trusted token row, never user input.
        TenantContext.Current = new TenantContext(token.TenantId);

        if (!await moduleEntitlementService.IsEntitledAsync(token.TenantId, LeasingModuleKey))
        {
            return ServiceResult<ApplicationLinkContext>.NotFound();
        }

        var listing = await listingRepository.GetByIdForTenantAsync(token.TenantId, token.VacancyListingId);
        if (listing is null || !listing.AcceptsApplications())
        {
            return ServiceResult<ApplicationLinkContext>.NotFound();
        }

        var unit = await unitAssetService.GetByIdAsync(listing.UnitAssetId);
        var label = unit.Outcome == ServiceResultOutcome.Success ? unit.Data!.Label : string.Empty;

        return ServiceResult<ApplicationLinkContext>.Success(new ApplicationLinkContext(
            token.TenantId, token.VacancyListingId, token.ApplicantEmail, label, listing.RentAmount, listing.Currency));
    }
}
