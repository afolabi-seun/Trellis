using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.ToTable("workflow_template", schema: "trellis_workflow");
        builder.HasKey(t => t.WorkflowTemplateId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(t => t.WorkflowTemplateId).HasColumnName("workflow_template_id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.RequestType).HasColumnName("request_type").HasMaxLength(50).IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToString().ToUpperInvariant(), s => (WorkflowTemplateStatus)Enum.Parse(typeof(WorkflowTemplateStatus), s, true))
            .HasMaxLength(20);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => new { t.TenantId, t.RequestType });

        // WorkflowTemplate owns its Steps (BDR-WF-001: edited as one coherent definition, never
        // assembled via separate step-by-step API calls) — EF Core reads/writes through the
        // private `_steps` backing field since `Steps` itself only exposes IReadOnlyList<T>.
        builder.HasMany(t => t.Steps)
            .WithOne()
            .HasForeignKey(s => s.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
