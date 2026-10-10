namespace Trellis.Api.Modules.PropertyAsset.Application.Dtos;

/// <summary>
/// Called by Leasing (on lease execution) and Rent Collection (on a manually started rent
/// schedule), never by a controller. A set <paramref name="LeaseApplicationId"/> makes the lease
/// LEASING-sourced and makes the call idempotent by that id. <paramref name="TermRecorded"/> false
/// is only for leases sent for signature before the term was captured (NOT_RECORDED); otherwise a
/// null <paramref name="EndDate"/> means month-to-month.
/// </summary>
public sealed record LeaseStartRequest(
    Guid UnitAssetId, Guid? LeaseApplicationId, DateOnly StartDate, bool TermRecorded, DateOnly? EndDate);

/// <summary>BDR-LSE-011. Reason: NON_RENEWAL, LESSEE_BREACH, MUTUAL_AGREEMENT, PROPERTY_SALE, RENOVATION, or OTHER (which needs a Note). EndedBy: PM_COMPANY or LESSEE.</summary>
public sealed record LeaseEndRequest(DateOnly EffectiveDate, string Reason, string EndedBy, string? Note);

/// <summary>TermType: FIXED (EndDate required) or MONTH_TO_MONTH (no EndDate).</summary>
public sealed record LeaseTermRequest(string TermType, DateOnly? EndDate, string RowVersion);

/// <summary>A Leasing-sourced lease's end note, for a POPIA export only — never logged, never returned anywhere else.</summary>
public sealed record LeaseEndNote(Guid LeaseId, Guid LeaseApplicationId, string EndNote);

/// <summary>
/// DaysUntilEnd is null unless the term is FIXED, and negative for an active lease whose end date
/// has passed. EndReason/EndedBy are null while active; EndReason is NOT_RECORDED and EndedBy null
/// for a lease ended before BDR-LSE-011 existed.
/// </summary>
public sealed record LeaseResponse(
    Guid LeaseId, Guid UnitAssetId, string UnitLabel, Guid PropertyId, string PropertyAddress, string Source,
    Guid? LeaseApplicationId, DateOnly StartDate, string TermType, DateOnly? EndDate, int? DaysUntilEnd,
    string Status, DateOnly? EndedOn, string? EndReason, string? EndedBy, string? EndNote, string RowVersion, DateTimeOffset CreatedAt);
