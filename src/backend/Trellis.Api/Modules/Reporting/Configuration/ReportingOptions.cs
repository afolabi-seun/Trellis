using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Reporting.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__Reporting__*</c> — see BillingOptions's identical doc comment.</summary>
public class ReportingOptions
{
    /// <summary>BDR-SEC-003 — how long a report share link stays resolvable before it 404s on its own, independent of an explicit Revoke.</summary>
    [Range(1, 90)]
    public int ShareLinkExpiryDays { get; init; } = 14;

    /// <summary>BDR-RPT-001's "bottlenecked steps" count — a WorkflowStepInstance still InProgress past this many days.</summary>
    [Range(1, 30)]
    public int WorkflowBottleneckThresholdDays { get; init; } = 5;
}
