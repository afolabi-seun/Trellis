using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Tenant.Domain.Entities;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Api.Modules.Tenant.Infrastructure.Data.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("tenant", schema: "trellis_tenant");
        builder.HasKey(t => t.TenantId);

        // coding_standards.md's UseXminAsConcurrencyToken() is obsolete in this Npgsql version —
        // superseded by mapping the xmin system column as a shadow property with IsRowVersion(),
        // Npgsql's own current recommended replacement. Same zero-schema-cost guarantee.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.TenantName).HasColumnName("tenant_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(63).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();

        // data_model.md's schema stores status/tier as uppercase strings (ONBOARDING, STARTER,
        // ...) — the C# enum members are PascalCase, so a bare HasConversion<string>() would
        // store "Onboarding" instead and silently diverge from the documented column values.
        // Named arguments (ignoreCase: true) aren't allowed inside a HasConversion lambda — EF
        // Core compiles these to expression trees, which the C# expression-tree translator
        // rejects named arguments in.
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => (TenantStatus)Enum.Parse(typeof(TenantStatus), s, true))
            .HasMaxLength(20);

        builder.Property(t => t.SubscriptionTier)
            .HasColumnName("subscription_tier")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => (SubscriptionTier)Enum.Parse(typeof(SubscriptionTier), s, true))
            .HasMaxLength(20);

        builder.Property(t => t.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
