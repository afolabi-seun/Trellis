using Microsoft.EntityFrameworkCore;

namespace Trellis.Api.Core.Data;

/// <summary>
/// The idempotency-key store — <c>trellis_audit.audit_record</c> used to live here too (before
/// Reporting, Phase 8, was built), tracked by <see cref="IdempotencyKey"/>'s own migration
/// history table staying in the <c>trellis_audit</c> schema even after the ownership transfer
/// (see the Core migration that moved <c>AuditRecord</c> out — the schema name is a historical
/// artifact of that shared past, not a live Reporting dependency).
/// </summary>
public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // data_model.md's own ApplyConfigurationsFromAssembly(assembly) example, taken literally,
        // would map every module's entities into every DbContext in a single-project setup like
        // this one — it scans the WHOLE assembly for IEntityTypeConfiguration<T>, and every
        // module lives in this same Trellis.Api assembly. Scoped by namespace instead, so this
        // DbContext only ever picks up Core/'s own configurations, preserving the "no DbContext
        // maps a table it doesn't own" rule project_architecture.md actually requires.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CoreDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Core.", StringComparison.Ordinal) == true);
}
