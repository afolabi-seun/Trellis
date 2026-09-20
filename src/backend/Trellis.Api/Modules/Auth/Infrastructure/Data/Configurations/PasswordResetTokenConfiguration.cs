using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_token", schema: "trellis_auth");
        builder.HasKey(t => t.TokenId);

        builder.Property(t => t.TokenId).HasColumnName("token_id");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        // data_model.md's schema calls this INET, but Npgsql's EF Core provider won't map a C#
        // string directly to that type without a value converter — audit-display-only field, so
        // plain text is simpler and just as usable; not parsed or compared as an address anywhere.
        builder.Property(t => t.RequestedIp).HasColumnName("requested_ip").HasMaxLength(45);

        builder.HasIndex(t => t.TokenHash).IsUnique();
    }
}
