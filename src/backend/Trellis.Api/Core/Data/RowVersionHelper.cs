using Microsoft.EntityFrameworkCore;

namespace Trellis.Api.Core.Data;

/// <summary>
/// coding_standards.md's Optimistic Concurrency convention: a Response DTO for any mutable
/// entity carries the `xmin` shadow property as an opaque <c>RowVersion</c> string (never parsed
/// by the client, just echoed back on Update). `xmin` isn't a real property on the Domain
/// entity — EF Core only exposes it via <c>ChangeTracker.Entry(entity).Property("xmin")</c> —
/// so every repository that surfaces it needs a DbContext, which is why this lives here rather
/// than on the entity itself (Domain purity) or the Service layer (no DbContext access there).
/// First real usage: PropertyAsset's Owner/Property/UnitAsset Update endpoints — Tenant never
/// needed this since it has no Update/Patch endpoint in Phase 1.
/// </summary>
public static class RowVersionHelper
{
    public static string Get(DbContext context, object entity) =>
        Convert.ToBase64String(BitConverter.GetBytes((uint)context.Entry(entity).Property("xmin").CurrentValue!));

    /// <summary>
    /// Tells EF Core "this is the version I loaded" before <c>SaveChangesAsync</c> — a mismatch
    /// with the row's actual current `xmin` throws <see cref="DbUpdateConcurrencyException"/>,
    /// caught at the Service boundary and mapped to <c>ServiceResult&lt;T&gt;.ConcurrencyConflict()</c>.
    /// </summary>
    public static void SetExpected(DbContext context, object entity, string rowVersion)
    {
        var bytes = Convert.FromBase64String(rowVersion);
        context.Entry(entity).Property("xmin").OriginalValue = BitConverter.ToUInt32(bytes);
    }
}
