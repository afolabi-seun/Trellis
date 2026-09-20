using Trellis.Api.Modules.Tenant.Domain.Exceptions;

namespace Trellis.Api.Modules.Tenant.Domain.Entities;

/// <summary>data_model.md's `tenant.status` — `ONBOARDING` is the interim value between an active tenant and `OFFBOARDED`.</summary>
public enum TenantStatus
{
    Onboarding,
    Active,
    Suspended,
    Offboarding,
    Offboarded,
}

/// <summary>
/// data_model.md's `tenant.subscription_tier` — repurposed as a feature/support tier, decoupled
/// from the actual invoice amount (billing_design.md's usage-based model).
/// </summary>
public enum SubscriptionTier
{
    Starter,
    Professional,
    Enterprise,
}

/// <summary>
/// The PM company tenant — the aggregate root every other tenant-owned table's RLS policy
/// isolates *around*. Not itself RLS-protected (there is no tenant to isolate a tenant row from
/// itself); access here is RBAC (`SUPER_ADMIN` only), not row-level security.
/// </summary>
public class Tenant
{
    public Guid TenantId { get; private set; }
    public string TenantName { get; private set; } = null!;
    public string CountryCode { get; private set; } = null!;
    public TenantStatus Status { get; private set; }
    public SubscriptionTier SubscriptionTier { get; private set; }
    public string? ConfigJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Create(string tenantName, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            throw new ArgumentException("Tenant name is required.", nameof(tenantName));
        }

        if (countryCode is not { Length: 2 })
        {
            throw new ArgumentException("Country code must be an ISO 3166-1 alpha-2 code.", nameof(countryCode));
        }

        var now = DateTimeOffset.UtcNow;
        return new Tenant
        {
            TenantId = Guid.NewGuid(),
            TenantName = tenantName,
            CountryCode = countryCode.ToUpperInvariant(),
            Status = TenantStatus.Onboarding,
            SubscriptionTier = SubscriptionTier.Starter,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Activate() => TransitionTo(TenantStatus.Active, TenantStatus.Onboarding, TenantStatus.Suspended);

    public void Suspend() => TransitionTo(TenantStatus.Suspended, TenantStatus.Active);

    /// <summary>multi_tenant_design.md's Tenant Offboarding step 2 — the state-machine half; session revocation and audit are the caller's job.</summary>
    public void BeginOffboarding() => TransitionTo(TenantStatus.Offboarding, TenantStatus.Active, TenantStatus.Suspended);

    /// <summary>multi_tenant_design.md's Tenant Offboarding step 5 — reached only after the retention window and PII erasure.</summary>
    public void CompleteOffboarding() => TransitionTo(TenantStatus.Offboarded, TenantStatus.Offboarding);

    private void TransitionTo(TenantStatus target, params TenantStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidTenantStatusTransitionException(Status.ToString(), target.ToString());
        }

        Status = target;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
