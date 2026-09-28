namespace Trellis.Api.Modules.RentLedger.Application.Dtos;

public sealed record RentScheduleCreateRequest(
    Guid UnitAssetId, decimal RentAmount, string Currency, DateOnly StartDate, int DueDayOfMonth, int? GracePeriodDays);

public sealed record RentScheduleUpdateRequest(decimal RentAmount, int DueDayOfMonth, int GracePeriodDays, string RowVersion);

/// <summary>RentStatus is null for an ENDED schedule (it no longer accrues anything). TotalReceived is the sum of non-voided payment records — money recorded as received outside Trellis, never money Trellis holds.</summary>
public sealed record RentScheduleResponse(
    Guid RentScheduleId, Guid UnitAssetId, string UnitLabel, Guid? LeaseApplicationId, decimal RentAmount, string Currency,
    DateOnly StartDate, int DueDayOfMonth, int GracePeriodDays, string Status, string? RentStatus, int PeriodsInArrears,
    decimal AmountShort, decimal TotalReceived, DateOnly? OldestUnpaidDueDate, string RowVersion, DateTimeOffset CreatedAt);

public sealed record RentPaymentRecordCreateRequest(decimal AmountReceived, DateOnly DateReceived, string? MethodReference);

public sealed record RentPaymentRecordVoidRequest(string Reason);

public sealed record RentPaymentRecordResponse(
    Guid RentPaymentRecordId, Guid RentScheduleId, decimal AmountReceived, DateOnly DateReceived, string? MethodReference,
    Guid RecordedByUserId, DateTimeOffset RecordedAt, bool IsVoided, DateTimeOffset? VoidedAt, string? VoidReason);

/// <summary>BDR-RENT-005 — the Owner report's rent rollup across an Owner's units. ReceivedInPeriod sums recorded amounts as-is (no currency conversion — same single-currency assumption as the report's maintenance-cost figure).</summary>
public sealed record OwnerRentSummary(int CurrentCount, int OverdueCount, int ArrearsCount, decimal ReceivedInPeriod);
