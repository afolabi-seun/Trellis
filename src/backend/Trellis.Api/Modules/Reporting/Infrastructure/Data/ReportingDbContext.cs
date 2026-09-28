using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data;

/// <summary>
/// Owns <c>trellis_audit.audit_record</c> (relocated from CoreDbContext, Phase 8 — see the Core
/// migration that transferred ownership) alongside the two tables genuinely new to this module.
/// The physical audit_record table stays in the trellis_audit schema; only which DbContext maps
/// it changed, not where it physically lives.
/// </summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<OwnerReport> OwnerReports => Set<OwnerReport>();
    public DbSet<ReportShareLink> ReportShareLinks => Set<ReportShareLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReportingDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Reporting.", StringComparison.Ordinal) == true);
}
