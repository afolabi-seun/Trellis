using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IApplicationRepository
{
    Task AddAsync(LeasingApplication application);

    /// <summary>Staff path — Scoped() to TenantContext.Current.</summary>
    Task<LeasingApplication?> GetByIdAsync(Guid applicationId);

    Task<(IReadOnlyList<LeasingApplication> Items, bool HasMore, int TotalCount)> ListByListingAsync(
        Guid vacancyListingId, (DateTimeOffset SubmittedAt, Guid Id)? cursor, int limit);

    /// <summary>Cross-tenant, unscoped — the e-signature webhook resolves the owning tenant before TenantContext.Current can be set, exact ReportShareLink precedent.</summary>
    Task<LeasingApplication?> GetByIdAcrossTenantsAsync(Guid applicationId);

    /// <summary>The webhook's own lookup — the provider's completion payload carries an envelope id, never a Trellis ApplicationId.</summary>
    Task<LeasingApplication?> GetByEnvelopeIdAcrossTenantsAsync(string envelopeId);

    /// <summary>POPIA erasure's own lookup — Scoped() to TenantContext.Current, same as GetByIdAsync. Every application this applicant ever submitted against this tenant, not just one.</summary>
    Task<IReadOnlyList<LeasingApplication>> ListByApplicantEmailAsync(string applicantEmail);

    Task SaveChangesAsync();
}
