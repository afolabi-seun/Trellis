using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Dtos;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

public interface IReportShareLinkService
{
    Task<ServiceResult<ReportShareLinkResponse>> CreateAsync(Guid ownerReportId, Guid createdBy);

    /// <summary>The anonymous path only — resolves a raw token to its linked OwnerReport's PDF bytes. Every failure mode (bad token, expired, revoked, report not Ready) returns NotFound, never Forbidden — no existence disclosure (error_handling.md, ErrorCodeRegistry.Auth.InvalidCredentials's own "same code either way" precedent).</summary>
    Task<ServiceResult<(byte[] PdfContent, DateOnly PeriodStart)>> ResolveAsync(string rawToken);

    Task<ServiceResult<bool>> RevokeAsync(Guid reportShareLinkId);
}
