using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// BDR-LSE-010. An offer can be made on any ACTIVE lease, LEASING or MANUAL — renewing a tenancy
/// doesn't depend on how it was won. Accepting one extends that same lease record (PropertyAsset)
/// and, for a new rent, adds a dated rent change to its schedule (Rent Collection), so periods
/// before the change keep their old rent; no new application is involved.
/// </summary>
public sealed class RenewalOfferService(
    IRenewalOfferRepository repository,
    ILeaseService leaseService,
    IRentScheduleService rentScheduleService,
    ITenantSettingsService tenantSettingsService,
    IAuditService auditService) : IRenewalOfferService
{
    public async Task<ServiceResult<RenewalOfferResponse>> CreateAsync(RenewalOfferCreateRequest request, Guid actorUserId)
    {
        var leaseResult = await leaseService.GetByIdAsync(request.LeaseId);
        if (leaseResult.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<RenewalOfferResponse>.NotFound();
        }

        var lease = leaseResult.Data!;
        if (lease.Status != "ACTIVE")
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.LeaseNotActive);
        }

        if (await repository.HasOpenOfferForLeaseAsync(request.LeaseId))
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.RenewalAlreadyOffered);
        }

        if (!EnumStringConverter.TryParseUpperSnakeCase<RenewalTerm>(request.ProposedTerm, out var term)
            || !ExtendsLease(lease, term, request.ProposedEndDate, request.ProposedRentFrom))
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
        }

        RenewalOffer offer;
        try
        {
            offer = RenewalOffer.Create(
                TenantContext.Current!.TenantId, lease.LeaseId, lease.UnitAssetId, term, request.ProposedEndDate,
                request.ProposedRentAmount, request.ProposedRentFrom, request.RespondBy, Today, actorUserId);
        }
        catch (ArgumentException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
        }

        await repository.AddAsync(offer);
        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race against the one-open-offer-per-lease partial unique index.
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.RenewalAlreadyOffered);
        }

        await auditService.RecordAsync(
            offer.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Leasing.RenewalOffered,
            $"{{\"renewalOfferId\":\"{offer.RenewalOfferId}\",\"leaseId\":\"{offer.LeaseId}\"}}");

        return ServiceResult<RenewalOfferResponse>.Success(ToResponse(offer, lease));
    }

    public async Task<ServiceResult<RenewalOfferResponse>> AcceptAsync(Guid renewalOfferId, RenewalOfferAcceptRequest request, Guid actorUserId)
    {
        var offer = await repository.GetByIdAsync(renewalOfferId);
        if (offer is null)
        {
            return ServiceResult<RenewalOfferResponse>.NotFound();
        }

        if (offer.Status != RenewalOfferStatus.Offered)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        var leaseResult = await leaseService.GetByIdAsync(offer.LeaseId);
        if (leaseResult.Outcome != ServiceResultOutcome.Success || leaseResult.Data!.Status != "ACTIVE")
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.LeaseNotActive);
        }

        // Re-checked against the lease as it is now (its term may have been recorded since the
        // offer), before anything changes — so a rejected acceptance leaves rent and lease alone.
        if (!ExtendsLease(leaseResult.Data, offer.ProposedTerm, offer.ProposedEndDate, offer.ProposedRentFrom))
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
        }

        // Rent first: it's idempotent by date, so if the lease step then failed, a retry repeats it harmlessly.
        bool? rentChangeApplied = null;
        if (offer.ProposedRentAmount is { } rent && offer.ProposedRentFrom is { } rentFrom)
        {
            var change = await rentScheduleService.ScheduleRentChangeForLeaseAsync(
                offer.TenantId, offer.LeaseId, rentFrom, rent, offer.RenewalOfferId, actorUserId);
            if (change.Outcome != ServiceResultOutcome.Success)
            {
                return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(change.ErrorCode ?? ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
            }

            rentChangeApplied = change.Data;
        }

        var renewed = await leaseService.RenewAsync(
            offer.LeaseId, new LeaseRenewRequest(offer.ProposedTerm.ToUpperSnakeCase(), offer.ProposedEndDate), actorUserId);
        if (renewed.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(renewed.ErrorCode ?? ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
        }

        try
        {
            offer.Accept(request.RespondedOn, Today, rentChangeApplied, actorUserId);
        }
        catch (ArgumentException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidRenewalTerms);
        }

        return await SaveOutcomeAsync(offer, renewed.Data!, AuditTypes.Leasing.RenewalAccepted, actorUserId);
    }

    public async Task<ServiceResult<RenewalOfferResponse>> DeclineAsync(Guid renewalOfferId, RenewalOfferDeclineRequest request, Guid actorUserId)
    {
        var offer = await repository.GetByIdAsync(renewalOfferId);
        if (offer is null)
        {
            return ServiceResult<RenewalOfferResponse>.NotFound();
        }

        var reasons = await tenantSettingsService.GetRenewalDeclineReasonsAsync(offer.TenantId);
        try
        {
            offer.Decline(request.RespondedOn, Today, request.Reason, request.Note, reasons.ToList(), actorUserId);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }
        catch (ArgumentException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidDeclineReason);
        }

        return await SaveOutcomeAsync(offer, null, AuditTypes.Leasing.RenewalDeclined, actorUserId);
    }

    public async Task<ServiceResult<RenewalOfferResponse>> ExpireAsync(Guid renewalOfferId, Guid actorUserId)
    {
        var offer = await repository.GetByIdAsync(renewalOfferId);
        if (offer is null)
        {
            return ServiceResult<RenewalOfferResponse>.NotFound();
        }

        try
        {
            offer.Expire(Today, actorUserId);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }
        catch (ArgumentException)
        {
            return ServiceResult<RenewalOfferResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.RespondByNotPassed);
        }

        return await SaveOutcomeAsync(offer, null, AuditTypes.Leasing.RenewalExpired, actorUserId);
    }

    public async Task<ServiceResult<RenewalOfferResponse>> GetByIdAsync(Guid renewalOfferId)
    {
        var offer = await repository.GetByIdAsync(renewalOfferId);
        return offer is null
            ? ServiceResult<RenewalOfferResponse>.NotFound()
            : ServiceResult<RenewalOfferResponse>.Success(ToResponse(offer, await LeaseOrNullAsync(offer.LeaseId)));
    }

    public async Task<ServiceResult<IReadOnlyList<RenewalOfferResponse>>> ListAsync(Guid? leaseId, string? status)
    {
        RenewalOfferStatus? statusFilter = null;
        if (status is not null)
        {
            if (!EnumStringConverter.TryParseUpperSnakeCase<RenewalOfferStatus>(status, out var parsed))
            {
                return ServiceResult<IReadOnlyList<RenewalOfferResponse>>.Success([]);
            }

            statusFilter = parsed;
        }

        var offers = await repository.ListAsync(leaseId, statusFilter);

        // Sequential, not Task.WhenAll — the lease lookups share this request's scoped DbContexts.
        var leases = new Dictionary<Guid, LeaseResponse?>();
        var responses = new List<RenewalOfferResponse>();
        foreach (var offer in offers)
        {
            if (!leases.TryGetValue(offer.LeaseId, out var lease))
            {
                lease = await LeaseOrNullAsync(offer.LeaseId);
                leases[offer.LeaseId] = lease;
            }

            responses.Add(ToResponse(offer, lease));
        }

        return ServiceResult<IReadOnlyList<RenewalOfferResponse>>.Success(responses);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// The same rules Lease.Renew applies, checked up front: a fixed renewal ends after the current
    /// term (or the start, with no fixed end yet); month-to-month only from a lease that isn't
    /// already; and a new rent can't apply before the lease began.
    /// </summary>
    private static bool ExtendsLease(LeaseResponse lease, RenewalTerm term, DateOnly? proposedEndDate, DateOnly? proposedRentFrom)
    {
        var termOk = term == RenewalTerm.Fixed
            ? proposedEndDate > (lease.EndDate ?? lease.StartDate)
            : lease.TermType != "MONTH_TO_MONTH";
        return termOk && (proposedRentFrom is null || proposedRentFrom > lease.StartDate);
    }

    private async Task<ServiceResult<RenewalOfferResponse>> SaveOutcomeAsync(
        RenewalOffer offer, LeaseResponse? lease, string auditType, Guid actorUserId)
    {
        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<RenewalOfferResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            offer.TenantId, actorUserId, AuditActorType.HumanUser, auditType,
            $"{{\"renewalOfferId\":\"{offer.RenewalOfferId}\",\"leaseId\":\"{offer.LeaseId}\"}}");

        return ServiceResult<RenewalOfferResponse>.Success(ToResponse(offer, lease ?? await LeaseOrNullAsync(offer.LeaseId)));
    }

    private async Task<LeaseResponse?> LeaseOrNullAsync(Guid leaseId)
    {
        var lease = await leaseService.GetByIdAsync(leaseId);
        return lease.Outcome == ServiceResultOutcome.Success ? lease.Data : null;
    }

    private static RenewalOfferResponse ToResponse(RenewalOffer offer, LeaseResponse? lease) => new(
        offer.RenewalOfferId, offer.LeaseId, offer.UnitAssetId, lease?.UnitLabel ?? "Unknown unit", lease?.PropertyAddress ?? "Unknown property",
        lease?.EndDate, offer.ProposedTerm.ToUpperSnakeCase(), offer.ProposedEndDate, offer.ProposedRentAmount, offer.ProposedRentFrom,
        offer.RespondBy, offer.Status == RenewalOfferStatus.Offered && Today > offer.RespondBy, offer.Status.ToUpperSnakeCase(),
        offer.OutcomeOn, offer.DeclineReason, offer.DeclineNote, offer.RentChangeApplied, offer.CreatedAt);
}
