using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.PropertyAsset.Application.Services;

/// <summary>
/// BDR-LSE-009/011 and BDR-AST-005. The lease and its unit's occupancy are saved together through
/// the one PropertyDbContext both repositories share, so a lease can never be ACTIVE on a VACANT
/// unit or the other way round, even on a crash between the two writes.
/// </summary>
public sealed class LeaseService(
    ILeaseRepository repository,
    IUnitAssetRepository unitAssetRepository,
    IPropertyRepository propertyRepository,
    IAuditService auditService) : ILeaseService
{
    public async Task<ServiceResult<LeaseResponse>> StartAsync(LeaseStartRequest request, Guid? actorUserId)
    {
        if (request.LeaseApplicationId is { } applicationId && await repository.GetByLeaseApplicationIdAsync(applicationId) is { } existing)
        {
            return ServiceResult<LeaseResponse>.Success(await ToResponseAsync(existing));
        }

        var unit = await unitAssetRepository.GetByIdAsync(request.UnitAssetId);
        if (unit is null || unit.Status != UnitAssetStatus.Active)
        {
            return ServiceResult<LeaseResponse>.NotFound();
        }

        if (await repository.GetActiveForUnitAsync(request.UnitAssetId) is { } active)
        {
            if (request.LeaseApplicationId is null)
            {
                return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.UnitAlreadyLeased);
            }

            // A signed lease is never blocked by an earlier one — the same rule RentLedger applies
            // to the earlier rent schedule. Saved on its own first: the one-ACTIVE-per-unit partial
            // unique index is checked per statement, so the end must land before the new insert.
            await SupersedeAsync(active, request.StartDate, actorUserId);
        }

        var termType = !request.TermRecorded
            ? LeaseTermType.NotRecorded
            : request.EndDate is null ? LeaseTermType.MonthToMonth : LeaseTermType.Fixed;

        Lease lease;
        try
        {
            lease = Lease.Start(
                TenantContext.Current!.TenantId, request.UnitAssetId, request.LeaseApplicationId, request.StartDate,
                termType, request.EndDate, actorUserId);
        }
        catch (ArgumentException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseTerm);
        }

        // Units that predate lease records were all backfilled consistently, so an OCCUPIED unit
        // with no active lease shouldn't exist; if one does, starting a lease is still correct.
        if (unit.OccupancyStatus == UnitAssetOccupancyStatus.Vacant)
        {
            unit.MarkOccupied();
        }

        await repository.AddAsync(lease);

        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race on one of the partial unique indexes: a concurrent delivery of the same
            // webhook (goal state already reached), or another lease started on this unit.
            return request.LeaseApplicationId is { } raced && await repository.GetByLeaseApplicationIdAsync(raced) is { } winner
                ? ServiceResult<LeaseResponse>.Success(await ToResponseAsync(winner))
                : ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.UnitAlreadyLeased);
        }

        await auditService.RecordAsync(
            lease.TenantId, actorUserId ?? lease.TenantId, actorUserId is null ? AuditActorType.System : AuditActorType.HumanUser,
            AuditTypes.PropertyAsset.LeaseStarted,
            $"{{\"leaseId\":\"{lease.LeaseId}\",\"unitAssetId\":\"{lease.UnitAssetId}\",\"termType\":\"{lease.TermType.ToUpperSnakeCase()}\"}}");

        return ServiceResult<LeaseResponse>.Success(await ToResponseAsync(lease));
    }

    public async Task<ServiceResult<LeaseResponse>> EndAsync(Guid leaseId, LeaseEndRequest request, Guid actorUserId)
    {
        var lease = await repository.GetByIdAsync(leaseId);
        if (lease is null)
        {
            return ServiceResult<LeaseResponse>.NotFound();
        }

        if (!EnumStringConverter.TryParseUpperSnakeCase<LeaseEndReason>(request.Reason, out var reason)
            || !EnumStringConverter.TryParseUpperSnakeCase<LeaseEndedBy>(request.EndedBy, out var endedBy))
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseEnd);
        }

        try
        {
            lease.End(request.EffectiveDate, reason, endedBy, request.Note);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }
        catch (ArgumentException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseEnd);
        }

        var unit = await unitAssetRepository.GetByIdAsync(lease.UnitAssetId);
        if (unit?.OccupancyStatus == UnitAssetOccupancyStatus.Occupied)
        {
            unit.MarkVacant(request.EffectiveDate);
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            lease.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.PropertyAsset.LeaseEnded,
            $"{{\"leaseId\":\"{lease.LeaseId}\",\"unitAssetId\":\"{lease.UnitAssetId}\",\"reason\":\"{reason.ToUpperSnakeCase()}\",\"endedBy\":\"{endedBy.ToUpperSnakeCase()}\"}}");

        return ServiceResult<LeaseResponse>.Success(await ToResponseAsync(lease));
    }

    public async Task<ServiceResult<LeaseResponse>> RecordTermAsync(Guid leaseId, LeaseTermRequest request, Guid actorUserId)
    {
        var lease = await repository.GetByIdAsync(leaseId);
        if (lease is null)
        {
            return ServiceResult<LeaseResponse>.NotFound();
        }

        if (!EnumStringConverter.TryParseUpperSnakeCase<LeaseTermType>(request.TermType, out var termType))
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseTerm);
        }

        try
        {
            lease.RecordTerm(termType, request.EndDate);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }
        catch (ArgumentException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseTerm);
        }

        try
        {
            repository.SetExpectedRowVersion(lease, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<LeaseResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            lease.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.PropertyAsset.LeaseTermRecorded,
            $"{{\"leaseId\":\"{lease.LeaseId}\",\"termType\":\"{lease.TermType.ToUpperSnakeCase()}\"}}");

        return ServiceResult<LeaseResponse>.Success(await ToResponseAsync(lease));
    }

    public async Task<ServiceResult<LeaseResponse>> RenewAsync(Guid leaseId, LeaseRenewRequest request, Guid actorUserId)
    {
        var lease = await repository.GetByIdAsync(leaseId);
        if (lease is null)
        {
            return ServiceResult<LeaseResponse>.NotFound();
        }

        if (!EnumStringConverter.TryParseUpperSnakeCase<LeaseTermType>(request.TermType, out var termType))
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseTerm);
        }

        var previousEnd = lease.EndDate;
        try
        {
            lease.Renew(termType, request.NewEndDate);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidStatusTransition);
        }
        catch (ArgumentException)
        {
            return ServiceResult<LeaseResponse>.BusinessRuleViolation(ErrorCodeRegistry.PropertyAsset.InvalidLeaseTerm);
        }

        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            lease.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.PropertyAsset.LeaseRenewed,
            $"{{\"leaseId\":\"{lease.LeaseId}\",\"previousEndDate\":\"{previousEnd:O}\",\"termType\":\"{lease.TermType.ToUpperSnakeCase()}\",\"endDate\":\"{lease.EndDate:O}\"}}");

        return ServiceResult<LeaseResponse>.Success(await ToResponseAsync(lease));
    }

    public async Task<ServiceResult<LeaseResponse>> GetByIdAsync(Guid leaseId) =>
        await ToResultAsync(await repository.GetByIdAsync(leaseId));

    public async Task<ServiceResult<LeaseResponse>> GetByLeaseApplicationIdAsync(Guid leaseApplicationId) =>
        await ToResultAsync(await repository.GetByLeaseApplicationIdAsync(leaseApplicationId));

    public async Task<ServiceResult<LeaseResponse>> GetActiveForUnitAsync(Guid unitAssetId) =>
        await ToResultAsync(await repository.GetActiveForUnitAsync(unitAssetId));

    public async Task<ServiceResult<IReadOnlyList<LeaseResponse>>> ListExpiringAsync(int withinDays) =>
        await ToListResultAsync(await repository.ListActiveFixedEndingOnOrBeforeAsync(Today.AddDays(withinDays)));

    public async Task<ServiceResult<IReadOnlyList<LeaseResponse>>> ListTermNotRecordedAsync() =>
        await ToListResultAsync(await repository.ListActiveWithTermNotRecordedAsync());

    public async Task<IReadOnlyList<LeaseEndNote>> ListEndNotesForApplicationsAsync(IReadOnlyCollection<Guid> applicationIds)
    {
        if (applicationIds.Count == 0)
        {
            return [];
        }

        var leases = await repository.ListByLeaseApplicationIdsAsync(applicationIds);
        return leases
            .Where(l => l.EndNote is not null)
            .Select(l => new LeaseEndNote(l.LeaseId, l.LeaseApplicationId!.Value, l.EndNote!))
            .ToList();
    }

    public async Task<int> RedactEndNotesForApplicationsAsync(IReadOnlyCollection<Guid> applicationIds)
    {
        if (applicationIds.Count == 0)
        {
            return 0;
        }

        var leases = await repository.ListByLeaseApplicationIdsAsync(applicationIds);
        var redacted = leases.Count(l => l.RedactEndNote());
        if (redacted > 0)
        {
            await repository.SaveChangesAsync();
        }

        return redacted;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Ends the earlier lease the day before the new one starts (never before its own start). The unit stays OCCUPIED throughout.</summary>
    private async Task SupersedeAsync(Lease active, DateOnly newLeaseStart, Guid? actorUserId)
    {
        var endedOn = newLeaseStart.AddDays(-1) < active.StartDate ? active.StartDate : newLeaseStart.AddDays(-1);
        active.End(endedOn, LeaseEndReason.Other, LeaseEndedBy.PmCompany, Lease.SupersededEndNote);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            active.TenantId, actorUserId ?? active.TenantId, actorUserId is null ? AuditActorType.System : AuditActorType.HumanUser,
            AuditTypes.PropertyAsset.LeaseEnded,
            $"{{\"leaseId\":\"{active.LeaseId}\",\"unitAssetId\":\"{active.UnitAssetId}\",\"reason\":\"OTHER\",\"supersededBySignedLease\":true}}");
    }

    private async Task<ServiceResult<LeaseResponse>> ToResultAsync(Lease? lease) =>
        lease is null
            ? ServiceResult<LeaseResponse>.NotFound()
            : ServiceResult<LeaseResponse>.Success(await ToResponseAsync(lease));

    private async Task<ServiceResult<IReadOnlyList<LeaseResponse>>> ToListResultAsync(IReadOnlyList<Lease> leases)
    {
        // Sequential, not Task.WhenAll — the lookups share this request's scoped DbContext.
        var units = new Dictionary<Guid, UnitAsset?>();
        var properties = new Dictionary<Guid, Property?>();
        var responses = new List<LeaseResponse>();
        foreach (var lease in leases)
        {
            responses.Add(await ToResponseAsync(lease, units, properties));
        }

        return ServiceResult<IReadOnlyList<LeaseResponse>>.Success(responses);
    }

    private async Task<LeaseResponse> ToResponseAsync(
        Lease lease, Dictionary<Guid, UnitAsset?>? units = null, Dictionary<Guid, Property?>? properties = null)
    {
        units ??= [];
        properties ??= [];

        if (!units.TryGetValue(lease.UnitAssetId, out var unit))
        {
            unit = await unitAssetRepository.GetByIdAsync(lease.UnitAssetId);
            units[lease.UnitAssetId] = unit;
        }

        Property? property = null;
        if (unit is not null && !properties.TryGetValue(unit.PropertyId, out property))
        {
            property = await propertyRepository.GetByIdAsync(unit.PropertyId);
            properties[unit.PropertyId] = property;
        }

        return new LeaseResponse(
            lease.LeaseId, lease.UnitAssetId, unit?.Label ?? "Unknown unit", unit?.PropertyId ?? Guid.Empty,
            property?.Address ?? "Unknown property", lease.Source.ToUpperSnakeCase(), lease.LeaseApplicationId,
            lease.StartDate, lease.TermType.ToUpperSnakeCase(), lease.EndDate, lease.DaysUntilEnd(Today),
            lease.Status.ToUpperSnakeCase(), lease.EndedOn, lease.EndReason?.ToUpperSnakeCase(), lease.EndedBy?.ToUpperSnakeCase(),
            lease.EndNote, repository.GetRowVersion(lease), lease.CreatedAt);
    }
}
