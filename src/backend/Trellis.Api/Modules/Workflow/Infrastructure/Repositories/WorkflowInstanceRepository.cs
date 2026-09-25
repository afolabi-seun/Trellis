using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;

namespace Trellis.Api.Modules.Workflow.Infrastructure.Repositories;

public sealed class WorkflowInstanceRepository(WorkflowDbContext dbContext)
    : TenantAwareRepository<WorkflowInstance>, IWorkflowInstanceRepository
{
    public async Task AddAsync(WorkflowInstance instance) => await dbContext.WorkflowInstances.AddAsync(instance);

    public Task<WorkflowInstance?> GetByIdAsync(Guid workflowInstanceId) =>
        Scoped(dbContext.WorkflowInstances)
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.WorkflowInstanceId == workflowInstanceId);

    public Task<WorkflowInstance?> GetForVendorAsync(Guid workflowInstanceId) =>
        dbContext.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.WorkflowInstanceId == workflowInstanceId);

    public async Task<(IReadOnlyList<WorkflowInstance> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit)
    {
        IQueryable<WorkflowInstance> query = Scoped(dbContext.WorkflowInstances).Include(i => i.Steps);

        if (cursor is { } c)
        {
            query = query.Where(i => i.CreatedAt > c.CreatedAt || (i.CreatedAt == c.CreatedAt && i.WorkflowInstanceId > c.Id));
        }

        var totalCount = await Scoped(dbContext.WorkflowInstances).CountAsync();

        var page = await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.WorkflowInstanceId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
