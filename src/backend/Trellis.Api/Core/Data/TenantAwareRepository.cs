using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Middleware;

namespace Trellis.Api.Core.Data;

/// <summary>
/// Every module's repository classes inherit from this rather than querying a <c>DbSet&lt;T&gt;</c>
/// directly (multi_tenant_design.md, "TenantAwareRepository&lt;T&gt;") — "forgot the tenant
/// filter" becomes a base-class problem to fix once, not a per-query risk spread across every
/// module. This is layer 2 of the 3-layer isolation model; layer 1 (database RLS) holds even if
/// a subclass somehow bypasses this.
/// </summary>
public abstract class TenantAwareRepository<T> where T : class, ITenantOwned
{
    protected IQueryable<T> Scoped(DbSet<T> set) =>
        set.Where(e => e.TenantId == TenantContext.Current!.TenantId);
}
