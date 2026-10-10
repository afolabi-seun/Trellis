using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

/// <summary>
/// The only way a Unit/Asset's occupancy changes: starting a lease marks it OCCUPIED, ending one
/// marks it VACANT from the lease's end date (BDR-AST-005). Every method requires the caller's
/// TenantContext.Current (Scoped()).
/// </summary>
public interface ILeaseService
{
    /// <summary>Idempotent by LeaseApplicationId: a second call for the same application returns the existing lease, so a retried e-signature webhook never starts two.</summary>
    Task<ServiceResult<LeaseResponse>> StartAsync(LeaseStartRequest request, Guid? actorUserId);

    Task<ServiceResult<LeaseResponse>> EndAsync(Guid leaseId, LeaseEndRequest request, Guid actorUserId);

    Task<ServiceResult<LeaseResponse>> RecordTermAsync(Guid leaseId, LeaseTermRequest request, Guid actorUserId);

    Task<ServiceResult<LeaseResponse>> GetByIdAsync(Guid leaseId);

    Task<ServiceResult<LeaseResponse>> GetByLeaseApplicationIdAsync(Guid leaseApplicationId);

    Task<ServiceResult<LeaseResponse>> GetActiveForUnitAsync(Guid unitAssetId);

    /// <summary>BDR-LSE-009 — active fixed-term leases ending within <paramref name="withinDays"/> days, plus any already past their end date and still active.</summary>
    Task<ServiceResult<IReadOnlyList<LeaseResponse>>> ListExpiringAsync(int withinDays);

    Task<ServiceResult<IReadOnlyList<LeaseResponse>>> ListTermNotRecordedAsync();
}
