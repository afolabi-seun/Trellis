using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Domain.Exceptions;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

public enum EntitlementStatus
{
    Active,
    Disabled,
}

/// <summary>BDR-BIL-009 — one row per (tenant, module) pair, the third billing axis independent of subscription tier and usage. Unique(TenantId, ModuleKey) at the DB level.</summary>
public sealed class ModuleEntitlement : ITenantOwned
{
    public Guid EntitlementId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ModuleKey { get; private set; } = null!;
    public EntitlementStatus Status { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public DateTimeOffset EnabledAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    private ModuleEntitlement() { }

    public static ModuleEntitlement Create(Guid tenantId, string moduleKey, decimal monthlyPrice) => new()
    {
        EntitlementId = Guid.NewGuid(),
        TenantId = tenantId,
        ModuleKey = moduleKey,
        Status = EntitlementStatus.Active,
        MonthlyPrice = monthlyPrice,
        EnabledAt = DateTimeOffset.UtcNow,
    };

    public void Disable()
    {
        if (Status != EntitlementStatus.Active)
        {
            throw new InvalidStatusTransitionException(nameof(ModuleEntitlement), Status.ToString(), EntitlementStatus.Disabled.ToString());
        }

        Status = EntitlementStatus.Disabled;
        DisabledAt = DateTimeOffset.UtcNow;
    }

    public void Enable()
    {
        if (Status != EntitlementStatus.Disabled)
        {
            throw new InvalidStatusTransitionException(nameof(ModuleEntitlement), Status.ToString(), EntitlementStatus.Active.ToString());
        }

        Status = EntitlementStatus.Active;
        DisabledAt = null;
    }

    public void UpdatePrice(decimal monthlyPrice) => MonthlyPrice = monthlyPrice;
}
