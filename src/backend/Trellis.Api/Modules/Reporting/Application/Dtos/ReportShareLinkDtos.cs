namespace Trellis.Api.Modules.Reporting.Application.Dtos;

/// <summary>Token is the raw, unhashed value — shown to the caller exactly once, at creation, and never retrievable again (Auth's own password-reset-token posture).</summary>
public sealed record ReportShareLinkResponse(
    Guid ReportShareLinkId, Guid OwnerReportId, string Token, string Status, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);
