using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>BDR-BIL-001 — active_unit_count captured once at cycle-end by UsageSnapshotBackgroundService, not continuously metered. Insert-only, no transitions.</summary>
public sealed class UsageSnapshot : ITenantOwned
{
    public Guid SnapshotId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateOnly BillingPeriodStart { get; private set; }
    public DateOnly BillingPeriodEnd { get; private set; }
    public int ActiveUnitCount { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }

    private UsageSnapshot() { }

    public static UsageSnapshot Create(Guid tenantId, DateOnly billingPeriodStart, DateOnly billingPeriodEnd, int activeUnitCount) => new()
    {
        SnapshotId = Guid.NewGuid(),
        TenantId = tenantId,
        BillingPeriodStart = billingPeriodStart,
        BillingPeriodEnd = billingPeriodEnd,
        ActiveUnitCount = activeUnitCount,
        CapturedAt = DateTimeOffset.UtcNow,
    };
}
