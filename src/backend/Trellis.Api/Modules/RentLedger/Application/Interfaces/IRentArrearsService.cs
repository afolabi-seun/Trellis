namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

/// <summary>Kept as a public service, with ArrearsCheckBackgroundService a thin shell over it (exact DunningService precedent), so the arrears logic is unit-testable with an explicit as-of date.</summary>
public interface IRentArrearsService
{
    /// <summary>
    /// BDR-RENT-003/004 — across every entitled tenant, notify staff once per newly-arrears period.
    /// Returns how many alerts were raised. Must run under SystemContext (the caller's job); sets
    /// TenantContext.Current per tenant itself for the Scoped() cross-module unit lookup.
    /// </summary>
    Task<int> RaiseArrearsAlertsAsync(DateOnly asOf);
}
