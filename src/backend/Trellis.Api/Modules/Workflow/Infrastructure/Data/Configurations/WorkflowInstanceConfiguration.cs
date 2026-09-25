using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("workflow_instance", schema: "trellis_workflow");
        builder.HasKey(i => i.WorkflowInstanceId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(i => i.WorkflowInstanceId).HasColumnName("workflow_instance_id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id");
        builder.Property(i => i.WorkflowTemplateId).HasColumnName("workflow_template_id");
        builder.Property(i => i.PropertyId).HasColumnName("property_id");
        builder.Property(i => i.RequestingDepartmentId).HasColumnName("requesting_department_id");
        builder.Property(i => i.CostAmount).HasColumnName("cost_amount").HasColumnType("numeric(14,2)");

        // WorkflowInstanceStatus.InProgress is a compound name — a bare ToUpperInvariant() would
        // round-trip it as "INPROGRESS", not "IN_PROGRESS" (data_model.md's SCREAMING_SNAKE_CASE
        // convention), the same PropertyType.MixedUse-shaped bug EnumStringConverter exists to
        // avoid. Found via this module's own live smoke test.
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<WorkflowInstanceStatus>(s))
            .HasMaxLength(20);

        builder.Property(i => i.CurrentStepOrder).HasColumnName("current_step_order");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => i.WorkflowTemplateId);
        builder.HasIndex(i => new { i.TenantId, i.Status });

        // WorkflowInstance owns its own Steps, same private-backing-field pattern as
        // WorkflowTemplate.Steps — see WorkflowTemplateConfiguration's comment.
        builder.HasMany(i => i.Steps)
            .WithOne()
            .HasForeignKey(s => s.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(i => i.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
