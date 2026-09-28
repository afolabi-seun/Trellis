namespace Trellis.Api.Modules.Reporting.Application.Dtos;

/// <summary>BDR-RPT-001 — four tenant-wide operational counts, no dollar figures. BDR-RPT-003's department/assigned-property narrowing is a known, explicitly-flagged gap: no existing module exposes a "my department's work orders only" filter today, so this doesn't invent one ahead of a real BDR requiring it.</summary>
public sealed record DashboardSummaryResponse(
    int OpenWorkOrders, int ExpiringDocuments, int UpcomingInspections, int BottleneckedSteps);
