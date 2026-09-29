using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Core.Security;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;
using Trellis.Api.Modules.Leasing.Infrastructure.Security;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Api.Modules.Leasing.Application.Services;

public sealed class ApplicationService(
    IApplicationRepository repository,
    IVacancyListingRepository listingRepository,
    IScreeningResultRepository screeningResultRepository,
    IApplicationEventRepository eventRepository,
    IApplicationLinkTokenRepository tokenRepository,
    IApplicationLinkService applicationLinkService,
    IScreeningProvider screeningProvider,
    IESignatureProvider eSignatureProvider,
    ITenantService tenantService,
    IAuthService authService,
    ITenantSettingsService tenantSettingsService,
    INotificationService notificationService,
    IDocumentService documentService,
    IUnitAssetService unitAssetService,
    IRentScheduleService rentScheduleService,
    IEncryptionService encryptionService,
    IAuditService auditService) : IApplicationService
{
    public async Task<ServiceResult<ApplicationSubmitResponse>> SubmitAsync(string rawLinkToken, ApplicationSubmitRequest request)
    {
        // Resolving the token IS the caller's authentication AND BDR-LSE-008's email-control
        // verification. ResolveAsync sets TenantContext inside its own async frame, which doesn't
        // flow back here, so it is set again below from the trusted, server-resolved context.
        var linkResult = await applicationLinkService.ResolveAsync(rawLinkToken);
        if (linkResult.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<ApplicationSubmitResponse>.NotFound();
        }

        var context = linkResult.Data!;
        TenantContext.Current = new TenantContext(context.TenantId);

        var token = await tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash(rawLinkToken));
        var listing = await listingRepository.GetByIdForTenantAsync(context.TenantId, context.VacancyListingId);
        if (token is null || listing is null)
        {
            return ServiceResult<ApplicationSubmitResponse>.NotFound();
        }

        if (!listing.AcceptsApplications())
        {
            return ServiceResult<ApplicationSubmitResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.ListingNotAcceptingApplications);
        }

        // ApplicantEmail comes from the resolved token context ONLY — ApplicationSubmitRequest
        // deliberately has no email field at all (BDR-LSE-008).
        var application = LeasingApplication.Create(
            context.TenantId, context.VacancyListingId, context.ApplicantEmail, request.FullName, request.Phone,
            request.DateOfBirth, request.CurrentAddress, request.EmployerName, request.MonthlyIncome, request.ConsentToScreening);

        try
        {
            token.Consume(application.ApplicationId);
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<ApplicationSubmitResponse>.NotFound();
        }

        listing.MarkUnderApplication();
        await repository.AddAsync(application);
        await AddEventAsync(application, ApplicationEventTypes.LinkIssued, ApplicationEventActorType.System, null,
            $"{{\"createdAt\":\"{token.CreatedAt:O}\",\"expiresAt\":\"{token.ExpiresAt:O}\"}}");
        await AddEventAsync(application, ApplicationEventTypes.Submitted, ApplicationEventActorType.System, null, null);

        try
        {
            // One flush covers the new Application, the consumed token, the listing transition and
            // both events — they share this request's single LeasingDbContext. The token's xmin
            // makes a concurrent double-submit against one link fail here, not both succeed.
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<ApplicationSubmitResponse>.NotFound();
        }

        await auditService.RecordAsync(
            application.TenantId, application.ApplicationId, AuditActorType.System, AuditTypes.Leasing.ApplicationSubmitted,
            $"{{\"applicationId\":\"{application.ApplicationId}\"}}");

        await RunScreeningAsync(application);
        await NotifyStaffAsync(application, context.UnitLabel);

        return ServiceResult<ApplicationSubmitResponse>.Success(
            new ApplicationSubmitResponse(application.ApplicationId, application.Status.ToUpperSnakeCase()));
    }

    public async Task<ServiceResult<PagedResponse<ApplicationResponse>>> ListByListingAsync(Guid vacancyListingId, string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListByListingAsync(vacancyListingId, decoded, limit);
        var nextCursor = hasMore && items.Count > 0 ? CursorCodec.Encode(items[^1].SubmittedAt, items[^1].ApplicationId) : null;

        var responses = new List<ApplicationResponse>();
        foreach (var item in items)
        {
            responses.Add(await ToResponseAsync(item));
        }

        return ServiceResult<PagedResponse<ApplicationResponse>>.Success(
            new PagedResponse<ApplicationResponse>(responses, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<ApplicationResponse>> GetByIdAsync(Guid applicationId)
    {
        var application = await repository.GetByIdAsync(applicationId);
        return application is null
            ? ServiceResult<ApplicationResponse>.NotFound()
            : ServiceResult<ApplicationResponse>.Success(await ToResponseAsync(application));
    }

    public async Task<ServiceResult<IReadOnlyList<ApplicationEventResponse>>> GetEventsAsync(Guid applicationId)
    {
        if (await repository.GetByIdAsync(applicationId) is null)
        {
            return ServiceResult<IReadOnlyList<ApplicationEventResponse>>.NotFound();
        }

        var events = await eventRepository.ListForApplicationAsync(applicationId);
        return ServiceResult<IReadOnlyList<ApplicationEventResponse>>.Success(
            events.Select(e => new ApplicationEventResponse(
                e.ApplicationEventId, e.EventType, e.ActorType.ToUpperSnakeCase(), e.DetailJson, e.CreatedAt)).ToList());
    }

    public async Task<ServiceResult<ApplicationResponse>> ApproveAsync(Guid applicationId, Guid staffUserId)
    {
        var application = await repository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        try
        {
            application.Approve();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<ApplicationResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        await AddEventAsync(application, ApplicationEventTypes.Approved, ApplicationEventActorType.HumanUser, staffUserId, null);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, staffUserId, AuditActorType.HumanUser, AuditTypes.Leasing.ApplicationApproved,
            $"{{\"applicationId\":\"{application.ApplicationId}\"}}");

        return ServiceResult<ApplicationResponse>.Success(await ToResponseAsync(application));
    }

    public async Task<ServiceResult<ApplicationResponse>> DeclineAsync(Guid applicationId, Guid staffUserId)
    {
        var application = await repository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        try
        {
            application.Decline();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<ApplicationResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        await AddEventAsync(application, ApplicationEventTypes.Declined, ApplicationEventActorType.HumanUser, staffUserId, null);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, staffUserId, AuditActorType.HumanUser, AuditTypes.Leasing.ApplicationDeclined,
            $"{{\"applicationId\":\"{application.ApplicationId}\"}}");

        return ServiceResult<ApplicationResponse>.Success(await ToResponseAsync(application));
    }

    public async Task<ServiceResult<ApplicationResponse>> SendForSignatureAsync(Guid applicationId, Guid draftDocumentId, Guid staffUserId)
    {
        var application = await repository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        if (application.Status != ApplicationStatus.Approved || application.EnvelopeId is not null)
        {
            return ServiceResult<ApplicationResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.ApplicationNotReadyForSignature);
        }

        var draft = await documentService.GetByIdAsync(draftDocumentId);
        if (draft.Outcome != ServiceResultOutcome.Success)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        // The executed lease will be filed as this document's next version, so it must really be this unit's lease draft.
        var listing = await listingRepository.GetByIdAsync(application.VacancyListingId);
        if (listing is null || draft.Data!.DocumentType != "LEASE" || draft.Data.UnitAssetId != listing.UnitAssetId)
        {
            return ServiceResult<ApplicationResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.DraftDocumentInvalid);
        }

        var staff = await authService.GetUserByIdAsync(staffUserId);
        if (staff is null)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        var signers = new[]
        {
            new Signer(application.ApplicantFullName, application.ApplicantEmail, "TENANT"),
            new Signer(staff.Email, staff.Email, "LANDLORD_REP"),
        };

        var envelopeId = await eSignatureProvider.SendForSignatureAsync(draftDocumentId, signers);
        application.MarkSentForSignature(envelopeId, staffUserId, draftDocumentId);

        await AddEventAsync(application, ApplicationEventTypes.SentForSignature, ApplicationEventActorType.HumanUser, staffUserId,
            $"{{\"draftDocumentId\":\"{draftDocumentId}\",\"envelopeId\":\"{envelopeId}\"}}");
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, staffUserId, AuditActorType.HumanUser, AuditTypes.Leasing.LeaseSentForSignature,
            $"{{\"applicationId\":\"{application.ApplicationId}\",\"envelopeId\":\"{envelopeId}\"}}");

        return ServiceResult<ApplicationResponse>.Success(await ToResponseAsync(application));
    }

    public async Task<ServiceResult<ApplicationResponse>> EndLeaseAsync(Guid applicationId, EndLeaseRequest request, Guid staffUserId)
    {
        var application = await repository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ServiceResult<ApplicationResponse>.NotFound();
        }

        if (application.Status != ApplicationStatus.Leased)
        {
            return ServiceResult<ApplicationResponse>.BusinessRuleViolation(ErrorCodeRegistry.Leasing.InvalidStatusTransition);
        }

        var listing = await listingRepository.GetByIdAsync(application.VacancyListingId)
            ?? throw new InvalidOperationException($"Vacancy listing {application.VacancyListingId} not found for application {application.ApplicationId}.");

        // Ending the schedule and vacating the unit both tolerate "already done" — a retry after a
        // partial failure must still be able to complete, same posture as the lease-execution webhook.
        await rentScheduleService.EndForLeaseAsync(application.TenantId, application.ApplicationId, staffUserId);

        var vacated = await unitAssetService.MarkVacantAsync(listing.UnitAssetId);
        if (vacated.Outcome is not (ServiceResultOutcome.Success or ServiceResultOutcome.BusinessRuleViolation))
        {
            throw new InvalidOperationException($"Marking the unit vacant failed: {vacated.Outcome}.");
        }

        application.MarkLeaseEnded(request.EffectiveDate, request.Reason);
        await AddEventAsync(application, ApplicationEventTypes.LeaseEnded, ApplicationEventActorType.HumanUser, staffUserId,
            $"{{\"effectiveDate\":\"{request.EffectiveDate:O}\"}}");
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            application.TenantId, staffUserId, AuditActorType.HumanUser, AuditTypes.Leasing.LeaseEnded,
            $"{{\"applicationId\":\"{application.ApplicationId}\",\"unitAssetId\":\"{listing.UnitAssetId}\"}}");

        return ServiceResult<ApplicationResponse>.Success(await ToResponseAsync(application));
    }

    /// <summary>BDR-LSE-003 — triggered inline from a submitted application. A provider outage degrades to "screening skipped," never a failed submission: the application stays Submitted and staff may approve it directly (screening is Should-priority, not a hard prerequisite).</summary>
    private async Task RunScreeningAsync(LeasingApplication application)
    {
        if (!application.ConsentToScreening)
        {
            return;
        }

        var tenant = await tenantService.GetByIdAsync(application.TenantId);
        var details = new ApplicantDetails(
            application.ApplicantFullName, application.ApplicantEmail, application.ApplicantPhone,
            application.ApplicantDateOfBirth, application.ApplicantCurrentAddress, application.ApplicantEmployerName,
            application.ApplicantMonthlyIncome, tenant.Outcome == ServiceResultOutcome.Success ? tenant.Data!.CountryCode : string.Empty);

        ScreeningOutcome outcome;
        try
        {
            outcome = await screeningProvider.RequestScreeningAsync(details);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Nothing recorded — the application simply stays Submitted for staff to handle directly.
            return;
        }

        application.BeginScreening();
        await AddEventAsync(application, ApplicationEventTypes.ScreeningRequested, ApplicationEventActorType.System, null, null);

        // The provider hands back plaintext (ScreeningOutcome.FullReportJson) — it never touches
        // keys. Encrypted here, bound to this record via AAD, so a ciphertext copied onto another
        // application's row would fail to decrypt rather than silently decrypt as the wrong report.
        var ciphertext = outcome.FullReportJson is null
            ? null
            : await encryptionService.EncryptAsync(outcome.FullReportJson, $"{application.TenantId}:{application.ApplicationId}");
        await screeningResultRepository.AddAsync(ScreeningResult.Create(
            application.TenantId, application.ApplicationId, outcome.Provider, outcome.OutcomeSummary, ciphertext));
        await AddEventAsync(application, ApplicationEventTypes.ScreeningReceived, ApplicationEventActorType.System, null,
            $"{{\"provider\":\"{outcome.Provider}\",\"outcomeSummary\":\"{outcome.OutcomeSummary.ToUpperSnakeCase()}\"}}");
        await repository.SaveChangesAsync();

        await notificationService.SendAsync(
            application.TenantId, "SCREENING_COMPLETE", application.ApplicantEmail,
            new { ApplicantFullName = application.ApplicantFullName, UnitLabel = string.Empty },
            new RelatedEntityRef("Application", application.ApplicationId),
            NotificationRecipientType.ProspectiveTenant);
    }

    private async Task NotifyStaffAsync(LeasingApplication application, string unitLabel)
    {
        // The tenant chooses which staff roles hear about new applications (Tenant settings; default both).
        var recipients = await tenantSettingsService.GetNotificationSettingsAsync(application.TenantId);
        var staff = await authService.GetUsersByRolesAsync(application.TenantId, recipients.ApplicationSubmittedRoles);
        foreach (var user in staff)
        {
            await notificationService.SendAsync(
                application.TenantId, "APPLICATION_SUBMITTED", user.Email,
                new { UnitLabel = unitLabel, ApplicantFullName = application.ApplicantFullName },
                new RelatedEntityRef("Application", application.ApplicationId));
        }
    }

    private Task AddEventAsync(
        LeasingApplication application, string eventType, ApplicationEventActorType actorType, Guid? actorId, string? detailJson) =>
        eventRepository.AddAsync(ApplicationEvent.Create(application.TenantId, application.ApplicationId, eventType, actorType, actorId, detailJson));

    private async Task<ApplicationResponse> ToResponseAsync(LeasingApplication application)
    {
        var screening = await screeningResultRepository.GetLatestForApplicationAsync(application.ApplicationId);
        return new ApplicationResponse(
            application.ApplicationId, application.VacancyListingId, application.ApplicantEmail, application.ApplicantFullName,
            application.Status.ToUpperSnakeCase(), screening?.OutcomeSummary.ToUpperSnakeCase(), application.SubmittedAt);
    }
}
