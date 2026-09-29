using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Core.Security;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Services;

public sealed class ScreeningReportService(
    IApplicationRepository applicationRepository,
    IScreeningResultRepository screeningResultRepository,
    IApplicationEventRepository eventRepository,
    IAuthService authService,
    IEncryptionService encryptionService,
    IAuditService auditService) : IScreeningReportService
{
    public async Task<ServiceResult<ScreeningReportResponse>> ViewAsync(Guid applicationId, Guid actorUserId, string currentPassword)
    {
        var application = await applicationRepository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ServiceResult<ScreeningReportResponse>.NotFound();
        }

        var screening = await screeningResultRepository.GetLatestForApplicationAsync(applicationId);
        if (screening?.FullReportCiphertext is null)
        {
            return ServiceResult<ScreeningReportResponse>.NotFound();
        }

        var reauth = await authService.ReauthenticateAsync(actorUserId, currentPassword);
        if (reauth.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<ScreeningReportResponse>.Forbidden(ErrorCodeRegistry.Auth.ReauthenticationFailed);
        }

        // Audit-before-plaintext: the view is recorded and durably saved before any decryption —
        // if either write below fails, the caller sees a 500 and the report is never decrypted,
        // never a race where plaintext escapes and the audit write only fails afterwards.
        await eventRepository.AddAsync(ApplicationEvent.Create(
            application.TenantId, application.ApplicationId, ApplicationEventTypes.ScreeningReportViewed,
            ApplicationEventActorType.HumanUser, actorUserId, null));
        await auditService.RecordAsync(
            application.TenantId, actorUserId, AuditActorType.HumanUser, AuditTypes.Leasing.ScreeningReportViewed,
            $"{{\"applicationId\":\"{applicationId}\"}}");
        await eventRepository.SaveChangesAsync();

        var reportJson = await encryptionService.DecryptAsync(
            screening.FullReportCiphertext, $"{application.TenantId}:{application.ApplicationId}");

        return ServiceResult<ScreeningReportResponse>.Success(new ScreeningReportResponse(
            screening.Provider, screening.OutcomeSummary.ToUpperSnakeCase(), reportJson, screening.ReceivedAt));
    }
}
