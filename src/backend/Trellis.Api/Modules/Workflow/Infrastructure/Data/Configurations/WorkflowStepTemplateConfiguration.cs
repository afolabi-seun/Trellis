using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data.Configurations;

public sealed class WorkflowStepTemplateConfiguration : IEntityTypeConfiguration<WorkflowStepTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowStepTemplate> builder)
    {
        builder.ToTable("workflow_step_template", schema: "trellis_workflow");
        builder.HasKey(s => s.WorkflowStepTemplateId);

        builder.Property(s => s.WorkflowStepTemplateId).HasColumnName("workflow_step_template_id");
        builder.Property(s => s.WorkflowTemplateId).HasColumnName("workflow_template_id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.StepOrder).HasColumnName("step_order");
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.DefaultDepartmentId).HasColumnName("default_department_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.WorkflowTemplateId);

        // WorkflowStepTemplate owns its own Rules, same private-backing-field pattern as
        // WorkflowTemplate.Steps — see WorkflowTemplateConfiguration's comment.
        builder.HasMany(s => s.Rules)
            .WithOne()
            .HasForeignKey(r => r.WorkflowStepTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(s => s.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
