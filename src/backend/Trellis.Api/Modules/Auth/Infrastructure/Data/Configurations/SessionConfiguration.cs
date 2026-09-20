using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("session", schema: "trellis_auth");
        builder.HasKey(s => s.SessionId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(s => s.SessionId).HasColumnName("session_id");
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.IssuedAt).HasColumnName("issued_at");
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at");
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(s => s.UserId);
    }
}
