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
    int OpenMaintenanceCount,
    int CompliantDocumentCount,
    int ExpiringDocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    OwnerReportRentSection? Rent = null);

/// <summary>
/// BDR-RENT-005 — rent status at generation time. Null when the tenant isn't entitled to Rent
/// Collection (the report then omits the section). ReceivedInPeriod is money recorded as received
/// outside Trellis — Trellis processes and holds none (BDR-RENT-006).
/// </summary>
public sealed record OwnerReportRentSection(int CurrentCount, int OverdueCount, int ArrearsCount, decimal ReceivedInPeriod, DateOnly AsOf);

/// <summary>The internal content set IReportExportService renders from — pure data, no DB dependency, so the PDF/CSV renderers stay independently unit-testable.</summary>
public sealed record OwnerReportContent(
    string TenantName,
    string OwnerName,
    string PeriodType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int PropertyCount,
    decimal TotalMaintenanceCost,
    int OpenMaintenanceCount,
    int CompliantDocumentCount,
    int ExpiringDocumentCount,
    OwnerReportRentSection? Rent = null);
