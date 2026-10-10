using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Services;

/// <summary>
/// BDR-LSE-005/006/007/009 — the one place the whole e-signature completion converges: file the signed
/// lease (Document), start the lease record with the term staff set when sending (PropertyAsset,
/// which also flips the unit to occupied), start its rent schedule
/// (RentLedger, BDR-RENT-001), close out the listing and application (Leasing), and write the
/// audit trail.
///
/// The <c>TenantContext.Current = ...</c> below is deliberate and NOT a repeat of the rejected
/// Phase 8 share-link pattern. There, setting it was unnecessary (Reporting's own lookups never
/// used Scoped()). Here it is required: ILeaseService.StartAsync and IDocumentService
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
    ILeaseService leaseService,
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

        var uploadedBy = application.SentForSignatureBy
            ?? throw new InvalidOperationException($"Application {application.ApplicationId} has no SentForSignatureBy.");

        if (application.SignedLeaseVersionId is null)
        {
            var signedBytes = await eSignatureProvider.DownloadSignedDocumentAsync(envelopeId);
            var fileName = $"lease-{application.ApplicationId}.pdf";

            // A real staff actor (whoever triggered send-for-signature), never an invented system user.
            // The executed lease becomes the next VERSION of the draft staff sent (BDR-LSE-006), keeping
            // draft and signed copy as one document. Applications sent before the draft was tracked fall
            // back to filing a new document.
            Guid signedLeaseVersionId;
            if (application.DraftDocumentId is { } draftDocumentId)
            {
                var version = await documentService.AddVersionAsync(
                    draftDocumentId, new MemoryStream(signedBytes), fileName, "application/pdf", signedBytes.Length, uploadedBy);
                if (version.Outcome != ServiceResultOutcome.Success)
                {
                    throw new InvalidOperationException($"Filing the signed lease failed: {version.Outcome} {version.ErrorCode}.");
                }

                signedLeaseVersionId = version.Data!.DocumentVersionId;
            }
            else
            {
                var documentResult = await documentService.CreateAsync(
                    new DocumentUploadRequest("LEASE", null, null, listing.UnitAssetId, null, null),
                    new MemoryStream(signedBytes), fileName, "application/pdf", signedBytes.Length, uploadedBy);
                if (documentResult.Outcome != ServiceResultOutcome.Success)
                {
                    throw new InvalidOperationException($"Filing the signed lease failed: {documentResult.Outcome} {documentResult.ErrorCode}.");
                }

                signedLeaseVersionId = documentResult.Data!.CurrentVersion?.DocumentVersionId ?? documentResult.Data.DocumentId;
            }

            // Saved on its own, before the remaining steps: if a later step fails and the provider retries,
            // the lease must not be filed a second time.
            application.RecordSignedLeaseFiled(signedLeaseVersionId);
            await applicationRepository.SaveChangesAsync();
        }

        // Idempotent by application id, so a retry after a partial failure gets the same lease back.
        // Applications sent before the term was captured start a NOT_RECORDED lease from the
        // listing's availability date; staff record the real term afterwards (BDR-LSE-009).
        var termRecorded = application.LeaseStartDate is not null;
        var leaseStart = application.LeaseStartDate ?? listing.AvailableFrom;
        var lease = await leaseService.StartAsync(
            new LeaseStartRequest(listing.UnitAssetId, application.ApplicationId, leaseStart, termRecorded, application.LeaseEndDate),
            uploadedBy);
        if (lease.Outcome != ServiceResultOutcome.Success)
        {
            throw new InvalidOperationException($"Starting the lease failed: {lease.Outcome} {lease.ErrorCode}.");
        }

        // BDR-RENT-001 — must land BEFORE the Leasing save below: after it the application is Leased,
        // so a webhook retry would hit the early return above and never create the schedule. The
        // call is idempotent by lease application id, so a retry after a later failure is safe.
        var schedule = await rentScheduleService.EnsureScheduleForLeaseAsync(
            application.TenantId, listing.UnitAssetId, application.ApplicationId, lease.Data!.LeaseId, listing.RentAmount, listing.Currency,
            leaseStart, uploadedBy);
        if (schedule.Outcome != ServiceResultOutcome.Success)
        {
            throw new InvalidOperationException($"Creating the rent schedule failed: {schedule.Outcome} {schedule.ErrorCode}.");
        }

        listing.MarkLeased();
        application.MarkLeased();

        await eventRepository.AddAsync(ApplicationEvent.Create(
            application.TenantId, application.ApplicationId, ApplicationEventTypes.SignatureCompleted,
            ApplicationEventActorType.System, null, $"{{\"envelopeId\":\"{envelopeId}\",\"draftDocumentId\":\"{application.DraftDocumentId}\",\"signedLeaseVersionId\":\"{application.SignedLeaseVersionId}\"}}"));
        await eventRepository.AddAsync(ApplicationEvent.Create(
            application.TenantId, application.ApplicationId, ApplicationEventTypes.Leased,
            ApplicationEventActorType.System, null, null));
        await applicationRepository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, uploadedBy, AuditActorType.System, AuditTypes.Leasing.LeaseExecuted,
            $"{{\"applicationId\":\"{application.ApplicationId}\",\"unitAssetId\":\"{listing.UnitAssetId}\"}}");
    }
}
