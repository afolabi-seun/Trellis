using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>
/// Deviates from leasing_design.md's literal bare-Task&lt;string&gt; shape to match this
/// codebase's universal ServiceResult&lt;T&gt; convention (direct analog: ReportShareLinkService
/// .CreateAsync). Explicit tenantId always — this whole surface is anonymous, no ambient
/// TenantContext ever exists.
/// </summary>
public interface IApplicationLinkService
{
    Task<ServiceResult<string>> IssueApplicationLinkAsync(Guid tenantId, Guid vacancyListingId, string applicantEmail);

    /// <summary>
    /// BDR-LSE-008 made explicit: resolving this token — the applicant clicking the emailed link —
    /// IS the email-control verification; no separate step exists. Resolvable repeatedly before
    /// submission (a reload mid-form-fill must still work); single-use is enforced at
    /// IApplicationService.SubmitAsync's own Consume() call, not here.
    /// </summary>
    Task<ServiceResult<ApplicationLinkContext>> ResolveAsync(string rawToken);
}
