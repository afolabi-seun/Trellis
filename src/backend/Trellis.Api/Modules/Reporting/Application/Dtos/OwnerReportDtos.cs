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
    DateTimeOffset? ReadyAt);

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
    int ExpiringDocumentCount);
