using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Tenant.Domain.Entities;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data.Configurations;

/// <summary>"One tenant per VERIFIED domain" is a Postgres partial unique index — not expressible via EF Core's fluent API — so it lives in the owning migration's raw SQL only.</summary>
public sealed class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("tenant_domain", schema: "trellis_tenant");
        builder.HasKey(d => d.TenantDomainId);

        builder.Property(d => d.TenantDomainId).HasColumnName("tenant_domain_id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Domain).HasColumnName("domain").HasMaxLength(253);
        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<TenantDomainStatus>(s))
            .HasMaxLength(30);
        builder.Property(d => d.VerificationToken).HasColumnName("verification_token").HasMaxLength(64);
        builder.Property(d => d.VerifiedAt).HasColumnName("verified_at");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Ignore(d => d.ExpectedTxtValue);

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.Domain);
    }
}
