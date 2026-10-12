using Trellis.Api.Modules.RentLedger.Application.Dtos;

namespace Trellis.Api.Modules.Reporting.Application.Dtos;

public sealed record OwnerReportCreateRequest(Guid OwnerId, string PeriodType, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record OwnerReportResponse(
    Guid OwnerReportId,
    Guid OwnerId,
    string OwnerName,
    string PeriodType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    int PropertyCount,
    decimal TotalMaintenanceCost,
    string MaintenanceCurrency,
    int OpenMaintenanceCount,
    int CompliantDocumentCount,
    int ExpiringDocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    OwnerReportRentSection? Rent = null);

/// <summary>
/// BDR-RENT-005 — rent status at generation time. Null when the tenant isn't entitled to Rent
/// Collection (the report then omits the section). ReceivedByCurrency is money recorded as received
/// outside Trellis (BDR-RENT-006), grouped by each schedule's own currency and never summed across
/// currencies — an empty list, not a zero, means nothing was recorded in the period.
/// </summary>
public sealed record OwnerReportRentSection(
    int CurrentCount, int OverdueCount, int ArrearsCount, IReadOnlyList<CurrencyAmount> ReceivedByCurrency, DateOnly AsOf,
    OwnerReportPaymentTimeliness? Timeliness = null);

/// <summary>
/// BDR-RENT-007 — for periods due within the report period whose grace had passed by generation:
/// overall and per property, summed from period counts. Null on reports generated before it existed.
/// </summary>
public sealed record OwnerReportPaymentTimeliness(PaymentTimelinessDto Overall, IReadOnlyList<OwnerReportPropertyTimeliness> ByProperty);

public sealed record OwnerReportPropertyTimeliness(Guid PropertyId, string PropertyAddress, PaymentTimelinessDto Timeliness);

/// <summary>The internal content set IReportExportService renders from — pure data, no DB dependency, so the PDF/CSV renderers stay independently unit-testable.</summary>
public sealed record OwnerReportContent(
    string TenantName,
    string OwnerName,
    string PeriodType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int PropertyCount,
    decimal TotalMaintenanceCost,
    string MaintenanceCurrency,
    int OpenMaintenanceCount,
    int CompliantDocumentCount,
    int ExpiringDocumentCount,
    OwnerReportRentSection? Rent = null);
