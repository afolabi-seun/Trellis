using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Infrastructure.Csv;
using QuestDocument = QuestPDF.Fluent.Document;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Pdf;

/// <summary>
/// BDR-OWN-004's Owner report PDF — QuestPDF's Community license (free under $1M USD annual
/// revenue, set once via QuestPDF.Settings.License in ReportingModuleExtensions) is a real
/// threshold, not a placeholder; flagged here explicitly as a future business decision, same
/// "swap point" convention as NoOpPaymentGateway's own doc comment. Also the sole
/// IReportExportService implementation — its CSV methods are thin wrappers around CsvWriter, not
/// QuestPDF-dependent, kept on this one class rather than split across two since the interface
/// itself is cohesive (one Owner report, two export formats). Aliased to QuestDocument: QuestPDF's
/// own `Document` class is otherwise ambiguous here against the sibling `Trellis.Api.Modules.
/// Document` module namespace, which C#'s enclosing-namespace lookup resolves ahead of a `using`.
/// </summary>
public sealed class QuestPdfOwnerReportRenderer : IReportExportService
{
    // Guarantees the license is set before the first PDF render regardless of caller — DI-resolved
    // production usage already sets it in ReportingModuleExtensions.AddReportingModule(), but a
    // unit test instantiates this class directly, with no DI container ever built, and QuestPDF
    // throws on an unset license — caught by this class's own unit test, not by any conditional
    // that could silently do nothing under Debug/Development.
    static QuestPdfOwnerReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    // Every currency-shaped figure goes through CurrencyFormatter now, which renders each amount in
    // its own ISO 4217 code's real culture (see that class's own doc comment for why "always en-US"
    // was a real bug). Every other ToString(...) call in this class stays on InvariantCulture, which
    // is fine for plain numbers/dates.

    public byte[] GenerateOwnerReportPdf(OwnerReportContent content) =>
        QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(content.TenantName).FontSize(16).Bold();
                    column.Item().Text($"Owner Report — {content.OwnerName}").FontSize(13);
                    column.Item().Text($"{content.PeriodType} · {content.PeriodStart:yyyy-MM-dd} to {content.PeriodEnd:yyyy-MM-dd}").FontSize(10);
                });

                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Text("Financial Summary").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        AddRow(table, "Properties", content.PropertyCount.ToString(CultureInfo.InvariantCulture));
                        AddRow(table, $"Total maintenance cost ({content.MaintenanceCurrency})", CurrencyFormatter.Format(content.TotalMaintenanceCost, content.MaintenanceCurrency));
                        AddRow(table, "Open maintenance requests", content.OpenMaintenanceCount.ToString(CultureInfo.InvariantCulture));
                    });

                    column.Item().PaddingTop(10).Text("Document Compliance").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        AddRow(table, "Compliant documents", content.CompliantDocumentCount.ToString(CultureInfo.InvariantCulture));
                        AddRow(table, "Documents expiring soon", content.ExpiringDocumentCount.ToString(CultureInfo.InvariantCulture));
                    });

                    if (content.Rent is { } rent)
                    {
                        // BDR-RENT-006 — worded as a record of payments received elsewhere, never as Trellis handling money.
                        column.Item().PaddingTop(10).Text("Rent Collection").FontSize(13).Bold();
                        column.Item().Text($"Payments recorded as received outside Trellis. Status as of {rent.AsOf:yyyy-MM-dd}.").FontSize(9);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            AddRow(table, "Rent schedules current", rent.CurrentCount.ToString(CultureInfo.InvariantCulture));
                            AddRow(table, "Rent schedules overdue", rent.OverdueCount.ToString(CultureInfo.InvariantCulture));
                            AddRow(table, "Rent schedules in arrears", rent.ArrearsCount.ToString(CultureInfo.InvariantCulture));
                            foreach (var received in rent.ReceivedByCurrency)
                            {
                                AddRow(table, $"Rent recorded as received in period ({received.Currency})", CurrencyFormatter.Format(received.Amount, received.Currency));
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ").FontSize(9);
                    x.Span(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).FontSize(9);
                });
            });
        }).GeneratePdf();

    public byte[] GenerateOwnerReportCsv(OwnerReportContent content)
    {
        List<IReadOnlyList<string>> rows =
        [
            ["Owner", content.OwnerName],
            ["Period", $"{content.PeriodStart:yyyy-MM-dd} to {content.PeriodEnd:yyyy-MM-dd}"],
            ["Properties", content.PropertyCount.ToString(CultureInfo.InvariantCulture)],
            [$"Total maintenance cost ({content.MaintenanceCurrency})", content.TotalMaintenanceCost.ToString("F2", CultureInfo.InvariantCulture)],
            ["Open maintenance requests", content.OpenMaintenanceCount.ToString(CultureInfo.InvariantCulture)],
            ["Compliant documents", content.CompliantDocumentCount.ToString(CultureInfo.InvariantCulture)],
            ["Documents expiring soon", content.ExpiringDocumentCount.ToString(CultureInfo.InvariantCulture)],
        ];

        if (content.Rent is { } rent)
        {
            rows.Add(["Rent schedules current", rent.CurrentCount.ToString(CultureInfo.InvariantCulture)]);
            rows.Add(["Rent schedules overdue", rent.OverdueCount.ToString(CultureInfo.InvariantCulture)]);
            rows.Add(["Rent schedules in arrears", rent.ArrearsCount.ToString(CultureInfo.InvariantCulture)]);
            foreach (var received in rent.ReceivedByCurrency)
            {
                rows.Add([$"Rent recorded as received in period ({received.Currency})", received.Amount.ToString("F2", CultureInfo.InvariantCulture)]);
            }

            rows.Add(["Rent status as of", rent.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);
        }

        return CsvWriter.Write(["Metric", "Value"], rows);
    }

    public byte[] GenerateDashboardCsv(DashboardSummaryResponse summary) => CsvWriter.Write(
        ["Metric", "Value"],
        [
            ["Open work orders", summary.OpenWorkOrders.ToString(CultureInfo.InvariantCulture)],
            ["Expiring documents", summary.ExpiringDocuments.ToString(CultureInfo.InvariantCulture)],
            ["Upcoming inspections", summary.UpcomingInspections.ToString(CultureInfo.InvariantCulture)],
            ["Bottlenecked steps", summary.BottleneckedSteps.ToString(CultureInfo.InvariantCulture)],
        ]);

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Text(label);
        table.Cell().AlignRight().Text(value);
    }
}
