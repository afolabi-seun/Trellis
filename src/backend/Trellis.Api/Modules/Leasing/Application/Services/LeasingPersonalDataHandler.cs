using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// A prospective-tenant/applicant subject — matched by ApplicantEmail across every Application
/// and ApplicationLinkToken row this tenant holds. application_event and audit_record are
/// deliberately untouched (IDs-only by design, security_guide.md); the executed lease Document
/// itself and rent schedules/payment records are also untouched — POPIA permits retention where
/// the law requires it, and neither embeds the applicant's email or free-text PII the way a
/// screening report or webhook payload can.
/// </summary>
public sealed class LeasingPersonalDataHandler(
    IApplicationRepository applicationRepository,
    IApplicationLinkTokenRepository tokenRepository,
    IScreeningResultRepository screeningResultRepository,
    ILeasingWebhookEventRepository webhookEventRepository) : IPersonalDataHandler
{
    public string ModuleName => "Leasing";

    public async Task<object?> ExportAsync(string subjectEmail)
    {
        var applications = await applicationRepository.ListByApplicantEmailAsync(subjectEmail);
        if (applications.Count == 0)
        {
            return null;
        }

        return applications.Select(a => new
        {
            a.ApplicationId,
            a.ApplicantFullName,
            a.ApplicantEmail,
            a.ApplicantPhone,
            a.ApplicantCurrentAddress,
            a.ApplicantEmployerName,
            a.ApplicantMonthlyIncome,
            Status = a.Status.ToString(),
            a.SubmittedAt,
        }).ToList();
    }

    public async Task<ErasureSummary> EraseAsync(string subjectEmail)
    {
        var applications = await applicationRepository.ListByApplicantEmailAsync(subjectEmail);
        foreach (var application in applications)
        {
            application.RedactApplicantPii();

            if (application.EnvelopeId is { } envelopeId)
            {
                // leasing_webhook_event's own "system_only" RLS (its doc comment) means an
                // ordinary TENANT_ADMIN-authenticated request context sees zero rows here without
                // this — the same bypass ESignatureWebhookController itself uses, narrowly scoped
                // to just this lookup+mutation, not the whole EraseAsync call.
                using var systemScope = SystemContext.Enter();
                var webhookEvent = await webhookEventRepository.GetByEnvelopeIdAsync(envelopeId);
                webhookEvent?.MarkPayloadRedacted();
                await webhookEventRepository.SaveChangesAsync();
            }

            var screening = await screeningResultRepository.GetLatestForApplicationAsync(application.ApplicationId);
            screening?.ClearFullReportCiphertext();
        }

        await applicationRepository.SaveChangesAsync();
        await screeningResultRepository.SaveChangesAsync();

        var tokens = await tokenRepository.ListByApplicantEmailAsync(subjectEmail);
        foreach (var token in tokens)
        {
            token.RedactApplicantEmail();
        }

        await tokenRepository.SaveChangesAsync();

        return new ErasureSummary(ModuleName, applications.Count + tokens.Count);
    }
}
