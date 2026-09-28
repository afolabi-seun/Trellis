using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;
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

    public Task<int> CountByStatusAsync(WorkflowInstanceStatus status) =>
        Scoped(dbContext.WorkflowInstances).CountAsync(i => i.Status == status);

    public Task<int> CountBottleneckedStepsAsync(int thresholdDays)
    {
        var tenantId = TenantContext.Current!.TenantId;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-thresholdDays);
        return dbContext.WorkflowStepInstances.CountAsync(
            s => s.TenantId == tenantId && s.Status == WorkflowStepInstanceStatus.InProgress && s.CreatedAt <= cutoff);
    }

    public async Task<(decimal TotalCost, int OpenCount)> GetOwnerMaintenanceSummaryAsync(
        IReadOnlyList<Guid> propertyIds, DateOnly periodStart, DateOnly periodEnd)
    {
        if (propertyIds.Count == 0)
        {
            return (0m, 0);
        }

        var periodStartOffset = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndOffset = periodEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var forProperties = Scoped(dbContext.WorkflowInstances).Where(i => i.PropertyId != null && propertyIds.Contains(i.PropertyId.Value));

        var totalCost = await forProperties
            .Where(i => i.CreatedAt >= periodStartOffset && i.CreatedAt <= periodEndOffset)
            .SumAsync(i => i.CostAmount ?? 0m);

        var openCount = await forProperties.CountAsync(i => i.Status == WorkflowInstanceStatus.InProgress);

        return (totalCost, openCount);
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
