using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;

namespace Trellis.Api.Modules.Vendor.Domain.Entities;

public enum VendorAssignmentStatus
{
    Assigned,
    InProgress,
    Completed,
    Closed,
}

/// <summary>
/// BDR-VEN-002/003 — links one work item (currently only a WorkflowInstance; Inspection in
/// Phase 5) to the Vendor doing the work, for exactly one engaging tenant. This entity is the
/// *source* <see cref="IVendorAssignmentReader"/> reads from — it's the work item entities
/// themselves (WorkflowInstance) that implement <see cref="IVendorAssignable"/> and get filtered
/// by <see cref="VendorScopedRepository{T}"/>, not this one.
/// </summary>
public sealed class VendorAssignment : ITenantOwned
{
    public Guid AssignmentId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid TenantId { get; private set; }
    public string WorkItemType { get; private set; } = null!;
    public Guid WorkItemId { get; private set; }
    public VendorAssignmentStatus Status { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private VendorAssignment() { }

    public static VendorAssignment Create(Guid vendorId, Guid tenantId, string workItemType, Guid workItemId) => new()
    {
        AssignmentId = Guid.NewGuid(),
        VendorId = vendorId,
        TenantId = tenantId,
        WorkItemType = workItemType,
        WorkItemId = workItemId,
        Status = VendorAssignmentStatus.Assigned,
        AssignedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>BDR-VEN-003 — the vendor has started the work.</summary>
    public void MarkInProgress()
    {
        if (Status != VendorAssignmentStatus.Assigned)
        {
            throw new InvalidStatusTransitionException("VendorAssignment", Status.ToString(), VendorAssignmentStatus.InProgress.ToString());
        }

        Status = VendorAssignmentStatus.InProgress;
    }

    /// <summary>
    /// BDR-VEN-005's approval/sign-off gate is deferred (Should priority, no schema documented
    /// anywhere) — transitions straight to Closed rather than resting at Completed pending a
    /// sign-off step that doesn't exist yet, matching operational_flows.md's own documented
    /// no-approval-configured branch: "otherwise closes on Vendor/staff completion."
    /// </summary>
    public void Complete()
    {
        if (Status is not (VendorAssignmentStatus.Assigned or VendorAssignmentStatus.InProgress))
        {
            throw new InvalidStatusTransitionException("VendorAssignment", Status.ToString(), VendorAssignmentStatus.Closed.ToString());
        }

        Status = VendorAssignmentStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;
    }
}
