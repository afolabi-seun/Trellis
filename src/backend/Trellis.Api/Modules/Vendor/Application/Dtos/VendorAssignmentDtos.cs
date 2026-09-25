using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Vendor.Application.Dtos;

public sealed record VendorAssignmentCreateRequest(Guid VendorId, string WorkItemType, Guid WorkItemId);

public sealed record VendorAssignmentResponse(
    Guid AssignmentId,
    Guid VendorId,
    Guid TenantId,
    string WorkItemType,
    Guid WorkItemId,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ClosedAt);

/// <summary>The vendor-side list shape — adds the engaging tenant's display name (resolved via ITenantService, never denormalized) since a Vendor's own view spans tenants it has no other way to identify.</summary>
public sealed record VendorAssignmentVendorView(VendorAssignmentResponse Assignment, string TenantName);

/// <summary>The vendor-side single-item detail shape — adds the vendor-safe work item projection (BDR-VEN-002: no CostAmount/RequestingDepartmentId) on top of what the list view carries.</summary>
public sealed record VendorAssignmentDetailView(VendorAssignmentResponse Assignment, string TenantName, VendorWorkflowInstanceView? WorkItem);
