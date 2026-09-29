using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IApplicationLinkTokenRepository
{
    Task AddAsync(ApplicationLinkToken token);

    /// <summary>The anonymous resolution path — no ambient TenantContext, exact ReportShareLinkRepository.ResolveByTokenHashAsync precedent.</summary>
    Task<ApplicationLinkToken?> ResolveByTokenHashAsync(string tokenHash);

    /// <summary>POPIA erasure's own lookup — Scoped() to TenantContext.Current.</summary>
    Task<IReadOnlyList<ApplicationLinkToken>> ListByApplicantEmailAsync(string applicantEmail);

    Task SaveChangesAsync();
}
