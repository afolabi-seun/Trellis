using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

/// <summary>Append-only — no xmin, no UPDATE grant at the migration level (same belt-and-suspenders posture as trellis_audit.audit_record's own grants).</summary>
public sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> builder)
    {
        builder.ToTable("application_event", schema: "trellis_leasing");
        builder.HasKey(e => e.ApplicationEventId);

        builder.Property(e => e.ApplicationEventId).HasColumnName("application_event_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ApplicationId).HasColumnName("application_id");
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(50);

        builder.Property(e => e.ActorType)
            .HasColumnName("actor_type")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<ApplicationEventActorType>(s))
            .HasMaxLength(20);

        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.DetailJson).HasColumnName("detail_json").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.ApplicationId);
    }
}
