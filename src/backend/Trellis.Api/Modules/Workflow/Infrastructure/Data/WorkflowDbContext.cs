using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowStepTemplate> WorkflowStepTemplates => Set<WorkflowStepTemplate>();
    public DbSet<WorkflowRoutingRule> WorkflowRoutingRules => Set<WorkflowRoutingRule>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStepInstance> WorkflowStepInstances => Set<WorkflowStepInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Scoped by namespace — see PropertyDbContext's identical comment.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WorkflowDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Workflow.", StringComparison.Ordinal) == true);
}
