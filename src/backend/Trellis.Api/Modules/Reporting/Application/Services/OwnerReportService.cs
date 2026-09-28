using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Reporting.Application.Services;

/// <summary>
/// BDR-OWN-002/003/004 — assembles an OwnerReport from four cross-module Application Services
/// (own-service-exposes-what-it-owns, no shared read model), renders it once via
/// IReportExportService, and stores the PDF bytes so re-download never re-runs the aggregate.
/// Runs synchronously within the request — no background job, matching the exit criterion's own
/// "generated without a Trellis engineer hand-assembling it," not "generated asynchronously."
/// </summary>
public sealed class OwnerReportService(
    IOwnerReportRepository repository,
    IReportExportService exportService,
    IAuditService auditService,
    INotificationService notificationService,
    ITenantService tenantService,
    IOwnerService ownerService,
    IPropertyService propertyService,
    IWorkflowInstanceService workflowInstanceService,
    IDocumentService documentService) : IOwnerReportService
{
    /// <summary>Same 30-day lookahead DashboardService uses, applied relative to the report's own PeriodEnd (not "today") so a report generated for a past period stays internally consistent on re-download.</summary>
    private const int ExpiryLookaheadDays = 30;

    public async Task<ServiceResult<OwnerReportResponse>> GenerateAsync(
        Guid ownerId, OwnerReportPeriodType periodType, DateOnly periodStart, DateOnly periodEnd, Guid generatedBy)
    {
        var ownerResult = await ownerService.GetByIdAsync(ownerId);
        if (ownerResult.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<OwnerReportResponse>.NotFound();
        }

        var tenantId = TenantContext.Current!.TenantId;
        var owner = ownerResult.Data!;

        var report = OwnerReport.Create(tenantId, ownerId, owner.Name, periodType, periodStart, periodEnd, generatedBy);
        await repository.AddAsync(report);
        await repository.SaveChangesAsync();

        try
        {
            var propertyIds = await propertyService.ListPropertyIdsByOwnerAsync(ownerId);
            var (totalCost, openCount) = await workflowInstanceService.GetOwnerMaintenanceSummaryAsync(propertyIds, periodStart, periodEnd);
            var (compliantCount, expiringCount) = await CountDocumentComplianceAsync(ownerId, periodEnd);

            var tenantResult = await tenantService.GetByIdAsync(tenantId);
            var tenantName = tenantResult.Outcome == ServiceResultOutcome.Success ? tenantResult.Data!.TenantName : string.Empty;

            var content = new OwnerReportContent(
                tenantName, owner.Name, report.PeriodType.ToUpperSnakeCase(), periodStart, periodEnd,
                propertyIds.Count, totalCost, openCount, compliantCount, expiringCount);

            var pdfBytes = exportService.GenerateOwnerReportPdf(content);

            report.MarkReady(propertyIds.Count, totalCost, openCount, compliantCount, expiringCount, pdfBytes);
            await repository.SaveChangesAsync();
        }
        catch (Exception)
        {
            report.MarkFailed();
            await repository.SaveChangesAsync();
            throw;
        }

        await auditService.RecordAsync(
            tenantId, generatedBy, AuditActorType.HumanUser, AuditTypes.Reporting.OwnerReportGenerated,
            $"{{\"ownerReportId\":\"{report.OwnerReportId}\"}}");

        return ServiceResult<OwnerReportResponse>.Success(ToResponse(report));
    }

    public async Task<ServiceResult<OwnerReportResponse>> GetByIdAsync(Guid ownerReportId)
    {
        var report = await repository.GetByIdAsync(ownerReportId);
        return report is null ? ServiceResult<OwnerReportResponse>.NotFound() : ServiceResult<OwnerReportResponse>.Success(ToResponse(report));
    }

    public async Task<ServiceResult<PagedResponse<OwnerReportResponse>>> ListByOwnerAsync(Guid? ownerId, string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(ownerId, decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].OwnerReportId)
            : null;

        return ServiceResult<PagedResponse<OwnerReportResponse>>.Success(
            new PagedResponse<OwnerReportResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<byte[]>> DownloadPdfAsync(Guid ownerReportId)
    {
        var report = await repository.GetByIdAsync(ownerReportId);
        if (report is null || report.Status != OwnerReportStatus.Ready || report.PdfContent is null)
        {
            return ServiceResult<byte[]>.NotFound();
        }

        return ServiceResult<byte[]>.Success(report.PdfContent);
    }

    public async Task<ServiceResult<byte[]>> DownloadCsvAsync(Guid ownerReportId)
    {
        var report = await repository.GetByIdAsync(ownerReportId);
        if (report is null || report.Status != OwnerReportStatus.Ready)
        {
            return ServiceResult<byte[]>.NotFound();
        }

        var content = new OwnerReportContent(
            string.Empty, report.OwnerName, report.PeriodType.ToUpperSnakeCase(), report.PeriodStart, report.PeriodEnd,
            report.PropertyCount, report.TotalMaintenanceCost, report.OpenMaintenanceCount,
            report.CompliantDocumentCount, report.ExpiringDocumentCount);

        return ServiceResult<byte[]>.Success(exportService.GenerateOwnerReportCsv(content));
    }

    public async Task<ServiceResult<bool>> DeliverByEmailAsync(Guid ownerReportId, Guid actorId)
    {
        var report = await repository.GetByIdAsync(ownerReportId);
        if (report is null || report.Status != OwnerReportStatus.Ready)
        {
            return ServiceResult<bool>.NotFound();
        }

        var ownerResult = await ownerService.GetByIdAsync(report.OwnerId);
        if (ownerResult.Outcome != ServiceResultOutcome.Success || string.IsNullOrWhiteSpace(ownerResult.Data!.Email))
        {
            return ServiceResult<bool>.BusinessRuleViolation(ErrorCodeRegistry.Reporting.OwnerHasNoEmail);
        }

        await notificationService.SendAsync(
            report.TenantId,
            "OWNER_REPORT_READY",
            ownerResult.Data!.Email!,
            new { report.OwnerName, report.PeriodStart, report.PeriodEnd },
            new RelatedEntityRef("OwnerReport", report.OwnerReportId));

        await auditService.RecordAsync(
            report.TenantId, actorId, AuditActorType.HumanUser, AuditTypes.Reporting.OwnerReportDelivered,
            $"{{\"ownerReportId\":\"{report.OwnerReportId}\"}}");

        return ServiceResult<bool>.Success(true);
    }

    private async Task<(int Compliant, int Expiring)> CountDocumentComplianceAsync(Guid ownerId, DateOnly asOfDate)
    {
        var documentsResult = await documentService.ListByOwnerAsync(ownerId, null, 1000);
        if (documentsResult.Outcome != ServiceResultOutcome.Success)
        {
            return (0, 0);
        }

        var lookahead = asOfDate.AddDays(ExpiryLookaheadDays);
        var active = documentsResult.Data!.Items.Where(d => d.Status == "ACTIVE").ToList();
        var expiring = active.Count(d => d.ExpiresAt is { } e && e <= lookahead);
        var compliant = active.Count - expiring;

        return (compliant, expiring);
    }

    private static OwnerReportResponse ToResponse(OwnerReport report) => new(
        report.OwnerReportId,
        report.OwnerId,
        report.OwnerName,
        report.PeriodType.ToUpperSnakeCase(),
        report.PeriodStart,
        report.PeriodEnd,
        report.Status.ToUpperSnakeCase(),
        report.PropertyCount,
        report.TotalMaintenanceCost,
        report.OpenMaintenanceCount,
        report.CompliantDocumentCount,
        report.ExpiringDocumentCount,
        report.CreatedAt,
        report.ReadyAt);
}
