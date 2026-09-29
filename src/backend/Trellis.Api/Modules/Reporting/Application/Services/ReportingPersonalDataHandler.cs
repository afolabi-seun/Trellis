using Trellis.Api.Core.Privacy;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Application.Services;

/// <summary>
/// An owner subject, resolved the same way PropertyAssetPersonalDataHandler resolves its own
/// (by email) — through PropertyAsset's own IOwnerService.GetByEmailAsync, the one legitimate
/// cross-module read this handler needs, exact "own service exposes what it owns" convention
/// every other cross-module read in this codebase uses. OwnerReport.OwnerId, not an email column
/// of its own, is what actually ties a report to this subject.
/// </summary>
public sealed class ReportingPersonalDataHandler(
    IOwnerService ownerService, IOwnerReportRepository ownerReportRepository, IReportShareLinkRepository shareLinkRepository) : IPersonalDataHandler
{
    public string ModuleName => "Reporting";

    public async Task<object?> ExportAsync(string subjectEmail)
    {
        var ownerId = await ResolveOwnerIdAsync(subjectEmail);
        if (ownerId is null)
        {
            return null;
        }

        var reports = await ownerReportRepository.ListAllForOwnerAsync(ownerId.Value);
        return reports.Select(r => new { r.OwnerReportId, r.PeriodStart, r.PeriodEnd, Status = r.Status.ToString() }).ToList();
    }

    public async Task<ErasureSummary> EraseAsync(string subjectEmail)
    {
        var ownerId = await ResolveOwnerIdAsync(subjectEmail);
        if (ownerId is null)
        {
            return new ErasureSummary(ModuleName, 0);
        }

        var reports = await ownerReportRepository.ListAllForOwnerAsync(ownerId.Value);
        foreach (var report in reports)
        {
            report.RedactOwnerPii();

            var links = await shareLinkRepository.ListByOwnerReportIdAsync(report.OwnerReportId);
            foreach (var link in links.Where(l => l.Status == ReportShareLinkStatus.Active))
            {
                link.Revoke();
            }
        }

        await ownerReportRepository.SaveChangesAsync();
        await shareLinkRepository.SaveChangesAsync();

        return new ErasureSummary(ModuleName, reports.Count);
    }

    private async Task<Guid?> ResolveOwnerIdAsync(string subjectEmail)
    {
        var owner = await ownerService.GetByEmailAsync(subjectEmail);
        return owner.Outcome == ServiceResultOutcome.Success ? owner.Data!.OwnerId : null;
    }
}
