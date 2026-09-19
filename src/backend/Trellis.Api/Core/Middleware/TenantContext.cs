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
