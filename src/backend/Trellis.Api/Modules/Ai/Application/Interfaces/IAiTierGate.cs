namespace Trellis.Api.Modules.Ai.Application.Interfaces;

/// <summary>billing_design.md — the Ai module is reserved for PROFESSIONAL+ tenants, gated by tenant.subscription_tier (no RequiresModuleEntitlement/trellis_billing exists until Phase 7).</summary>
public interface IAiTierGate
{
    Task<bool> IsEntitledAsync(Guid tenantId);
}
