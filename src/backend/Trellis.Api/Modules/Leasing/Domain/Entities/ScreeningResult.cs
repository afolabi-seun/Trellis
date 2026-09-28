using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum ScreeningOutcomeSummary
{
    Pass,
    Flagged,
    Fail,
}

/// <summary>
/// BDR-LSE-003/004 — the third-party screening outcome for one Application.
/// <see cref="FullReportCiphertext"/> is "the single most sensitive column in the schema"
/// (security_guide.md) — application-level-encrypted at rest, and per this phase's own deliberate
/// decision, never decrypted or returned by any API. Only <see cref="OutcomeSummary"/> ever
/// surfaces anywhere ("coarse result only," matching BDR-LSE-004's "never expose full report
/// detail" language). A future narrow, separately-audited decrypt view is an explicitly deferred
/// capability, not built this phase.
/// </summary>
public sealed class ScreeningResult : ITenantOwned
{
    public Guid ScreeningResultId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Provider { get; private set; } = null!;
    public ScreeningOutcomeSummary OutcomeSummary { get; private set; }
    public string? FullReportCiphertext { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    private ScreeningResult() { }

    public static ScreeningResult Create(
        Guid tenantId, Guid applicationId, string provider, ScreeningOutcomeSummary outcomeSummary, string? fullReportCiphertext) => new()
        {
            ScreeningResultId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Provider = provider,
            OutcomeSummary = outcomeSummary,
            FullReportCiphertext = fullReportCiphertext,
            ReceivedAt = DateTimeOffset.UtcNow,
        };
}
