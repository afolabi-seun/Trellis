namespace Trellis.Api.Modules.Billing.Application.Dtos;

public sealed record ModuleEntitlementResponse(
    Guid EntitlementId, string ModuleKey, string Status, decimal MonthlyPrice, DateTimeOffset EnabledAt, DateTimeOffset? DisabledAt);

/// <summary>PUT /platform/billing/module-entitlements/{tenantId}/{moduleKey} — Super Admin only (billing_design.md: "set by a Super Admin, per IBillingService, not hardcoded").</summary>
public sealed record ModuleEntitlementSetRequest(string Status, decimal MonthlyPrice);
