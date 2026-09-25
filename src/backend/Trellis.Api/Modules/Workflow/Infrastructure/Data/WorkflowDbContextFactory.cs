using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Data;

public sealed class WorkflowDbContextFactory : DesignTimeDbContextFactoryBase<WorkflowDbContext>
{
    protected override WorkflowDbContext CreateContext(DbContextOptions<WorkflowDbContext> options) => new(options);
}
