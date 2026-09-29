using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;
using Trellis.Api.Modules.Leasing.Infrastructure.Security;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Tests.Leasing;

public sealed class ApplicationServiceTests : IDisposable
{
    private const string RawToken = "raw-token-value";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _listingId = Guid.NewGuid();

    private readonly IApplicationRepository _repository = Substitute.For<IApplicationRepository>();
    private readonly IVacancyListingRepository _listingRepository = Substitute.For<IVacancyListingRepository>();
    private readonly IScreeningResultRepository _screeningResultRepository = Substitute.For<IScreeningResultRepository>();
    private readonly IApplicationEventRepository _eventRepository = Substitute.For<IApplicationEventRepository>();
    private readonly IApplicationLinkTokenRepository _tokenRepository = Substitute.For<IApplicationLinkTokenRepository>();
    private readonly IApplicationLinkService _linkService = Substitute.For<IApplicationLinkService>();
    private readonly IScreeningProvider _screeningProvider = Substitute.For<IScreeningProvider>();
    private readonly IESignatureProvider _eSignatureProvider = Substitute.For<IESignatureProvider>();
    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private static readonly string[] AdminOnlyRoles = ["TENANT_ADMIN"];
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ITenantSettingsService _tenantSettingsService = Substitute.For<ITenantSettingsService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IRentScheduleService _rentScheduleService = Substitute.For<IRentScheduleService>();

    public void Dispose() => TenantContext.Current = null;

    private ApplicationService CreateSut() => new(
        _repository, _listingRepository, _screeningResultRepository, _eventRepository, _tokenRepository, _linkService,
        _screeningProvider, _eSignatureProvider, _tenantService, _authService, _tenantSettingsService, _notificationService,
        _documentService, _unitAssetService, _rentScheduleService, _auditService);

    private static ApplicationSubmitRequest ValidRequest(bool consent = true) =>
        new("Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", "Acme", 5000m, consent);

    /// <summary>Wires a fully valid link + listing, returning the real token/listing entities the service will mutate.</summary>
    private (ApplicationLinkToken Token, VacancyListing Listing) ArrangeValidLink(string applicantEmail = "verified@example.com")
    {
        var token = ApplicationLinkToken.Create(_tenantId, _listingId, applicantEmail, ApplicationLinkTokenGenerator.Hash(RawToken), DateTimeOffset.UtcNow.AddDays(7));
        var listing = VacancyListing.Create(_tenantId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());

        _linkService.ResolveAsync(RawToken).Returns(ServiceResult<ApplicationLinkContext>.Success(
            new ApplicationLinkContext(_tenantId, _listingId, applicantEmail, "Unit 4B", 1500m, "USD")));
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash(RawToken)).Returns(token);
        _listingRepository.GetByIdForTenantAsync(_tenantId, _listingId).Returns(listing);
        _tenantService.GetByIdAsync(_tenantId).Returns(ServiceResult<TenantResponse>.Success(
            new TenantResponse(_tenantId, "Acme PM", "ZA", "ACTIVE", "PROFESSIONAL", DateTimeOffset.UtcNow)));
        _screeningProvider.RequestScreeningAsync(Arg.Any<ApplicantDetails>())
            .Returns(new ScreeningOutcome("NOOP", ScreeningOutcomeSummary.Pass, null));
        _tenantSettingsService.GetNotificationSettingsAsync(_tenantId).Returns(
            new NotificationSettingsDto(["TENANT_ADMIN", "DEPARTMENT_STAFF"], ["TENANT_ADMIN", "DEPARTMENT_STAFF"]));
        _authService.GetUsersByRolesAsync(_tenantId, Arg.Any<IEnumerable<string>>()).Returns(
            (IReadOnlyList<AppUserSummary>)[new AppUserSummary(Guid.NewGuid(), "admin@example.com", _tenantId), new AppUserSummary(Guid.NewGuid(), "staff@example.com", _tenantId)]);

