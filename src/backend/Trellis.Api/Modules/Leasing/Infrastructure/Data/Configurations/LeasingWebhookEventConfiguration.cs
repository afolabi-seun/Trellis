using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Data.Configurations;

public sealed class LeasingWebhookEventConfiguration : IEntityTypeConfiguration<LeasingWebhookEvent>
{
    public void Configure(EntityTypeBuilder<LeasingWebhookEvent> builder)
    {
        builder.ToTable("leasing_webhook_event", schema: "trellis_leasing");
        builder.HasKey(w => w.LeasingWebhookEventId);

        builder.Property(w => w.LeasingWebhookEventId).HasColumnName("leasing_webhook_event_id");
        builder.Property(w => w.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(255);
        builder.Property(w => w.EventType).HasColumnName("event_type").HasMaxLength(50);
        builder.Property(w => w.PayloadJson).HasColumnName("payload_json");
        builder.Property(w => w.ReceivedAt).HasColumnName("received_at");
        builder.Property(w => w.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(w => w.ProviderEventId).IsUnique();
    }
}
