using Trellis.Api.Modules.Inspection.Domain.Entities;

namespace Trellis.Api.Modules.Inspection.Application.Interfaces;

public interface IInspectionRepository
{
    Task AddAsync(InspectionRecord inspection);

    Task<InspectionRecord?> GetByIdAsync(Guid inspectionId);

    /// <summary>
    /// Vendor-context read — deliberately not tenant-scoped (a Vendor request has no
    /// TenantContext, only VendorContext). Correctness comes from the tenant_or_vendor_isolation
    /// RLS policy on inspection (layer 1) plus the app-layer IVendorAssignmentReader check the
    /// Service makes before calling this (layer 2), same pattern as WorkflowInstanceRepository's
    /// own GetForVendorAsync.
    /// </summary>
    Task<InspectionRecord?> GetForVendorAsync(Guid inspectionId);

    Task<(IReadOnlyList<InspectionRecord> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(InspectionRecord inspection);

    void SetExpectedRowVersion(InspectionRecord inspection, string rowVersion);

    Task SaveChangesAsync();
}
