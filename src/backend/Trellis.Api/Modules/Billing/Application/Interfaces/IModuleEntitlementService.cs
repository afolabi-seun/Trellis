using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IModuleEntitlementService
{
    /// <summary>RequiresModuleEntitlementAttribute's own call — a plain bool, not a ServiceResult, since a filter isn't a Controller action.</summary>
    Task<bool> IsEntitledAsync(Guid tenantId, string moduleKey);

    Task<ServiceResult<IReadOnlyList<ModuleEntitlementResponse>>> ListForTenantAsync(Guid tenantId);

    /// <summary>Super Admin only — enables/disables a module or reprices it, creating the entitlement row on first use.</summary>
    Task<ServiceResult<ModuleEntitlementResponse>> SetAsync(Guid tenantId, string moduleKey, ModuleEntitlementSetRequest request);

    /// <summary>
    /// UsageSnapshotBackgroundService's own per-tenant bootstrap step — the InitialBilling
    /// migration's retrofit seed (ACTIVE, $0/mo for INSPECTION and AI) only ever ran once, against
    /// tenants that existed at that moment. Without this, every tenant provisioned afterward would
    /// get zero entitlement rows and silently fail RequiresModuleEntitlement forever, since "no row"
    /// reads as "not entitled." Idempotent — a no-op for a tenant that already has the row.
    /// </summary>
    Task EnsureDefaultEntitlementsAsync(Guid tenantId);
}
