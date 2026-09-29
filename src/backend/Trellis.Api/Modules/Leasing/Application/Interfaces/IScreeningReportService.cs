using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>
/// BDR-LSE-004's narrow, separately-audited full-report view — distinct from ordinary application
/// review, which only ever sees the coarse OutcomeSummary (ApplicationsController's own doc
/// comment).
/// </summary>
public interface IScreeningReportService
{
    /// <summary>
    /// Forbidden(Auth.ReauthenticationFailed) on a wrong password (audited as a failed attempt by
    /// IAuthService itself). NotFound when the application or its screening result/report doesn't
    /// exist — before any password check, since there is genuinely nothing to authorize viewing.
    /// The view itself is written and durably saved (application_event + audit_record) before any
    /// decryption happens, so a failure recording the view means the report is never decrypted.
    /// </summary>
    Task<ServiceResult<ScreeningReportResponse>> ViewAsync(Guid applicationId, Guid actorUserId, string currentPassword);
}
