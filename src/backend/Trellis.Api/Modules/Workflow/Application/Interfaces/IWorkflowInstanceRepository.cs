using Trellis.Api.Modules.Workflow.Domain.Entities;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IWorkflowInstanceRepository
{
    Task AddAsync(WorkflowInstance instance);

    /// <summary>Always includes Steps — an instance is never useful without its step history (BDR-WF-004).</summary>
    Task<WorkflowInstance?> GetByIdAsync(Guid workflowInstanceId);

    /// <summary>
    /// Vendor-context read — deliberately not TenantContext.Current-scoped (a Vendor request has
    /// no TenantContext, only VendorContext, so calling Scoped() here would throw). Correctness
    /// comes from the tenant_or_vendor_isolation RLS policy on workflow_instance/
    /// workflow_step_instance (layer 1, already active for this connection since the caller's
    /// VendorContext.Current is real) plus the app-layer IVendorAssignmentReader check the Service
    /// makes before calling this (layer 2) — not from any filter in this method itself.
    /// </summary>
    Task<WorkflowInstance?> GetForVendorAsync(Guid workflowInstanceId);

    Task<(IReadOnlyList<WorkflowInstance> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    /// <summary>Reporting's dashboard (BDR-RPT-001) — tenant-scoped via Scoped(), a plain count, not a full ServiceResult&lt;T&gt; shape, matching UnitAssetRepository's own CountActiveForTenantAsync precedent for cross-module aggregate reads.</summary>
    Task<int> CountByStatusAsync(WorkflowInstanceStatus status);

    /// <summary>"Bottleneck" = a WorkflowStepInstance still InProgress past thresholdDays, using the step's own CreatedAt as the activation-time proxy — WorkflowStepInstance has no separately tracked "became active" timestamp.</summary>
    Task<int> CountBottleneckedStepsAsync(int thresholdDays);

    /// <summary>Reporting's Owner report (BDR-OWN-002) — TotalCost sums CostAmount for instances against the given properties created within [periodStart, periodEnd]; OpenCount is a current-state count (InProgress), not period-bound, matching the dashboard's own "open work orders" semantics.</summary>
    Task<(decimal TotalCost, int OpenCount)> GetOwnerMaintenanceSummaryAsync(
        IReadOnlyList<Guid> propertyIds, DateOnly periodStart, DateOnly periodEnd);

    Task SaveChangesAsync();
}
