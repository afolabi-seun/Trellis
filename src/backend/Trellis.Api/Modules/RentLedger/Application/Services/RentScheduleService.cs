using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Configuration;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.RentLedger.Application.Services;

/// <summary>
/// BDR-RENT-001/003/005. Records what rent is expected and reports what is recorded against it —
/// it never processes, holds, or moves funds (BDR-RENT-006), and deliberately has no dependency on
/// Billing beyond the entitlement check.
/// </summary>
public sealed class RentScheduleService(
    IRentScheduleRepository scheduleRepository,
    IRentPaymentRecordRepository paymentRepository,
    IUnitAssetService unitAssetService,
    ILeaseService leaseService,
    IModuleEntitlementService moduleEntitlementService,
    IAuditService auditService,
    IOptions<RentLedgerOptions> options) : IRentScheduleService
{
    public const string ModuleKey = "RENT_COLLECTION";

    /// <summary>The due day derived from a lease's availability date is capped at 28 so it never depends on a short month's length; staff can edit it after the fact.</summary>
    private const int MaxDerivedDueDay = 28;

    public async Task<ServiceResult<RentScheduleResponse>> CreateAsync(RentScheduleCreateRequest request, Guid createdBy)
    {
        var unit = await unitAssetService.GetByIdAsync(request.UnitAssetId);
        if (unit.Outcome != ServiceResultOutcome.Success || unit.Data!.Status == "ARCHIVED")
        {
            return ServiceResult<RentScheduleResponse>.NotFound();
        }

        var tenantId = TenantContext.Current!.TenantId;
        if (await scheduleRepository.GetActiveForUnitAsync(tenantId, request.UnitAssetId) is not null)
        {
            return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.UnitAlreadyHasActiveSchedule);
        }

        var lease = await ResolveLeaseForManualScheduleAsync(request, createdBy);
        if (lease.Outcome != ServiceResultOutcome.Success)
        {
            return lease.Outcome == ServiceResultOutcome.NotFound
                ? ServiceResult<RentScheduleResponse>.NotFound()
                : ServiceResult<RentScheduleResponse>.BusinessRuleViolation(lease.ErrorCode!);
        }

        var schedule = RentSchedule.Create(
            tenantId, request.UnitAssetId, null, lease.Data!.LeaseId, request.RentAmount, request.Currency, request.StartDate,
            request.DueDayOfMonth, request.GracePeriodDays ?? options.Value.DefaultGracePeriodDays, createdBy);

        await scheduleRepository.AddAsync(schedule);

        try
        {
            await scheduleRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race against the one-active-schedule-per-unit partial unique index.
            return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.UnitAlreadyHasActiveSchedule);
        }

        await auditService.RecordAsync(
            tenantId, createdBy, AuditActorType.HumanUser, AuditTypes.RentLedger.RentScheduleCreated,
            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\",\"unitAssetId\":\"{schedule.UnitAssetId}\"}}");

        return ServiceResult<RentScheduleResponse>.Success(await ToResponseAsync(schedule));
    }

    public async Task<ServiceResult<RentScheduleResponse>> UpdateAsync(Guid rentScheduleId, RentScheduleUpdateRequest request, Guid actorUserId)
    {
        var schedule = await scheduleRepository.GetByIdAsync(rentScheduleId);
        if (schedule is null)
        {
            return ServiceResult<RentScheduleResponse>.NotFound();
        }

        try
        {
            schedule.Update(request.RentAmount, request.DueDayOfMonth, request.GracePeriodDays);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.ScheduleNotActive);
        }

        try
        {
            scheduleRepository.SetExpectedRowVersion(schedule, request.RowVersion);
            await scheduleRepository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<RentScheduleResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            schedule.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.RentLedger.RentScheduleUpdated,
            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\"}}");

        return ServiceResult<RentScheduleResponse>.Success(await ToResponseAsync(schedule));
    }

    public async Task<ServiceResult<RentScheduleResponse>> EndAsync(Guid rentScheduleId, RentScheduleEndRequest? request, Guid actorUserId)
    {
        var schedule = await scheduleRepository.GetByIdAsync(rentScheduleId);
        if (schedule is null)
        {
            return ServiceResult<RentScheduleResponse>.NotFound();
        }

        if (schedule.Status != RentScheduleStatus.Active)
        {
            return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.InvalidStatusTransition);
        }

        // A MANUAL lease exists only because of a rent schedule, so ending the schedule is ending
        // the tenancy: the lease ends first (with BDR-LSE-011's reason, vacating the unit), and a
        // rejected end leaves the schedule running. A LEASING lease is ended through Leasing;
        // ending only its schedule just stops tracking rent against it.
        var lease = await leaseService.GetByIdAsync(schedule.LeaseId);
        if (lease.Outcome == ServiceResultOutcome.Success && lease.Data!.Source == "MANUAL" && lease.Data.Status == "ACTIVE")
        {
            if (request is null)
            {
                return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.LeaseEndDetailsRequired);
            }

            var ended = await leaseService.EndAsync(
                schedule.LeaseId, new LeaseEndRequest(request.EffectiveDate, request.Reason, request.EndedBy, request.Note), actorUserId);
            if (ended.Outcome != ServiceResultOutcome.Success)
            {
                return ServiceResult<RentScheduleResponse>.BusinessRuleViolation(ended.ErrorCode ?? ErrorCodeRegistry.RentLedger.InvalidStatusTransition);
            }
        }

        schedule.End();
        await scheduleRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            schedule.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.RentLedger.RentScheduleEnded,
            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\",\"leaseId\":\"{schedule.LeaseId}\"}}");

        return ServiceResult<RentScheduleResponse>.Success(await ToResponseAsync(schedule));
    }

    public async Task<ServiceResult<PagedResponse<RentScheduleResponse>>> ListAsync(string? cursor, int limit)
    {
        var (items, hasMore, totalCount) = await scheduleRepository.ListForTenantAsync(CursorCodec.Decode(cursor), limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].RentScheduleId) : null;

        var totals = await paymentRepository.GetReceivedTotalsAsync(items.Select(s => s.RentScheduleId).ToList());

        // Sequential, not Task.WhenAll — each ToResponseAsync makes a cross-module call and they share this request's scoped DbContexts.
        var responses = new List<RentScheduleResponse>();
        foreach (var item in items)
        {
            responses.Add(await ToResponseAsync(item, totals.GetValueOrDefault(item.RentScheduleId)));
        }

        return ServiceResult<PagedResponse<RentScheduleResponse>>.Success(
            new PagedResponse<RentScheduleResponse>(responses, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<RentScheduleResponse>> GetByIdAsync(Guid rentScheduleId)
    {
        var schedule = await scheduleRepository.GetByIdAsync(rentScheduleId);
        return schedule is null
            ? ServiceResult<RentScheduleResponse>.NotFound()
            : ServiceResult<RentScheduleResponse>.Success(await ToResponseAsync(schedule));
    }

    public async Task<ServiceResult<IReadOnlyList<RentScheduleResponse>>> ListArrearsAsync()
    {
        var active = await scheduleRepository.ListActiveForTenantAsync();
        var totals = await paymentRepository.GetReceivedTotalsAsync(active.Select(s => s.RentScheduleId).ToList());

        var inArrears = active
            .Select(s => (Schedule: s, Received: totals.GetValueOrDefault(s.RentScheduleId)))
            .Select(x => (x.Schedule, x.Received, Evaluation: x.Schedule.Evaluate(x.Received, Today)))
            .Where(x => x.Evaluation.Status == RentStatus.InArrears)
            .OrderBy(x => x.Evaluation.OldestUnpaidDueDate)
            .ToList();

        var responses = new List<RentScheduleResponse>();
        foreach (var (schedule, received, _) in inArrears)
        {
            responses.Add(await ToResponseAsync(schedule, received));
        }

        return ServiceResult<IReadOnlyList<RentScheduleResponse>>.Success(responses);
    }

    public async Task<int?> CountInArrearsAsync()
    {
        if (!await moduleEntitlementService.IsEntitledAsync(TenantContext.Current!.TenantId, ModuleKey))
        {
            return null;
        }

        var active = await scheduleRepository.ListActiveForTenantAsync();
        var totals = await paymentRepository.GetReceivedTotalsAsync(active.Select(s => s.RentScheduleId).ToList());

        return active.Count(s => s.Evaluate(totals.GetValueOrDefault(s.RentScheduleId), Today).Status == RentStatus.InArrears);
    }

    public async Task<ServiceResult<bool>> EnsureScheduleForLeaseAsync(
        Guid tenantId, Guid unitAssetId, Guid leaseApplicationId, Guid leaseId, decimal rentAmount, string currency, DateOnly startDate, Guid? actorUserId)
    {
        if (!await moduleEntitlementService.IsEntitledAsync(tenantId, ModuleKey))
        {
            return ServiceResult<bool>.Success(false);
        }

        if (await scheduleRepository.GetByLeaseApplicationIdAsync(tenantId, leaseApplicationId) is not null)
        {
            return ServiceResult<bool>.Success(false);
        }

        var actorType = actorUserId is null ? AuditActorType.System : AuditActorType.HumanUser;
        var actorId = actorUserId ?? tenantId;

        var prior = await scheduleRepository.GetActiveForUnitAsync(tenantId, unitAssetId);
        if (prior is not null)
        {
            // The previous tenancy on this unit is over — a signed lease must never be blocked by it. Saved on its own first: the partial unique index is checked per statement, so ending must land before the new insert.
            prior.End();
            await scheduleRepository.SaveChangesAsync();
            await auditService.RecordAsync(
                tenantId, actorId, actorType, AuditTypes.RentLedger.RentScheduleEnded,
                $"{{\"rentScheduleId\":\"{prior.RentScheduleId}\",\"reason\":\"superseded by a new lease\"}}");
        }

        var schedule = RentSchedule.Create(
            tenantId, unitAssetId, leaseApplicationId, leaseId, rentAmount, currency, startDate,
            Math.Min(startDate.Day, MaxDerivedDueDay), options.Value.DefaultGracePeriodDays, actorUserId);

        await scheduleRepository.AddAsync(schedule);

        try
        {
            await scheduleRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery of the same webhook won the race — the schedule exists, which is the goal state.
            return await scheduleRepository.GetByLeaseApplicationIdAsync(tenantId, leaseApplicationId) is not null
                ? ServiceResult<bool>.Success(false)
                : ServiceResult<bool>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.UnitAlreadyHasActiveSchedule);
        }

        await auditService.RecordAsync(
            tenantId, actorId, actorType, AuditTypes.RentLedger.RentScheduleCreated,
            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\",\"leaseApplicationId\":\"{leaseApplicationId}\"}}");

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> EndForLeaseAsync(Guid tenantId, Guid leaseApplicationId, Guid? actorUserId)
    {
        if (!await moduleEntitlementService.IsEntitledAsync(tenantId, ModuleKey))
        {
            return ServiceResult<bool>.Success(false);
        }

        var schedule = await scheduleRepository.GetByLeaseApplicationIdAsync(tenantId, leaseApplicationId);
        if (schedule is null || schedule.Status != RentScheduleStatus.Active)
        {
            return ServiceResult<bool>.Success(false);
        }

        schedule.End();
        await scheduleRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, actorUserId ?? tenantId, actorUserId is null ? AuditActorType.System : AuditActorType.HumanUser,
            AuditTypes.RentLedger.RentScheduleEnded,
            $"{{\"rentScheduleId\":\"{schedule.RentScheduleId}\",\"reason\":\"lease ended\"}}");

        return ServiceResult<bool>.Success(true);
    }

    public async Task<OwnerRentSummary?> GetOwnerRentSummaryAsync(
        IReadOnlyList<Guid> unitAssetIds, DateOnly periodStart, DateOnly periodEnd, DateOnly asOf)
    {
        if (!await moduleEntitlementService.IsEntitledAsync(TenantContext.Current!.TenantId, ModuleKey))
        {
            return null;
        }

        if (unitAssetIds.Count == 0)
        {
            return new OwnerRentSummary(0, 0, 0, []);
        }

        var schedules = await scheduleRepository.ListForUnitsAsync(unitAssetIds);
        var ids = schedules.Select(s => s.RentScheduleId).ToList();
        var totals = await paymentRepository.GetReceivedTotalsAsync(ids);

        int current = 0, overdue = 0, arrears = 0;
        foreach (var schedule in schedules.Where(s => s.Status == RentScheduleStatus.Active))
        {
            switch (schedule.Evaluate(totals.GetValueOrDefault(schedule.RentScheduleId), asOf).Status)
            {
                case RentStatus.Current: current++; break;
                case RentStatus.Overdue: overdue++; break;
                case RentStatus.InArrears: arrears++; break;
            }
        }

        var currencies = schedules.ToDictionary(s => s.RentScheduleId, s => s.Currency);
        var received = await paymentRepository.GetReceivedInPeriodByCurrencyAsync(currencies, periodStart, periodEnd);
        var byCurrency = received.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new CurrencyAmount(kv.Key, kv.Value)).ToList();
        return new OwnerRentSummary(current, overdue, arrears, byCurrency);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// The unit's active lease if it has one (a Leasing tenant turning Rent Collection on later);
    /// otherwise a new MANUAL lease from the request's term, which BDR-LSE-009 requires. If the
    /// schedule insert later loses a race, that lease stays — but the race it lost is the same
    /// one-per-unit race the lease's own unique index guards, so in practice the lease insert
    /// fails first.
    /// </summary>
    private async Task<ServiceResult<LeaseResponse>> ResolveLeaseForManualScheduleAsync(RentScheduleCreateRequest request, Guid createdBy)
    {
        var active = await leaseService.GetActiveForUnitAsync(request.UnitAssetId);
        if (active.Outcome == ServiceResultOutcome.Success)
        {
            return active;
        }

        var monthToMonth = request.LeaseMonthToMonth == true;
        if (!monthToMonth && request.LeaseEndDate is null)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.RentLedger.LeaseTermRequired);
        }

        return await leaseService.StartAsync(
            new LeaseStartRequest(request.UnitAssetId, null, request.StartDate, true, monthToMonth ? null : request.LeaseEndDate), createdBy);
    }

    private async Task<RentScheduleResponse> ToResponseAsync(RentSchedule schedule, decimal? totalReceived = null)
    {
        var received = totalReceived
            ?? (await paymentRepository.GetReceivedTotalsAsync([schedule.RentScheduleId])).GetValueOrDefault(schedule.RentScheduleId);

        var unit = await unitAssetService.GetByIdAsync(schedule.UnitAssetId);
        var unitLabel = unit.Outcome == ServiceResultOutcome.Success ? unit.Data!.Label : "Unknown unit";

        var lease = await leaseService.GetByIdAsync(schedule.LeaseId);
        var leaseSource = lease.Outcome == ServiceResultOutcome.Success ? lease.Data!.Source : null;

        var evaluation = schedule.Status == RentScheduleStatus.Active ? schedule.Evaluate(received, Today) : null;

        return new RentScheduleResponse(
            schedule.RentScheduleId, schedule.UnitAssetId, unitLabel, schedule.LeaseApplicationId, schedule.LeaseId, leaseSource, schedule.RentAmount,
            schedule.Currency, schedule.StartDate, schedule.DueDayOfMonth, schedule.GracePeriodDays,
            schedule.Status.ToUpperSnakeCase(), evaluation?.Status.ToUpperSnakeCase(), evaluation?.PeriodsInArrears ?? 0,
            evaluation?.AmountShort ?? 0m, received, evaluation?.OldestUnpaidDueDate,
            scheduleRepository.GetRowVersion(schedule), schedule.CreatedAt);
    }
}
