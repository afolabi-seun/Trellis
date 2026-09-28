using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Data.Configurations;

/// <summary>
/// data_model.md's <c>trellis_audit.audit_record</c> — append-only (a migration-level trigger
/// rejects UPDATE/DELETE), range-partitioned on <c>recorded_at</c> from its first migration
/// (BDR-NFR-008). The RLS policy and partitioning DDL live in the owning migration's raw SQL,
/// not here — this class is column/table mapping only.
/// </summary>
public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_record", schema: "trellis_audit");
        // Composite key, not just AuditId: Postgres requires a partitioned table's partition key
        // (recorded_at, per the owning migration's PARTITION BY RANGE) be part of every
        // PK/unique constraint.
        builder.HasKey(a => new { a.AuditId, a.RecordedAt });

        builder.Property(a => a.AuditId).HasColumnName("audit_id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.AuditType).HasColumnName("audit_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.DetailJson).HasColumnName("detail_json").HasColumnType("jsonb");
        builder.Property(a => a.RecordedAt).HasColumnName("recorded_at");
    }
}
