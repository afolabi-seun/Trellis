using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Inspection.Domain.Exceptions;

namespace Trellis.Api.Modules.Inspection.Domain.Entities;

public enum InspectionStatus
{
    Scheduled,
    InProgress,
    Completed,
    Closed,
}

/// <summary>
/// operational_flows.md's Inspection Flow — SCHEDULE (this entity's creation) → CAPTURE
/// (<see cref="MarkInProgress"/>) → SYNC → REPORT. Itself the Vendor-visible work item, identical
/// shape to WorkflowInstance: no column referencing vendor_assignment at all — that link is owned
/// entirely by trellis_vendor.vendor_assignment (work_item_type='INSPECTION',
/// work_item_id=InspectionId). Internal-staff assignment (AssignedToUserId) and Vendor assignment
/// are mutually exclusive, enforced at the service layer (VendorAssignmentService.CreateAsync
/// rejects a vendor assignment if AssignedToUserId is already set) — AssignedToUserId is settable
/// only at Create time, no endpoint ever changes it afterward.
/// </summary>
public sealed class InspectionRecord : ITenantOwned
{
    public Guid InspectionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public Guid? UnitAssetId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public Guid ScheduledByUserId { get; private set; }
    public InspectionStatus Status { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private InspectionRecord() { }

    public static InspectionRecord Create(
        Guid tenantId, Guid? propertyId, Guid? unitAssetId, Guid? assignedToUserId, Guid scheduledByUserId, DateTimeOffset scheduledAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new InspectionRecord
        {
            InspectionId = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            UnitAssetId = unitAssetId,
            AssignedToUserId = assignedToUserId,
            ScheduledByUserId = scheduledByUserId,
            Status = InspectionStatus.Scheduled,
            ScheduledAt = scheduledAt,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>operational_flows.md step 2, CAPTURE begins.</summary>
    public void MarkInProgress()
    {
        if (Status != InspectionStatus.Scheduled)
        {
            throw new InvalidStatusTransitionException(nameof(InspectionRecord), Status.ToString(), InspectionStatus.InProgress.ToString());
        }

        Status = InspectionStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        UpdatedAt = StartedAt.Value;
    }

    /// <summary>
    /// BDR-INSP-005's comparison view is Could-priority with no documented approval-gate schema —
    /// transitions straight to Closed rather than resting at Completed, the same reasoning
    /// VendorAssignment.Complete() already established for BDR-VEN-005. Completed stays a reserved
    /// enum value for symmetry, not built out now.
    /// </summary>
    public void Complete(decimal? latitude, decimal? longitude)
    {
        if (Status != InspectionStatus.InProgress)
        {
            throw new InvalidStatusTransitionException(nameof(InspectionRecord), Status.ToString(), InspectionStatus.Closed.ToString());
        }

        var now = DateTimeOffset.UtcNow;
        Status = InspectionStatus.Closed;
        CompletedAt = now;
        ClosedAt = now;
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = now;
    }
}
