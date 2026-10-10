using Trellis.Api.Core.Data;
using Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

namespace Trellis.Api.Modules.PropertyAsset.Domain.Entities;

public enum LeaseSource
{
    /// <summary>Executed through Leasing's application and e-signature flow.</summary>
    Leasing,

    /// <summary>Recorded by staff when starting a rent schedule without Leasing.</summary>
    Manual,
}

public enum LeaseTermType
{
    Fixed,
    MonthToMonth,

    /// <summary>A lease that predates term capture (BDR-LSE-009). Never guessed — staff record the real term later.</summary>
    NotRecorded,
}

public enum LeaseStatus
{
    Active,
    Ended,
}

/// <summary>BDR-LSE-011's fixed list. <see cref="NotRecorded"/> exists only for leases ended before the list did.</summary>
public enum LeaseEndReason
{
    NonRenewal,
    LesseeBreach,
    MutualAgreement,
    PropertySale,
    Renovation,
    Other,
    NotRecorded,
}

public enum LeaseEndedBy
{
    PmCompany,
    Lessee,
}

/// <summary>
/// The record of why a Unit/Asset is occupied: one lease per tenancy, whichever add-on started
/// it. It lives here, in Core, rather than in Leasing because Leasing and Rent Collection are
/// independently entitled add-ons — a tenant with only Rent Collection still has tenancies whose
/// term and end matter (BDR-LSE-009/011), and a tenant with only Leasing still has leases with no
/// rent schedule. PropertyAsset already owned occupancy, so the invariant is simply "a unit is
/// OCCUPIED exactly when it has an ACTIVE lease", enforced by LeaseService
/// and a partial unique index (one ACTIVE lease per unit). No lessee PII is held here: a
/// Leasing-sourced lease points at its application, which already owns the applicant's details
/// and their POPIA handling.
/// </summary>
public sealed class Lease : ITenantOwned
{
    public const int MaxEndNoteLength = 500;

    public Guid LeaseId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UnitAssetId { get; private set; }
    public LeaseSource Source { get; private set; }

    /// <summary>Set for <see cref="LeaseSource.Leasing"/> only — Leasing's application id, unique, which makes lease creation from the e-signature webhook idempotent.</summary>
    public Guid? LeaseApplicationId { get; private set; }

    public DateOnly StartDate { get; private set; }
    public LeaseTermType TermType { get; private set; }

    /// <summary>Set exactly when <see cref="TermType"/> is <see cref="LeaseTermType.Fixed"/>.</summary>
    public DateOnly? EndDate { get; private set; }

    public LeaseStatus Status { get; private set; }
    public DateOnly? EndedOn { get; private set; }
    public LeaseEndReason? EndReason { get; private set; }

    /// <summary>Null only for leases ended before BDR-LSE-011 existed.</summary>
    public LeaseEndedBy? EndedBy { get; private set; }

    public string? EndNote { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Lease() { }

    public static Lease Start(
        Guid tenantId, Guid unitAssetId, Guid? leaseApplicationId, DateOnly startDate,
        LeaseTermType termType, DateOnly? endDate, Guid? createdBy)
    {
        ValidateTerm(startDate, termType, endDate);

        var now = DateTimeOffset.UtcNow;
        return new Lease
        {
            LeaseId = Guid.NewGuid(),
            TenantId = tenantId,
            UnitAssetId = unitAssetId,
            Source = leaseApplicationId is null ? LeaseSource.Manual : LeaseSource.Leasing,
            LeaseApplicationId = leaseApplicationId,
            StartDate = startDate,
            TermType = termType,
            EndDate = endDate,
            Status = LeaseStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Records or corrects the term of an active lease — the only way a <see cref="LeaseTermType.NotRecorded"/> lease gets one.</summary>
    public void RecordTerm(LeaseTermType termType, DateOnly? endDate)
    {
        RequireActive(LeaseStatus.Active);
        if (termType == LeaseTermType.NotRecorded)
        {
            throw new ArgumentException("A term must be recorded as fixed or month-to-month.", nameof(termType));
        }

        ValidateTerm(StartDate, termType, endDate);

        TermType = termType;
        EndDate = endDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void End(DateOnly effectiveDate, LeaseEndReason reason, LeaseEndedBy endedBy, string? note)
    {
        RequireActive(LeaseStatus.Ended);

        if (reason == LeaseEndReason.NotRecorded)
        {
            throw new ArgumentException("A lease ended today must record its reason.", nameof(reason));
        }

        if (effectiveDate < StartDate)
        {
            throw new ArgumentException("A lease can't end before it started.", nameof(effectiveDate));
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reason == LeaseEndReason.Other && trimmed is null)
        {
            throw new ArgumentException("A note is required when the reason is Other.", nameof(note));
        }

        if (trimmed?.Length > MaxEndNoteLength)
        {
            throw new ArgumentException($"The note can't exceed {MaxEndNoteLength} characters.", nameof(note));
        }

        Status = LeaseStatus.Ended;
        EndedOn = effectiveDate;
        EndReason = reason;
        EndedBy = endedBy;
        EndNote = trimmed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Days from <paramref name="asOf"/> to the fixed end date — negative once it has passed with the lease still active. Null when there is no fixed end date.</summary>
    public int? DaysUntilEnd(DateOnly asOf) =>
        TermType == LeaseTermType.Fixed && EndDate is { } end ? end.DayNumber - asOf.DayNumber : null;

    private void RequireActive(LeaseStatus target)
    {
        if (Status != LeaseStatus.Active)
        {
            throw new InvalidStatusTransitionException(nameof(Lease), Status.ToString(), target.ToString());
        }
    }

    private static void ValidateTerm(DateOnly startDate, LeaseTermType termType, DateOnly? endDate)
    {
        switch (termType)
        {
            case LeaseTermType.Fixed when endDate is null:
                throw new ArgumentException("A fixed-term lease needs an end date.", nameof(endDate));
            case LeaseTermType.Fixed when endDate <= startDate:
                throw new ArgumentException("The end date must be after the start date.", nameof(endDate));
            case LeaseTermType.MonthToMonth or LeaseTermType.NotRecorded when endDate is not null:
                throw new ArgumentException("Only a fixed-term lease has an end date.", nameof(endDate));
        }
    }
}
