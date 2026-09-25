using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class WorkflowStepInstanceConfiguration : IEntityTypeConfiguration<WorkflowStepInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowStepInstance> builder)
    {
        builder.ToTable("workflow_step_instance", schema: "trellis_workflow");
        builder.HasKey(s => s.WorkflowStepInstanceId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(s => s.WorkflowStepInstanceId).HasColumnName("workflow_step_instance_id");
        builder.Property(s => s.WorkflowInstanceId).HasColumnName("workflow_instance_id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.WorkflowStepTemplateId).HasColumnName("workflow_step_template_id");
        builder.Property(s => s.StepOrder).HasColumnName("step_order");
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.RoutedDepartmentId).HasColumnName("routed_department_id");

        // WorkflowStepInstanceStatus.InProgress is a compound name — see
        // WorkflowInstanceConfiguration's identical comment on its own Status property.
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToUpperSnakeCase(), s => EnumStringConverter.ParseUpperSnakeCase<WorkflowStepInstanceStatus>(s))
            .HasMaxLength(20);

        builder.Property(s => s.DecidedBy).HasColumnName("decided_by");
        builder.Property(s => s.DecidedAt).HasColumnName("decided_at");
        builder.Property(s => s.DecisionNote).HasColumnName("decision_note");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.WorkflowInstanceId);
    }
}
