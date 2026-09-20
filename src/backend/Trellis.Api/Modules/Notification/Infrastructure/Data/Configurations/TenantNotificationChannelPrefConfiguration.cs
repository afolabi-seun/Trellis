using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Notification.Domain.Entities;

namespace Trellis.Api.Modules.Notification.Infrastructure.Data.Configurations;

public sealed class TenantNotificationChannelPrefConfiguration : IEntityTypeConfiguration<TenantNotificationChannelPref>
{
    public void Configure(EntityTypeBuilder<TenantNotificationChannelPref> builder)
    {
        builder.ToTable("tenant_notification_channel_pref", schema: "trellis_notification");
        builder.HasKey(p => new { p.TenantId, p.TemplateKey, p.Channel });

        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.TemplateKey).HasColumnName("template_key").HasMaxLength(50);
        builder.Property(p => p.Channel)
            .HasColumnName("channel")
            .HasConversion(c => c.ToString().ToUpperInvariant(), c => (NotificationChannel)Enum.Parse(typeof(NotificationChannel), c, true))
            .HasMaxLength(10);
        builder.Property(p => p.Enabled).HasColumnName("enabled").HasDefaultValue(true);
    }
}
