using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Audit;

namespace Trellis.Api.Core.Data;

/// <summary>
/// The one cross-cutting <c>DbContext</c> that isn't owned by a module — <c>trellis_audit</c>
/// belongs to the not-yet-built Reporting module (data_model.md), but every module needs
/// <see cref="IAuditService"/> from Phase 1 onward. Migrates first, ahead of every module, since
/// Tenant's own provisioning flow writes an audit record on its first real use.
/// </summary>
public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

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