        return (token, listing);
    }

    [Fact]
    public async Task SubmitAsync_CopiesApplicantEmailFromTheResolvedToken_NeverFromTheRequest()
    {
        ArrangeValidLink("verified@example.com");
        LeasingApplication? saved = null;
        await _repository.AddAsync(Arg.Do<LeasingApplication>(a => saved = a));
        var sut = CreateSut();

        var result = await sut.SubmitAsync(RawToken, ValidRequest());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        saved!.ApplicantEmail.Should().Be("verified@example.com");
        typeof(ApplicationSubmitRequest).GetProperties().Should().NotContain(p => p.Name.Contains("Email"),
            "BDR-LSE-008: the submit request has no email field at all, so a client can't supply one");
    }

    [Fact]
    public async Task SubmitAsync_ConsumesTheToken_MovesTheListingUnderApplication_AndRunsScreening()
    {
        var (token, listing) = ArrangeValidLink();
        var sut = CreateSut();

        var result = await sut.SubmitAsync(RawToken, ValidRequest());

        result.Data!.Status.Should().Be("SCREENING");
        token.Status.Should().Be(ApplicationLinkTokenStatus.Consumed);
        listing.Status.Should().Be(VacancyListingStatus.UnderApplication);
        await _screeningResultRepository.Received(1).AddAsync(Arg.Is<ScreeningResult>(r => r.Provider == "NOOP" && r.OutcomeSummary == ScreeningOutcomeSummary.Pass));
        await _auditService.Received(1).RecordAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), "APPLICATION_SUBMITTED", Arg.Any<string>());
    }

    [Fact]
    public async Task SubmitAsync_WritesTheLifecycleEventsInOrder()
    {
        ArrangeValidLink();
        var events = new List<string>();
        await _eventRepository.AddAsync(Arg.Do<ApplicationEvent>(e => events.Add(e.EventType)));
        var sut = CreateSut();

        await sut.SubmitAsync(RawToken, ValidRequest());

        events.Should().Equal("LINK_ISSUED", "SUBMITTED", "SCREENING_REQUESTED", "SCREENING_RECEIVED");
    }

    [Fact]
    public async Task SubmitAsync_TagsApplicantEmailsAsProspectiveTenant_AndNotifiesEveryStaffUser()
    {
        ArrangeValidLink();
        var sut = CreateSut();

        await sut.SubmitAsync(RawToken, ValidRequest());

        await _notificationService.Received(1).SendAsync(
            _tenantId, "SCREENING_COMPLETE", "verified@example.com", Arg.Any<object>(), Arg.Any<RelatedEntityRef>(), NotificationRecipientType.ProspectiveTenant);
        await _notificationService.Received(1).SendAsync(_tenantId, "APPLICATION_SUBMITTED", "admin@example.com", Arg.Any<object>(), Arg.Any<RelatedEntityRef>());
        await _notificationService.Received(1).SendAsync(_tenantId, "APPLICATION_SUBMITTED", "staff@example.com", Arg.Any<object>(), Arg.Any<RelatedEntityRef>());
    }

    [Fact]
    public async Task SubmitAsync_NotifiesOnlyTheRolesTheTenantChose_ForNewApplications()
    {
        ArrangeValidLink();
        _tenantSettingsService.GetNotificationSettingsAsync(_tenantId).Returns(
            new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN", "DEPARTMENT_STAFF"]));
        var sut = CreateSut();

        await sut.SubmitAsync(RawToken, ValidRequest());

        await _authService.Received(1).GetUsersByRolesAsync(_tenantId, Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(AdminOnlyRoles)));
        await _authService.DidNotReceive().GetUsersByRolesAsync(_tenantId, Arg.Is<IEnumerable<string>>(r => r.Contains("DEPARTMENT_STAFF")));
    }

    [Fact]
    public async Task SubmitAsync_WhenTheLinkDoesNotResolve_ReturnsNotFound_AndCreatesNothing()
    {
        _linkService.ResolveAsync(Arg.Any<string>()).Returns(ServiceResult<ApplicationLinkContext>.NotFound());
        var sut = CreateSut();

        var result = await sut.SubmitAsync("bogus", ValidRequest());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task SubmitAsync_WhenTheTokenWasAlreadyConsumed_ReturnsNotFound_NoSecondApplication()
    {
        var (token, _) = ArrangeValidLink();
        token.Consume(Guid.NewGuid());
        var sut = CreateSut();

        var result = await sut.SubmitAsync(RawToken, ValidRequest());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task SubmitAsync_WhenTheListingIsNoLongerAcceptingApplications_ReturnsBusinessRuleViolation()
    {
        var (_, listing) = ArrangeValidLink();
        listing.Withdraw();
        var sut = CreateSut();

        var result = await sut.SubmitAsync(RawToken, ValidRequest());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        result.ErrorCode.Should().Be("TRL-E-LSE-11002");
    }

    [Fact]
    public async Task SubmitAsync_WhenTheScreeningProviderIsDown_StillSucceeds_AndLeavesTheApplicationSubmitted()
    {
        ArrangeValidLink();
        _screeningProvider.RequestScreeningAsync(Arg.Any<ApplicantDetails>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        var result = await sut.SubmitAsync(RawToken, ValidRequest());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("SUBMITTED");
        await _screeningResultRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task SubmitAsync_WithoutConsent_NeverCallsTheScreeningProvider()
    {
        ArrangeValidLink();
        var sut = CreateSut();

        await sut.SubmitAsync(RawToken, ValidRequest(consent: false));

        await _screeningProvider.DidNotReceiveWithAnyArgs().RequestScreeningAsync(default!);
    }

    private LeasingApplication ArrangeApprovedApplication()
    {
        var application = LeasingApplication.Create(
            _tenantId, _listingId, "applicant@example.com", "Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, true);
        application.Approve();
        _repository.GetByIdAsync(application.ApplicationId).Returns(application);
        return application;
    }

    /// <summary>The listing the application is against — its unit is the only unit a lease draft may belong to.</summary>
    private Guid ArrangeListingUnit()
    {
        var unitId = Guid.NewGuid();
        _listingRepository.GetByIdAsync(_listingId).Returns(
            VacancyListing.Create(_tenantId, unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid()));
        return unitId;
    }

    private void ArrangeDraft(Guid draftId, string documentType, Guid? unitAssetId) =>
        _documentService.GetByIdAsync(draftId).Returns(ServiceResult<DocumentResponse>.Success(
            new DocumentResponse(draftId, null, unitAssetId, null, null, documentType, null, "ACTIVE", null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));

    [Fact]
    public async Task SendForSignatureAsync_WhenTheDraftIsNotALeaseDocument_IsRejected()
    {
        var application = ArrangeApprovedApplication();
        var unitId = ArrangeListingUnit();
        var draftId = Guid.NewGuid();
        ArrangeDraft(draftId, "INVOICE", unitId);
        var sut = CreateSut();

        var result = await sut.SendForSignatureAsync(application.ApplicationId, draftId, Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11005");
        await _eSignatureProvider.DidNotReceiveWithAnyArgs().SendForSignatureAsync(default, default!);
    }

    [Fact]
    public async Task SendForSignatureAsync_WhenTheDraftBelongsToAnotherUnit_IsRejected()
    {
        var application = ArrangeApprovedApplication();
        ArrangeListingUnit();
        var draftId = Guid.NewGuid();
        ArrangeDraft(draftId, "LEASE", Guid.NewGuid());
        var sut = CreateSut();

        var result = await sut.SendForSignatureAsync(application.ApplicationId, draftId, Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11005");
    }

    [Fact]
    public async Task SendForSignatureAsync_RecordsTheEnvelopeAndTheStaffActor_AndSendsBothSigners()
    {
        var application = ArrangeApprovedApplication();
        var unitId = ArrangeListingUnit();
        var staffId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        ArrangeDraft(draftId, "LEASE", unitId);
        _authService.GetUserByIdAsync(staffId).Returns(new AppUserSummary(staffId, "staff@example.com", _tenantId));
        _eSignatureProvider.SendForSignatureAsync(draftId, Arg.Any<IEnumerable<Signer>>()).Returns("env-123");
        var sut = CreateSut();

        var result = await sut.SendForSignatureAsync(application.ApplicationId, draftId, staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        application.EnvelopeId.Should().Be("env-123");
        application.SentForSignatureBy.Should().Be(staffId);
        application.DraftDocumentId.Should().Be(draftId);
        await _eSignatureProvider.Received(1).SendForSignatureAsync(draftId, Arg.Is<IEnumerable<Signer>>(s =>
            s.Any(x => x.Role == "TENANT" && x.Email == "applicant@example.com")
            && s.Any(x => x.Role == "LANDLORD_REP" && x.Email == "staff@example.com")));
    }

    [Fact]
    public async Task SendForSignatureAsync_WhenNotApproved_ReturnsBusinessRuleViolation()
    {
        var application = LeasingApplication.Create(
            _tenantId, _listingId, "a@example.com", "Ada", "1", new DateOnly(1990, 1, 1), "addr", null, null, true);
        _repository.GetByIdAsync(application.ApplicationId).Returns(application);
        var sut = CreateSut();

        var result = await sut.SendForSignatureAsync(application.ApplicationId, Guid.NewGuid(), Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11003");
    }

    [Fact]
    public async Task SendForSignatureAsync_CalledTwice_IsRejectedTheSecondTime()
    {
        var application = ArrangeApprovedApplication();
        application.MarkSentForSignature("env-1", Guid.NewGuid());
        var sut = CreateSut();

        var result = await sut.SendForSignatureAsync(application.ApplicationId, Guid.NewGuid(), Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11003");
    }

    [Fact]
    public async Task ApproveAsync_WritesAnEventAndAnAuditRecord()
    {
        var application = LeasingApplication.Create(
            _tenantId, _listingId, "a@example.com", "Ada", "1", new DateOnly(1990, 1, 1), "addr", null, null, true);
        _repository.GetByIdAsync(application.ApplicationId).Returns(application);
        var sut = CreateSut();

        var result = await sut.ApproveAsync(application.ApplicationId, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        await _eventRepository.Received(1).AddAsync(Arg.Is<ApplicationEvent>(e => e.EventType == "APPROVED"));
        await _auditService.Received(1).RecordAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), "APPLICATION_APPROVED", Arg.Any<string>());
    }

    [Fact]
    public async Task DeclineAsync_AfterLeased_ReturnsBusinessRuleViolation()
    {
        var application = ArrangeApprovedApplication();
        application.MarkLeased();
        var sut = CreateSut();

        var result = await sut.DeclineAsync(application.ApplicationId, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    // ---- EndLeaseAsync ----

    private LeasingApplication ArrangeLeasedApplication(out Guid unitId)
    {
        var application = ArrangeApprovedApplication();
        application.MarkLeased();
        unitId = ArrangeListingUnit();
        _unitAssetService.MarkVacantAsync(unitId).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "VACANT", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        _rentScheduleService.EndForLeaseAsync(_tenantId, application.ApplicationId, Arg.Any<Guid?>()).Returns(ServiceResult<bool>.Success(true));
        return application;
    }

    [Fact]
    public async Task EndLeaseAsync_OnALeasedApplication_EndsTheRentSchedule_VacatesTheUnit_AndClosesTheApplicationOut()
    {
        var staffId = Guid.NewGuid();
        var application = ArrangeLeasedApplication(out var unitId);
        var sut = CreateSut();

        var result = await sut.EndLeaseAsync(
            application.ApplicationId, new EndLeaseRequest(new DateOnly(2026, 6, 30), "Tenant relocated"), staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("LEASE_ENDED");
        application.LeaseEndedDate.Should().Be(new DateOnly(2026, 6, 30));
        await _rentScheduleService.Received(1).EndForLeaseAsync(_tenantId, application.ApplicationId, staffId);
        await _unitAssetService.Received(1).MarkVacantAsync(unitId);
        await _eventRepository.Received(1).AddAsync(Arg.Is<ApplicationEvent>(e => e.EventType == "LEASE_ENDED"));
        await _auditService.Received(1).RecordAsync(_tenantId, staffId, Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), "LEASE_ENDED", Arg.Any<string>());
    }

    [Fact]
    public async Task EndLeaseAsync_EndsTheRentScheduleBeforeVacatingTheUnit()
    {
        var application = ArrangeLeasedApplication(out var unitId);
        var order = new List<string>();
        _rentScheduleService.EndForLeaseAsync(_tenantId, application.ApplicationId, Arg.Any<Guid?>())
            .Returns(_ => { order.Add("rent"); return ServiceResult<bool>.Success(true); });
        _unitAssetService.MarkVacantAsync(unitId).Returns(_ =>
        {
            order.Add("vacate");
            return ServiceResult<UnitAssetResponse>.Success(
                new UnitAssetResponse(unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "VACANT", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow));
        });
        var sut = CreateSut();

        await sut.EndLeaseAsync(application.ApplicationId, new EndLeaseRequest(new DateOnly(2026, 6, 30), null), Guid.NewGuid());

        order.Should().Equal("rent", "vacate");
    }

    [Fact]
    public async Task EndLeaseAsync_WhenThereWasNoRentSchedule_StillEndsTheLease()
    {
        var application = ArrangeLeasedApplication(out _);
        _rentScheduleService.EndForLeaseAsync(_tenantId, application.ApplicationId, Arg.Any<Guid?>()).Returns(ServiceResult<bool>.Success(false));
        var sut = CreateSut();

        var result = await sut.EndLeaseAsync(application.ApplicationId, new EndLeaseRequest(new DateOnly(2026, 6, 30), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Fact]
    public async Task EndLeaseAsync_WhenTheUnitIsAlreadyVacant_StillCompletes_ARetryTolerance()
    {
        var application = ArrangeLeasedApplication(out var unitId);
        _unitAssetService.MarkVacantAsync(unitId).Returns(ServiceResult<UnitAssetResponse>.BusinessRuleViolation("TRL-E-AST-3000"));
        var sut = CreateSut();

        var result = await sut.EndLeaseAsync(application.ApplicationId, new EndLeaseRequest(new DateOnly(2026, 6, 30), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndLeaseAsync_OnAnApplicationThatIsNotLeased_ReturnsBusinessRuleViolation(bool approveFirst)
    {
        var application = LeasingApplication.Create(
            _tenantId, _listingId, "a@example.com", "Ada", "1", new DateOnly(1990, 1, 1), "addr", null, null, true);
        if (approveFirst)
        {
            application.Approve();
        }

        _repository.GetByIdAsync(application.ApplicationId).Returns(application);
        var sut = CreateSut();

        var result = await sut.EndLeaseAsync(application.ApplicationId, new EndLeaseRequest(new DateOnly(2026, 6, 30), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _rentScheduleService.DidNotReceiveWithAnyArgs().EndForLeaseAsync(default, default, default);
        await _unitAssetService.DidNotReceiveWithAnyArgs().MarkVacantAsync(default);
    }

    [Fact]
    public async Task EndLeaseAsync_ForAnUnknownApplication_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((LeasingApplication?)null);
        var sut = CreateSut();

        var result = await sut.EndLeaseAsync(Guid.NewGuid(), new EndLeaseRequest(new DateOnly(2026, 6, 30), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }
}
