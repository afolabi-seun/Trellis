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

    Task SaveChangesAsync();
}
