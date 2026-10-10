using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum RenewalOfferStatus
{
    Offered,
    Accepted,
    Declined,
    Expired,
}

public enum RenewalTerm
{
    Fixed,
    MonthToMonth,
}

/// <summary>
/// BDR-LSE-010 — an offer to renew a lease (PropertyAsset's record), and what became of it. Staff
/// record both the offer and the lessee's answer: lessees have no platform access. At most one
/// OFFERED offer per lease at a time (a partial unique index). The decline reason is copied as text
/// from the tenant's configurable list at the time, so editing that list later never rewrites history.
/// </summary>
public sealed class RenewalOffer : ITenantOwned
{
    public const int MaxDeclineReasonLength = 100;
    public const int MaxNoteLength = 500;
    public const string OtherReason = "Other";

    public Guid RenewalOfferId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LeaseId { get; private set; }
    public Guid UnitAssetId { get; private set; }

    public RenewalTerm ProposedTerm { get; private set; }

    /// <summary>Set exactly when <see cref="ProposedTerm"/> is <see cref="RenewalTerm.Fixed"/>.</summary>
    public DateOnly? ProposedEndDate { get; private set; }

    /// <summary>Null means the rent stays as it is. When set, <see cref="ProposedRentFrom"/> says from which date.</summary>
    public decimal? ProposedRentAmount { get; private set; }
    public DateOnly? ProposedRentFrom { get; private set; }

    public DateOnly RespondBy { get; private set; }
    public RenewalOfferStatus Status { get; private set; }

    /// <summary>When the lessee answered (ACCEPTED/DECLINED), or when staff recorded it lapsed (EXPIRED).</summary>
    public DateOnly? OutcomeOn { get; private set; }
    public string? DeclineReason { get; private set; }
    public string? DeclineNote { get; private set; }

    /// <summary>On an accepted offer with a new rent: whether a rent schedule was actually changed (false when nothing tracks this lease's rent). Null otherwise.</summary>
    public bool? RentChangeApplied { get; private set; }

    public Guid OfferedBy { get; private set; }
    public Guid? OutcomeRecordedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RenewalOffer() { }

    public static RenewalOffer Create(
        Guid tenantId, Guid leaseId, Guid unitAssetId, RenewalTerm proposedTerm, DateOnly? proposedEndDate,
        decimal? proposedRentAmount, DateOnly? proposedRentFrom, DateOnly respondBy, DateOnly today, Guid offeredBy)
    {
        if ((proposedTerm == RenewalTerm.Fixed) != (proposedEndDate is not null))
        {
            throw new ArgumentException("A fixed renewal needs an end date; a month-to-month one has none.", nameof(proposedEndDate));
        }

        if ((proposedRentAmount is null) != (proposedRentFrom is null) || proposedRentAmount <= 0)
        {
            throw new ArgumentException("A new rent needs a positive amount and the date it applies from.", nameof(proposedRentAmount));
        }

        if (respondBy < today)
        {
            throw new ArgumentException("The respond-by date can't be in the past.", nameof(respondBy));
        }

        var now = DateTimeOffset.UtcNow;
        return new RenewalOffer
        {
            RenewalOfferId = Guid.NewGuid(),
            TenantId = tenantId,
            LeaseId = leaseId,
            UnitAssetId = unitAssetId,
            ProposedTerm = proposedTerm,
            ProposedEndDate = proposedEndDate,
            ProposedRentAmount = proposedRentAmount,
            ProposedRentFrom = proposedRentFrom,
            RespondBy = respondBy,
            Status = RenewalOfferStatus.Offered,
            OfferedBy = offeredBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Accept(DateOnly respondedOn, DateOnly today, bool? rentChangeApplied, Guid recordedBy)
    {
        RequireOffered(RenewalOfferStatus.Accepted);
        RequireNotFuture(respondedOn, today);
        RentChangeApplied = ProposedRentAmount is null ? null : rentChangeApplied;
        Close(RenewalOfferStatus.Accepted, respondedOn, recordedBy);
    }

    /// <summary><paramref name="allowedReasons"/> is the tenant's list at the time; "Other" is always allowed, and needs a note.</summary>
    public void Decline(DateOnly respondedOn, DateOnly today, string reason, string? note, IReadOnlyCollection<string> allowedReasons, Guid recordedBy)
    {
        RequireOffered(RenewalOfferStatus.Declined);
        RequireNotFuture(respondedOn, today);

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var isOther = string.Equals(reason, OtherReason, StringComparison.OrdinalIgnoreCase);
        var listed = allowedReasons.FirstOrDefault(r => string.Equals(r, reason, StringComparison.OrdinalIgnoreCase));
        if (!isOther && listed is null)
        {
            throw new ArgumentException("Choose a reason from the list, or Other.", nameof(reason));
        }

        if (isOther && trimmedNote is null)
        {
            throw new ArgumentException("A note is required when the reason is Other.", nameof(note));
        }

        if (trimmedNote?.Length > MaxNoteLength)
        {
            throw new ArgumentException($"The note can't exceed {MaxNoteLength} characters.", nameof(note));
        }

        DeclineReason = isOther ? OtherReason : listed;
        DeclineNote = trimmedNote;
        Close(RenewalOfferStatus.Declined, respondedOn, recordedBy);
    }

    /// <summary>Only once the respond-by date has passed — before that, the lessee can still answer.</summary>
    public void Expire(DateOnly today, Guid recordedBy)
    {
        RequireOffered(RenewalOfferStatus.Expired);
        if (today <= RespondBy)
        {
            throw new ArgumentException("An offer can only lapse after its respond-by date.", nameof(today));
        }

        Close(RenewalOfferStatus.Expired, today, recordedBy);
    }

    private void Close(RenewalOfferStatus status, DateOnly outcomeOn, Guid recordedBy)
    {
        Status = status;
        OutcomeOn = outcomeOn;
        OutcomeRecordedBy = recordedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void RequireOffered(RenewalOfferStatus target)
    {
        if (Status != RenewalOfferStatus.Offered)
        {
            throw new InvalidStatusTransitionException(nameof(RenewalOffer), Status.ToString(), target.ToString());
        }
    }

    private static void RequireNotFuture(DateOnly respondedOn, DateOnly today)
    {
        if (respondedOn > today)
        {
            throw new ArgumentException("The response date can't be in the future.", nameof(respondedOn));
        }
    }
}
