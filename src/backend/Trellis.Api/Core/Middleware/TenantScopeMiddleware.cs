namespace Trellis.Api.Core.Middleware;

/// <summary>
/// Resolves the request's tenant or Vendor scope from JWT claims into
/// <see cref="TenantContext.Current"/> / <see cref="VendorContext.Current"/>
/// (multi_tenant_design.md) — one of the multi-tenant isolation model's three enforcement
/// layers. Neither claim present (and a SUPER_ADMIN role) means a platform-scope request; no
/// context is set. A no-op until Auth (Phase 1) actually issues JWTs carrying these claims.
/// </summary>
public sealed class TenantScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var vendorId = context.User.FindFirst("vendor_id")?.Value;

        if (tenantId is not null && Guid.TryParse(tenantId, out var parsedTenantId))
        {
            TenantContext.Current = new TenantContext(parsedTenantId);
        }
        else if (vendorId is not null && Guid.TryParse(vendorId, out var parsedVendorId))
        {
            VendorContext.Current = new VendorContext(parsedVendorId);
        }

        await next(context);
    }
}
