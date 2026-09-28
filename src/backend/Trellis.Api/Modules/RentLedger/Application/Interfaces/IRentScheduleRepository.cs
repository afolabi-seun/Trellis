using Trellis.Api.Modules.RentLedger.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Interfaces;

public interface IRentScheduleRepository
{
    Task AddAsync(RentSchedule schedule);

    Task<RentSchedule?> GetByIdAsync(Guid rentScheduleId);

    Task<(IReadOnlyList<RentSchedule> Items, bool HasMore, int TotalCount)> ListForTenantAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    Task<IReadOnlyList<RentSchedule>> ListActiveForTenantAsync();

    /// <summary>Every schedule (any status) on the given units — the Owner report counts only the ACTIVE ones but sums payments received on ended ones too.</summary>
    Task<IReadOnlyList<RentSchedule>> ListForUnitsAsync(IReadOnlyCollection<Guid> unitAssetIds);

    /// <summary>Explicit tenantId, not Scoped() — LeasingWebhookService's idempotent get-or-create runs under SystemContext.</summary>
    Task<RentSchedule?> GetByLeaseApplicationIdAsync(Guid tenantId, Guid leaseApplicationId);

    Task<RentSchedule?> GetActiveForUnitAsync(Guid tenantId, Guid unitAssetId);

    /// <summary>The arrears job's cross-tenant keyset batch (no Scoped(), runs under SystemContext) — ordered by id so a failing row can't be re-fetched forever.</summary>
    Task<IReadOnlyList<RentSchedule>> ListActiveAcrossTenantsAsync(Guid? afterScheduleId, int batchSize);

    /// <summary>Stops tracking a row whose save failed, so one bad row can't poison the next SaveChangesAsync in the arrears job's batch.</summary>
    void DiscardChanges(RentSchedule schedule);

    string GetRowVersion(RentSchedule schedule);

    void SetExpectedRowVersion(RentSchedule schedule, string rowVersion);

    Task SaveChangesAsync();
}
