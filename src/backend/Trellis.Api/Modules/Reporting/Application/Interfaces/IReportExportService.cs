using Trellis.Api.Modules.Reporting.Application.Dtos;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

/// <summary>Pure byte-generation, no DB dependency — independently unit-testable, and reusable for the dashboard's own CSV export.</summary>
public interface IReportExportService
{
    byte[] GenerateOwnerReportPdf(OwnerReportContent content);

    byte[] GenerateOwnerReportCsv(OwnerReportContent content);

    byte[] GenerateDashboardCsv(DashboardSummaryResponse summary);
}
