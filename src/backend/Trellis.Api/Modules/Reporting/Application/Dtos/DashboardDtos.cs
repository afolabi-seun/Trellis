namespace Trellis.Api.Modules.Reporting.Application.Dtos;

/// <summary>
/// BDR-RPT-001 — tenant-wide operational counts, no dollar figures. BDR-RPT-003's
/// department/assigned-property narrowing is a known, explicitly-flagged gap: no existing module
/// exposes a "my department's work orders only" filter today, so this doesn't invent one ahead of
/// a real BDR requiring it. ArrearsSchedules is null, not zero, when the tenant isn't entitled to
/// RENT_COLLECTION — mirrors IRentScheduleService.CountInArrearsAsync's own null-not-zero
/// convention, so the tile is omitted rather than showing a misleading zero.
/// </summary>
public sealed record DashboardSummaryResponse(
    int OpenWorkOrders, int ExpiringDocuments, int UpcomingInspections, int BottleneckedSteps, int? ArrearsSchedules);
