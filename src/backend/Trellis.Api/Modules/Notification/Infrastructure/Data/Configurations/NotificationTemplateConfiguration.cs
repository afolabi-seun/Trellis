using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_template", schema: "trellis_notification");
        builder.HasKey(t => t.TemplateId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(t => t.TemplateId).HasColumnName("template_id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.TemplateKey).HasColumnName("template_key").HasMaxLength(50).IsRequired();
        builder.Property(t => t.Channel)
            .HasColumnName("channel")
            .HasConversion(c => c.ToString().ToUpperInvariant(), c => (NotificationChannel)Enum.Parse(typeof(NotificationChannel), c, true))
            .HasMaxLength(10);
        builder.Property(t => t.SubjectTemplate).HasColumnName("subject_template");
        builder.Property(t => t.BodyTemplate).HasColumnName("body_template").IsRequired();
        builder.Property(t => t.Locale).HasColumnName("locale").HasMaxLength(10).HasDefaultValue("en");

        // Known gap: Postgres treats each NULL as distinct in a unique index, so this doesn't
        // actually stop two platform-default rows (TenantId == null) for the same
        // (TemplateKey, Channel, Locale) — would need a COALESCE-based partial unique index to
        // close fully. Not exercised in Phase 1 (nothing creates templates via an endpoint yet,
        // only migration seed data), so left as a documented limitation rather than solved now.
        builder.HasIndex(t => new { t.TenantId, t.TemplateKey, t.Channel, t.Locale }).IsUnique();
    }
}
