using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Configurations;

public sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_code", schema: "trellis_auth");
        builder.HasKey(c => c.RecoveryCodeId);

        builder.Property(c => c.RecoveryCodeId).HasColumnName("recovery_code_id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UsedAt).HasColumnName("used_at");

        builder.HasIndex(c => new { c.UserId, c.CodeHash });
    }
}
