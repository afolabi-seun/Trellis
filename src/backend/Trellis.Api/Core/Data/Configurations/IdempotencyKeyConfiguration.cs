using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trellis.Api.Core.Data.Configurations;

/// <summary>
/// api_standards.md's <c>trellis_core.idempotency_key</c> — column/table mapping only, RLS and
/// the cleanup-friendly index live in the owning migration's raw SQL.
/// </summary>
public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_key", schema: "trellis_core");
        builder.HasKey(k => new { k.TenantId, k.Key });

        builder.Property(k => k.TenantId).HasColumnName("tenant_id");
        builder.Property(k => k.Key).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(k => k.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(k => k.ExpiresAt).HasColumnName("expires_at");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
    }
}
