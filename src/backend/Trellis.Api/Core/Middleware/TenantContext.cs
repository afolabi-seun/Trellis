namespace Trellis.Api.Core.Middleware;

/// <summary>
/// The current request's resolved tenant, set once per request by
/// <see cref="TenantScopeMiddleware"/> from the JWT's <c>tenant_id</c> claim
/// (multi_tenant_design.md). <c>AsyncLocal</c>-backed so it flows correctly across
/// <c>await</c> boundaries without needing to be threaded through every method signature.
/// </summary>
public sealed class TenantContext(Guid tenantId)
{
    private static readonly AsyncLocal<TenantContext?> CurrentHolder = new();

    public Guid TenantId { get; } = tenantId;

    public static TenantContext? Current
    {
        get => CurrentHolder.Value;
        set => CurrentHolder.Value = value;
    }
}

/// <summary>
/// The current request's resolved Vendor identity, set by <see cref="TenantScopeMiddleware"/>
/// from the JWT's <c>vendor_id</c> claim. A Vendor token carries no <c>tenant_id</c> — its
/// access spans tenants and is resolved per-request against <c>vendor_assignment</c>
/// (multi_tenant_design.md, api_standards.md's Authentication section).
/// </summary>
public sealed class VendorContext(Guid vendorId)
{
    private static readonly AsyncLocal<VendorContext?> CurrentHolder = new();

    public Guid VendorId { get; } = vendorId;

    public static VendorContext? Current
    {
        get => CurrentHolder.Value;
        set => CurrentHolder.Value = value;
    }
}

/// <summary>
/// The Super Admin / background-job RLS bypass flag (multi_tenant_design.md, "Super Admin
/// Bypass" — <c>app.is_system</c>). Deliberately a third, separate flag rather than inferred
/// from "neither <see cref="TenantContext"/> nor <see cref="VendorContext"/> is set" — that
/// inference would be fail-open: an unauthenticated or malformed request that reaches a scoped
/// query with no context at all must see zero rows, not every tenant's. Set explicitly by:
/// <see cref="TenantScopeMiddleware"/> after confirming the <c>SUPER_ADMIN</c> role claim; a
/// trusted background job; or the Auth module's own identity-resolution lookups (login-by-
/// username, MFA-partial-token-by-id, forgot-password-by-username) that must run before any
/// tenant can be known — see <see cref="Enter"/> and multi_tenant_design.md's "Identity
/// Resolution Before Tenant Is Known" section.
/// </summary>
public sealed class SystemContext
{
    private static readonly AsyncLocal<bool> IsSystemHolder = new();

    public static bool IsSystem
    {
        get => IsSystemHolder.Value;
        set => IsSystemHolder.Value = value;
    }

    /// <summary>
    /// Scopes the bypass to a single lookup and restores the prior value afterward, so a narrow,
    /// server-only operation (never client-controlled) can't leak "is system" into unrelated code
    /// that happens to run later on the same async flow.
    /// </summary>
    public static IDisposable Enter()
    {
        var previous = IsSystemHolder.Value;
        IsSystemHolder.Value = true;
        return new Scope(previous);
    }

    private sealed class Scope(bool previous) : IDisposable
    {
        public void Dispose() => IsSystemHolder.Value = previous;
    }
}
