using Trellis.Api.Core.Data;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;
using Trellis.Api.Modules.RentLedger.Domain.ValueObjects;

namespace Trellis.Api.Modules.RentLedger.Domain.Entities;

public enum RentScheduleStatus
{
    Active,
    Ended,
}

/// <summary>
/// BDR-RENT-001 — the recurring rent expected for one occupied Unit/Asset. This records what is
/// expected; it says nothing about money moving through Trellis (BDR-RENT-006). Whether it is
/// current, overdue, or in arrears is never stored — it is computed on read by
/// <see cref="ValueObjects.RentStatusCalculator"/> from this row plus the recorded payments, so
/// no stored flag can drift from the records after a late payment is entered. StartDate is an
/// addition the design doc's column list omits: without it "how many periods are owed" is undefined.
/// One ACTIVE schedule per unit and one schedule per lease application are both Postgres partial
/// unique indexes in the owning migration (not expressible via EF Core's fluent API).
/// </summary>
public sealed class RentSchedule : ITenantOwned
{
    public Guid RentScheduleId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UnitAssetId { get; private set; }
    public Guid? LeaseApplicationId { get; private set; }

    /// <summary>The PropertyAsset lease this schedule tracks rent for — every schedule has one (BDR-LSE-009). Whether ending the schedule also ends the tenancy depends on that lease's source (MANUAL), not on this schedule.</summary>
    public Guid LeaseId { get; private set; }
    public decimal RentAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public int DueDayOfMonth { get; private set; }
    public int GracePeriodDays { get; private set; }
    public RentScheduleStatus Status { get; private set; }

