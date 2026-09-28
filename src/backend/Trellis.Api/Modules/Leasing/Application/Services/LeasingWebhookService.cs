using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// BDR-LSE-005/006/007 — the one place the whole e-signature completion converges: file the signed
/// lease (Document), flip the unit to occupied (PropertyAsset), start its rent schedule
/// (RentLedger, BDR-RENT-001), close out the listing and application (Leasing), and write the
/// audit trail.
///
/// The <c>TenantContext.Current = ...</c> below is deliberate and NOT a repeat of the rejected
/// Phase 8 share-link pattern. There, setting it was unnecessary (Reporting's own lookups never
/// used Scoped()). Here it is required: IUnitAssetService.MarkOccupiedAsync and IDocumentService
/// .CreateAsync are already-shipped, unchanged services whose internals call Scoped()/
/// TenantContext.Current!.TenantId directly, so they need the owning tenant set to find and write
/// the right rows. TenantContext is AsyncLocal and request-scoped, so this can't leak into any
/// other concurrent request. Any failure throws (the controller returns 500 and the provider
/// retries) rather than silently half-completing.
/// </summary>
public sealed class LeasingWebhookService(
    IApplicationRepository applicationRepository,
    IVacancyListingRepository listingRepository,
    IApplicationEventRepository eventRepository,
    IESignatureProvider eSignatureProvider,
    IDocumentService documentService,
    IUnitAssetService unitAssetService,
    IRentScheduleService rentScheduleService,
    IAuditService auditService) : ILeasingWebhookService
{
    public async Task ProcessSignatureCompletedAsync(string envelopeId, string rawPayloadJson)
    {
        var application = await applicationRepository.GetByEnvelopeIdAcrossTenantsAsync(envelopeId);
        if (application is null || application.Status == ApplicationStatus.Leased)
        {
            // Unknown envelope, or already completed (an idempotent replay) — nothing to do.
            return;
        }

        TenantContext.Current = new TenantContext(application.TenantId);

        var listing = await listingRepository.GetByIdAsync(application.VacancyListingId)
            ?? throw new InvalidOperationException($"Vacancy listing {application.VacancyListingId} not found for application {application.ApplicationId}.");

        var signedBytes = await eSignatureProvider.DownloadSignedDocumentAsync(envelopeId);
        var uploadedBy = application.SentForSignatureBy
            ?? throw new InvalidOperationException($"Application {application.ApplicationId} has no SentForSignatureBy.");

        // A real staff actor (whoever triggered send-for-signature), never an invented system user.
        var documentResult = await documentService.CreateAsync(
            new DocumentUploadRequest("LEASE", null, null, listing.UnitAssetId, null, null),
            new MemoryStream(signedBytes), $"lease-{application.ApplicationId}.pdf", "application/pdf", signedBytes.Length, uploadedBy);
        if (documentResult.Outcome != ServiceResultOutcome.Success)
        {
            throw new InvalidOperationException($"Filing the signed lease failed: {documentResult.Outcome} {documentResult.ErrorCode}.");
        }

        var occupied = await unitAssetService.MarkOccupiedAsync(listing.UnitAssetId);
        if (occupied.Outcome is not (ServiceResultOutcome.Success or ServiceResultOutcome.BusinessRuleViolation))
        {
            // BusinessRuleViolation = already OCCUPIED (a retry after a partial failure) — fine, the goal state holds.
            throw new InvalidOperationException($"Marking the unit occupied failed: {occupied.Outcome}.");
        }

        // BDR-RENT-001 — must land BEFORE the Leasing save below: after it the application is Leased,
        // so a webhook retry would hit the early return above and never create the schedule. The
        // call is idempotent by lease application id, so a retry after a later failure is safe.
        var schedule = await rentScheduleService.EnsureScheduleForLeaseAsync(
            application.TenantId, listing.UnitAssetId, application.ApplicationId, listing.RentAmount, listing.Currency,
            listing.AvailableFrom, uploadedBy);
        if (schedule.Outcome != ServiceResultOutcome.Success)
        {
            throw new InvalidOperationException($"Creating the rent schedule failed: {schedule.Outcome} {schedule.ErrorCode}.");
        }

        listing.MarkLeased();
        application.MarkLeased();

        await eventRepository.AddAsync(ApplicationEvent.Create(
            application.TenantId, application.ApplicationId, ApplicationEventTypes.SignatureCompleted,
            ApplicationEventActorType.System, null, $"{{\"envelopeId\":\"{envelopeId}\",\"documentId\":\"{documentResult.Data!.DocumentId}\"}}"));
        await eventRepository.AddAsync(ApplicationEvent.Create(
            application.TenantId, application.ApplicationId, ApplicationEventTypes.Leased,
            ApplicationEventActorType.System, null, null));
        await applicationRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, uploadedBy, AuditActorType.System, AuditTypes.Leasing.LeaseExecuted,
            $"{{\"applicationId\":\"{application.ApplicationId}\",\"unitAssetId\":\"{listing.UnitAssetId}\"}}");
    }
}
