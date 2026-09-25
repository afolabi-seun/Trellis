namespace Trellis.Api.Core.Data;

/// <summary>
/// <c>trellis_vendor.vendor_assignment.work_item_type</c>'s two values (data_model.md) — a
/// small, shared vocabulary so every module that references a work item polymorphically (Vendor,
/// Workflow, and Inspection once Phase 5 exists) agrees on the exact string. Only
/// <see cref="WorkflowInstance"/> has a real work item type behind it today.
/// </summary>
public static class WorkItemTypes
{
    public const string WorkflowInstance = "WORKFLOW_INSTANCE";
    public const string Inspection = "INSPECTION";
}
