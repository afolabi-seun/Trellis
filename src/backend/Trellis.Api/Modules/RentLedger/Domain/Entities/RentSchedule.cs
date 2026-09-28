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

    private RentSchedule() { }

    public static RentSchedule Create(
        Guid tenantId, Guid unitAssetId, Guid? leaseApplicationId, decimal rentAmount, string currency,
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

    public void Update(decimal rentAmount, int dueDayOfMonth, int gracePeriodDays)
    {
        RequireActive(RentScheduleStatus.Active);
        Validate(rentAmount, dueDayOfMonth, gracePeriodDays);

        RentAmount = rentAmount;
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

    public void MarkArrearsAlerted(DateOnly period)
    {
        LastArrearsAlertedPeriod = period;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Current / overdue / in-arrears as of a date, given the total recorded as received against this schedule (non-voided payments only).</summary>
    public RentEvaluation Evaluate(decimal totalReceived, DateOnly asOf) =>
        RentStatusCalculator.Evaluate(StartDate, DueDayOfMonth, GracePeriodDays, RentAmount, totalReceived, asOf);

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
