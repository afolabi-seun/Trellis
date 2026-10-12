namespace Trellis.Api.Modules.RentLedger.Application.Dtos;

/// <summary>
/// The lease term (BDR-LSE-009) is needed only when the unit has no active lease yet, which is
/// when this also starts one: LeaseEndDate for a fixed term, or LeaseMonthToMonth true. When the
/// unit already has an active lease (a Leasing tenant turning on Rent Collection later), the
/// schedule attaches to it and both are ignored.
/// </summary>
public sealed record RentScheduleCreateRequest(
    Guid UnitAssetId, decimal RentAmount, string Currency, DateOnly StartDate, int DueDayOfMonth, int? GracePeriodDays,
    DateOnly? LeaseEndDate = null, bool? LeaseMonthToMonth = null);

/// <summary>Required when the schedule's lease is MANUAL: ending that schedule ends the tenancy, so it needs BDR-LSE-011's reason and party. Ignored for a Leasing-started lease, which is ended through Leasing.</summary>
public sealed record RentScheduleEndRequest(DateOnly EffectiveDate, string Reason, string EndedBy, string? Note);

public sealed record RentScheduleUpdateRequest(decimal RentAmount, int DueDayOfMonth, int GracePeriodDays, string RowVersion);

/// <summary>RentAmount is the rent in effect today; UpcomingRentAmount/UpcomingRentFrom are the next dated change, if one is scheduled (BDR-LSE-010). LeaseSource is MANUAL when ending this schedule also ends the tenancy (and so needs a RentScheduleEndRequest), LEASING when the lease is ended through Leasing. RentStatus is null for an ENDED schedule (it no longer accrues anything). TotalReceived is the sum of non-voided payment records — money recorded as received outside Trellis, never money Trellis holds.</summary>
public sealed record RentScheduleResponse(
    Guid RentScheduleId, Guid UnitAssetId, string UnitLabel, Guid? LeaseApplicationId, Guid LeaseId, string? LeaseSource, decimal RentAmount, string Currency,
    DateOnly StartDate, int DueDayOfMonth, int GracePeriodDays, string Status, string? RentStatus, int PeriodsInArrears,
    decimal AmountShort, decimal TotalReceived, DateOnly? OldestUnpaidDueDate, string RowVersion, DateTimeOffset CreatedAt,
    decimal? UpcomingRentAmount, DateOnly? UpcomingRentFrom, PaymentTimelinessDto Timeliness);

public sealed record RentPaymentRecordCreateRequest(decimal AmountReceived, DateOnly DateReceived, string? MethodReference);

public sealed record RentPaymentRecordVoidRequest(string Reason);

public sealed record RentPaymentRecordResponse(
    Guid RentPaymentRecordId, Guid RentScheduleId, decimal AmountReceived, DateOnly DateReceived, string? MethodReference,
    Guid RecordedByUserId, DateTimeOffset RecordedAt, bool IsVoided, DateTimeOffset? VoidedAt, string? VoidReason);

public sealed record CurrencyAmount(string Currency, decimal Amount);

/// <summary>BDR-RENT-005 — the Owner report's rent rollup across an Owner's units. ReceivedByCurrency is grouped by each schedule's own currency and never summed across currencies — a KES schedule and a ZAR schedule under one Owner are reported as two separate figures, not one meaningless total.</summary>
public sealed record OwnerRentSummary(int CurrentCount, int OverdueCount, int ArrearsCount, IReadOnlyList<CurrencyAmount> ReceivedByCurrency);

/// <summary>
/// BDR-RENT-007. PeriodsAssessed counts periods whose grace deadline has passed; OnTimeRate is
/// PeriodsOnTime / PeriodsAssessed (null when nothing is assessed yet — no data, not 0%);
/// AverageDaysLate averages days from due date to the date a late period was covered (or to today
/// while uncovered) across late periods only (null when none were late).
/// </summary>
public sealed record PaymentTimelinessDto(int PeriodsAssessed, int PeriodsOnTime, decimal? OnTimeRate, decimal? AverageDaysLate);

public sealed record PropertyPaymentTimelinessDto(Guid PropertyId, PaymentTimelinessDto Timeliness);

/// <summary>BDR-RENT-007 for the Owner report: periods due in the report period, across all the Owner's schedules, and per property — each summed from period counts, never averaged from percentages.</summary>
public sealed record RentTimelinessSummary(PaymentTimelinessDto Overall, IReadOnlyList<PropertyPaymentTimelinessDto> ByProperty);
