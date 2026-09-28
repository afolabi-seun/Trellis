using Microsoft.Extensions.Options;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;
using Trellis.Api.Modules.Reporting.Infrastructure.Security;

namespace Trellis.Api.Modules.Reporting.Application.Services;

public sealed class ReportShareLinkService(
    IReportShareLinkRepository shareLinkRepository,
    IOwnerReportRepository ownerReportRepository,
    IAuditService auditService,
    IOptions<ReportingOptions> options) : IReportShareLinkService
{
    public async Task<ServiceResult<ReportShareLinkResponse>> CreateAsync(Guid ownerReportId, Guid createdBy)
    {
        var report = await ownerReportRepository.GetByIdAsync(ownerReportId);
        if (report is null || report.Status != OwnerReportStatus.Ready)
        {
            return ServiceResult<ReportShareLinkResponse>.NotFound();
        }

        var rawToken = ShareLinkTokenGenerator.GenerateRawToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(options.Value.ShareLinkExpiryDays);
        var link = ReportShareLink.Create(report.TenantId, ownerReportId, ShareLinkTokenGenerator.Hash(rawToken), expiresAt, createdBy);

        await shareLinkRepository.AddAsync(link);
        await shareLinkRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            report.TenantId, createdBy, AuditActorType.HumanUser, AuditTypes.Reporting.ReportShareLinkCreated,
            $"{{\"reportShareLinkId\":\"{link.ReportShareLinkId}\",\"ownerReportId\":\"{ownerReportId}\"}}");

        return ServiceResult<ReportShareLinkResponse>.Success(
            new ReportShareLinkResponse(link.ReportShareLinkId, ownerReportId, rawToken, link.Status.ToUpperSnakeCase(), link.ExpiresAt, link.CreatedAt));
    }

    /// <summary>
    /// The anonymous path — every failure mode (bad token, expired, revoked, report not Ready)
    /// returns NotFound, one code path, no existence disclosure. Deliberately never touches
    /// TenantContext.Current/Scoped() — see IOwnerReportRepository.GetByIdForTenantAsync's own
    /// doc comment for why.
    /// </summary>
    public async Task<ServiceResult<(byte[] PdfContent, DateOnly PeriodStart)>> ResolveAsync(string rawToken)
    {
        var link = await shareLinkRepository.ResolveByTokenHashAsync(ShareLinkTokenGenerator.Hash(rawToken));
        if (link is null || !link.IsUsable())
        {
            return ServiceResult<(byte[], DateOnly)>.NotFound();
        }

        var report = await ownerReportRepository.GetByIdForTenantAsync(link.TenantId, link.OwnerReportId);
        if (report is null || report.Status != OwnerReportStatus.Ready || report.PdfContent is null)
        {
            return ServiceResult<(byte[], DateOnly)>.NotFound();
        }

        link.RecordAccess();
        await shareLinkRepository.SaveChangesAsync();

        return ServiceResult<(byte[], DateOnly)>.Success((report.PdfContent, report.PeriodStart));
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid reportShareLinkId)
    {
        var link = await shareLinkRepository.GetByIdAsync(reportShareLinkId);
        if (link is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        try
        {
            link.Revoke();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<bool>.BusinessRuleViolation(ErrorCodeRegistry.Reporting.InvalidStatusTransition);
        }

        await shareLinkRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            link.TenantId, link.CreatedBy, AuditActorType.HumanUser, AuditTypes.Reporting.ReportShareLinkRevoked,
            $"{{\"reportShareLinkId\":\"{link.ReportShareLinkId}\"}}");

        return ServiceResult<bool>.Success(true);
    }
}
