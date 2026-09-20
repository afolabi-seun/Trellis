namespace Trellis.Api.Core.Middleware;

/// <summary>
/// Resolves the request's tenant or Vendor scope from JWT claims into
/// <see cref="TenantContext.Current"/> / <see cref="VendorContext.Current"/>
/// (multi_tenant_design.md) — one of the multi-tenant isolation model's three enforcement
/// layers. Neither claim present is only ever a platform-scope (<see cref="SystemContext"/>)
/// request if the JWT's role claim is verified as <c>SUPER_ADMIN</c> — never inferred from the
/// absence of the other two alone, which would let an unauthenticated or malformed request
/// bypass tenant isolation instead of seeing nothing.
/// </summary>
public sealed class TenantScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var vendorId = context.User.FindFirst("vendor_id")?.Value;
        var role = context.User.FindFirst("role")?.Value;

        if (tenantId is not null && Guid.TryParse(tenantId, out var parsedTenantId))
        {
            TenantContext.Current = new TenantContext(parsedTenantId);
        }
        else if (vendorId is not null && Guid.TryParse(vendorId, out var parsedVendorId))
        {
            VendorContext.Current = new VendorContext(parsedVendorId);
        }
        else if (role == "SUPER_ADMIN")
        {
            using (SystemContext.Enter())
            {
                await next(context);
                return;
            }
        }

        await next(context);
    }
}
