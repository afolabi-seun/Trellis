namespace Trellis.Api.Modules.Billing.Application.Dtos;

public sealed record SubscriptionResponse(
    Guid SubscriptionId, Guid TenantId, string Status, string Currency, int BillingCycleAnchorDay, DateTimeOffset CreatedAt);

/// <summary>GET /api/v1/billing/subscription — the current-usage/projected-invoice summary a Tenant Admin's Billing settings page opens on (BDR-BIL-006).</summary>
public sealed record SubscriptionSummaryResponse(
    SubscriptionResponse Subscription, int? LatestActiveUnitCount, decimal? ProjectedInvoiceTotal);

/// <summary>GET /platform/billing/summary — BDR-BIL-007's cross-tenant aggregate.</summary>
public sealed record PlatformRevenueSummaryResponse(
    decimal MonthlyRecurringRevenue, int TrialingCount, int ActiveCount, int PastDueCount, int SuspendedCount, int CanceledCount);
