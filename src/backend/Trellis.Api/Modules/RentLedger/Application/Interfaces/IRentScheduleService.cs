using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.RentLedger.Application.Dtos;

namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

public interface IRentScheduleService
{
    // Staff (TENANT_ADMIN + DEPARTMENT_STAFF)

    /// <summary>BDR-RENT-001's manual path — a unit already occupied before Leasing existed. Also marks the unit occupied (every legacy unit is backfilled VACANT, and a schedule implies occupancy).</summary>
    Task<ServiceResult<RentScheduleResponse>> CreateAsync(RentScheduleCreateRequest request, Guid createdBy);

    Task<ServiceResult<RentScheduleResponse>> UpdateAsync(Guid rentScheduleId, RentScheduleUpdateRequest request, Guid actorUserId);

    Task<ServiceResult<RentScheduleResponse>> EndAsync(Guid rentScheduleId, Guid actorUserId);

    Task<ServiceResult<PagedResponse<RentScheduleResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<RentScheduleResponse>> GetByIdAsync(Guid rentScheduleId);

    /// <summary>The arrears list — every ACTIVE schedule currently InArrears, oldest unpaid period first. Loads the tenant's active schedules and filters in memory (a computed status can't be paged in SQL); bounded by a tenant's unit count.</summary>
    Task<ServiceResult<IReadOnlyList<RentScheduleResponse>>> ListArrearsAsync();

    // Cross-module

    /// <summary>
    /// BDR-RENT-001 — called by LeasingWebhookService on lease execution. Idempotent get-or-create by
    /// leaseApplicationId (a webhook retry never makes a second schedule); a no-op when the tenant
    /// isn't entitled to RENT_COLLECTION; ends any prior ACTIVE schedule on the unit (the previous
    /// tenancy) so a signed lease is never blocked. Explicit tenantId — safe under SystemContext.
    /// </summary>
    Task<ServiceResult<bool>> EnsureScheduleForLeaseAsync(
        Guid tenantId, Guid unitAssetId, Guid leaseApplicationId, decimal rentAmount, string currency, DateOnly availableFrom, Guid? actorUserId);

    /// <summary>
    /// BDR-RENT-001's counterpart on the way out: called when Leasing ends a lease. A no-op (Success)
    /// when the tenant isn't entitled, or when the schedule is missing or already ended — the caller
    /// (freeing the unit to be re-listed) must never be blocked by rent tracking being absent or
    /// already tidy. Explicit tenantId — safe under an ambient staff request or SystemContext alike.
    /// </summary>
    Task<ServiceResult<bool>> EndForLeaseAsync(Guid tenantId, Guid leaseApplicationId, Guid? actorUserId);

    /// <summary>
    /// BDR-RENT-005 — the Owner report's rollup across the given units. Null when the current tenant
    /// isn't entitled to RENT_COLLECTION (the report then omits rent rather than showing a
    /// misleading zero). Requires the caller's TenantContext.Current (Scoped()).
    /// </summary>
    Task<OwnerRentSummary?> GetOwnerRentSummaryAsync(IReadOnlyList<Guid> unitAssetIds, DateOnly periodStart, DateOnly periodEnd, DateOnly asOf);
}
