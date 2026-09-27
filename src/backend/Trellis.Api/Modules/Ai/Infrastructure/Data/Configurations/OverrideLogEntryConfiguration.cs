using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Infrastructure.Data.Configurations;

public sealed class OverrideLogEntryConfiguration : IEntityTypeConfiguration<OverrideLogEntry>
{
    public void Configure(EntityTypeBuilder<OverrideLogEntry> builder)
    {
        builder.ToTable("override_log", schema: "trellis_ai");
        builder.HasKey(e => e.OverrideLogId);
        // No xmin — insert-only, immutable, matching audit_record's own append-only shape.

        builder.Property(e => e.OverrideLogId).HasColumnName("override_log_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");

        builder.Property(e => e.SuggestionType)
            .HasColumnName("suggestion_type")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<SuggestionType>(s))
            .HasMaxLength(20);

        builder.Property(e => e.SuggestionId).HasColumnName("suggestion_id");
        builder.Property(e => e.OriginalValueJson).HasColumnName("original_value_json");
        builder.Property(e => e.CorrectedValueJson).HasColumnName("corrected_value_json");
        builder.Property(e => e.OverriddenBy).HasColumnName("overridden_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.SuggestionType, e.SuggestionId });
    }
}