    /// <summary>The due date of the latest past-grace unpaid period when the last arrears alert fired — a per-period key, not a once-ever timestamp, so a new missed month alerts again.</summary>
    public DateOnly? LastArrearsAlertedPeriod { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<RentChange> _rentChanges = [];

    /// <summary>Dated rent changes (BDR-LSE-010 renewals), loaded with the schedule. See <see cref="RentChange"/>.</summary>
    public IReadOnlyCollection<RentChange> RentChanges => _rentChanges;

    private RentSchedule() { }

    public static RentSchedule Create(
        Guid tenantId, Guid unitAssetId, Guid? leaseApplicationId, Guid leaseId, decimal rentAmount, string currency,
        DateOnly startDate, int dueDayOfMonth, int gracePeriodDays, Guid? createdBy)
    {
        Validate(rentAmount, dueDayOfMonth, gracePeriodDays);
        if (currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        var now = DateTimeOffset.UtcNow;
        return new RentSchedule
        {
            RentScheduleId = Guid.NewGuid(),
            TenantId = tenantId,
            UnitAssetId = unitAssetId,
            LeaseApplicationId = leaseApplicationId,
            LeaseId = leaseId,
            RentAmount = rentAmount,
            Currency = currency.ToUpperInvariant(),
            StartDate = startDate,
            DueDayOfMonth = dueDayOfMonth,
            GracePeriodDays = gracePeriodDays,
            Status = RentScheduleStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// A correction to the terms. The rent amount corrected is the one in effect today: the base
    /// amount, or the latest rent change already in effect — never a future change, and never a
    /// period's amount from before that.
    /// </summary>
    public void Update(decimal rentAmount, int dueDayOfMonth, int gracePeriodDays)
    {
        RequireActive(RentScheduleStatus.Active);
        Validate(rentAmount, dueDayOfMonth, gracePeriodDays);

        if (ChangeInEffectOn(DateOnly.FromDateTime(DateTime.UtcNow)) is { } inEffect)
        {
            inEffect.Correct(rentAmount);
        }
        else
        {
            RentAmount = rentAmount;
        }

        DueDayOfMonth = dueDayOfMonth;
        GracePeriodDays = gracePeriodDays;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void End()
    {
        RequireActive(RentScheduleStatus.Ended);

        Status = RentScheduleStatus.Ended;
        EndedAt = DateTimeOffset.UtcNow;
        UpdatedAt = EndedAt.Value;
    }

    /// <summary>
    /// BDR-LSE-010 — rent changes to <paramref name="rentAmount"/> for every period due on or after
    /// <paramref name="effectiveFrom"/>. Idempotent by date: a second change on the same date
    /// replaces the first, so a retried renewal acceptance never stacks two.
    /// </summary>
    public RentChange ScheduleRentChange(DateOnly effectiveFrom, decimal rentAmount, Guid? renewalOfferId, Guid createdBy)
    {
        RequireActive(RentScheduleStatus.Active);
        if (rentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be positive.", nameof(rentAmount));
        }

        if (effectiveFrom <= StartDate)
        {
            throw new ArgumentException("A rent change must take effect after the schedule starts.", nameof(effectiveFrom));
        }

        var existing = _rentChanges.FirstOrDefault(c => c.EffectiveFrom == effectiveFrom);
        if (existing is not null)
        {
            existing.Correct(rentAmount);
            UpdatedAt = DateTimeOffset.UtcNow;
            return existing;
        }

        var change = RentChange.Create(TenantId, RentScheduleId, effectiveFrom, rentAmount, renewalOfferId, createdBy);
        _rentChanges.Add(change);
        UpdatedAt = DateTimeOffset.UtcNow;
        return change;
    }

    /// <summary>The rent for a period due on <paramref name="dueDate"/>.</summary>
    public decimal RentAmountOn(DateOnly dueDate) => ChangeInEffectOn(dueDate)?.RentAmount ?? RentAmount;

    /// <summary>The earliest change still to come after <paramref name="asOf"/>, if any.</summary>
    public RentChange? NextRentChangeAfter(DateOnly asOf) =>
        _rentChanges.Where(c => c.EffectiveFrom > asOf).OrderBy(c => c.EffectiveFrom).FirstOrDefault();

    private RentChange? ChangeInEffectOn(DateOnly date) =>
        _rentChanges.Where(c => c.EffectiveFrom <= date).OrderByDescending(c => c.EffectiveFrom).FirstOrDefault();

    public void MarkArrearsAlerted(DateOnly period)
    {
        LastArrearsAlertedPeriod = period;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// BDR-RENT-007 — how promptly rent was recorded as received (non-voided payments only), for periods
    /// due between <paramref name="dueFrom"/> and <paramref name="dueTo"/> when given. An ended schedule
    /// is judged only up to the day it ended: no periods accrue after it.
    /// </summary>
    public PaymentTimeliness Timeliness(
        IReadOnlyList<(DateOnly DateReceived, decimal Amount)> payments, DateOnly asOf, DateOnly? dueFrom = null, DateOnly? dueTo = null)
    {
        var until = EndedAt is { } ended && DateOnly.FromDateTime(ended.UtcDateTime) < asOf
            ? DateOnly.FromDateTime(ended.UtcDateTime)
            : asOf;
        return PaymentTimelinessCalculator.Calculate(StartDate, DueDayOfMonth, GracePeriodDays, RentAmountOn, payments, until, dueFrom, dueTo);
    }

    /// <summary>Current / overdue / in-arrears as of a date, given the total recorded as received against this schedule (non-voided payments only).</summary>
    public RentEvaluation Evaluate(decimal totalReceived, DateOnly asOf) =>
        RentStatusCalculator.Evaluate(StartDate, DueDayOfMonth, GracePeriodDays, RentAmountOn, totalReceived, asOf);

    private void RequireActive(RentScheduleStatus target)
    {
        if (Status != RentScheduleStatus.Active)
        {
            throw new InvalidStatusTransitionException(nameof(RentSchedule), Status.ToString(), target.ToString());
        }
    }

    private static void Validate(decimal rentAmount, int dueDayOfMonth, int gracePeriodDays)
    {
        if (rentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be positive.", nameof(rentAmount));
        }

        if (dueDayOfMonth is < 1 or > 31)
        {
            throw new ArgumentException("Due day of month must be between 1 and 31.", nameof(dueDayOfMonth));
        }

        if (gracePeriodDays is < 0 or > 60)
        {
            throw new ArgumentException("Grace period must be between 0 and 60 days.", nameof(gracePeriodDays));
        }
    }
}
