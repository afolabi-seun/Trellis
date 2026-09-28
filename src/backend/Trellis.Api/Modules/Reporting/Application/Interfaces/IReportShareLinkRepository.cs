using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

public interface IReportShareLinkRepository
{
    Task AddAsync(ReportShareLink link);

    /// <summary>The normal, authenticated path (Revoke) — Scoped() to TenantContext.Current.</summary>
    Task<ReportShareLink?> GetByIdAsync(Guid reportShareLinkId);

    /// <summary>
    /// The anonymous resolution path — deliberately cross-tenant, no Scoped()/TenantContext.Current
    /// dependency (see IOwnerReportRepository.GetByIdForTenantAsync's identical reasoning). The
    /// returned row already carries TenantId and OwnerReportId, which the caller uses for the
    /// follow-up IOwnerReportRepository.GetByIdForTenantAsync call.
    /// </summary>
    Task<ReportShareLink?> ResolveByTokenHashAsync(string tokenHash);

    Task SaveChangesAsync();
}
