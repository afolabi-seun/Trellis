using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Xunit;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Tests.Leasing;

public sealed class LeasingWebhookServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly IApplicationRepository _applicationRepository = Substitute.For<IApplicationRepository>();
    private readonly IVacancyListingRepository _listingRepository = Substitute.For<IVacancyListingRepository>();
    private readonly IApplicationEventRepository _eventRepository = Substitute.For<IApplicationEventRepository>();
    private readonly IESignatureProvider _eSignatureProvider = Substitute.For<IESignatureProvider>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IRentScheduleService _rentScheduleService = Substitute.For<IRentScheduleService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public void Dispose() => TenantContext.Current = null;

    private LeasingWebhookService CreateSut() => new(
        _applicationRepository, _listingRepository, _eventRepository, _eSignatureProvider, _documentService, _unitAssetService, _rentScheduleService, _auditService);

    private readonly Guid _draftId = Guid.NewGuid();
    private readonly Guid _signedVersionId = Guid.NewGuid();

    private (LeasingApplication Application, VacancyListing Listing) ArrangeSentApplication(bool withDraft = true)
    {
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        var application = LeasingApplication.Create(
            _tenantId, listing.VacancyListingId, "a@example.com", "Ada", "1", new DateOnly(1990, 1, 1), "addr", null, null, true);
        application.Approve();
        application.MarkSentForSignature("env-1", _staffId, withDraft ? _draftId : null);

        _applicationRepository.GetByEnvelopeIdAcrossTenantsAsync("env-1").Returns(application);
        _listingRepository.GetByIdAsync(listing.VacancyListingId).Returns(listing);
        _eSignatureProvider.DownloadSignedDocumentAsync("env-1").Returns([0x25, 0x50, 0x44, 0x46]);
        _documentService.CreateAsync(Arg.Any<DocumentUploadRequest>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>())
            .Returns(ServiceResult<DocumentResponse>.Success(
                new DocumentResponse(Guid.NewGuid(), null, _unitId, null, null, "LEASE", null, "ACTIVE", null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        _documentService.AddVersionAsync(_draftId, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>())
            .Returns(ServiceResult<DocumentVersionResponse>.Success(
                new DocumentVersionResponse(_signedVersionId, 2, "lease.pdf", "application/pdf", 4, "CLEAN", DateTimeOffset.UtcNow)));
        _unitAssetService.MarkOccupiedAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(_unitId, Guid.NewGuid(), "4B", "ACTIVE", "OCCUPIED", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        _rentScheduleService.EnsureScheduleForLeaseAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>())
            .Returns(ServiceResult<bool>.Success(true));
        return (application, listing);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_FilesTheLease_FlipsTheUnit_AndClosesOutTheListingAndApplication()
    {
        var (application, listing) = ArrangeSentApplication();
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        // BDR-LSE-006: the executed lease is the next VERSION of the draft staff sent — one document, not two.
        await _documentService.Received(1).AddVersionAsync(
            _draftId, Arg.Any<Stream>(), Arg.Any<string>(), "application/pdf", Arg.Any<long>(), _staffId);
        await _documentService.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default!, default!, default, default);
        await _unitAssetService.Received(1).MarkOccupiedAsync(_unitId);
        listing.Status.Should().Be(VacancyListingStatus.Leased);
        application.Status.Should().Be(ApplicationStatus.Leased);
        application.SignedLeaseVersionId.Should().Be(_signedVersionId);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_FilesTheDocumentAsTheStaffMemberWhoSentIt_NeverAnInventedSystemUser()
    {
        ArrangeSentApplication();
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await _documentService.Received(1).AddVersionAsync(
            Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), _staffId);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_WritesTheEventTrailAndTheLeaseExecutedAuditRecord()
    {
        ArrangeSentApplication();
        var events = new List<string>();
        await _eventRepository.AddAsync(Arg.Do<ApplicationEvent>(e => events.Add(e.EventType)));
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        events.Should().Equal("SIGNATURE_COMPLETED", "LEASED");
        await _auditService.Received(1).RecordAsync(
            _tenantId, _staffId, Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), "LEASE_EXECUTED", Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_ForAnAlreadyLeasedApplication_IsAnIdempotentNoOp()
    {
        var (application, _) = ArrangeSentApplication();
        application.MarkLeased();
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await _documentService.DidNotReceiveWithAnyArgs().AddVersionAsync(default, default!, default!, default!, default, default);
        await _unitAssetService.DidNotReceiveWithAnyArgs().MarkOccupiedAsync(default);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_ForAnUnknownEnvelope_DoesNothing()
    {
        _applicationRepository.GetByEnvelopeIdAcrossTenantsAsync("nope").Returns((LeasingApplication?)null);
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("nope", "{}");

        await _unitAssetService.DidNotReceiveWithAnyArgs().MarkOccupiedAsync(default);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_WhenFilingTheDocumentFails_Throws_SoTheProviderRetries_AndNothingHalfCompletes()
    {
        var (application, listing) = ArrangeSentApplication();
        _documentService.AddVersionAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>())
            .Returns(ServiceResult<DocumentVersionResponse>.BusinessRuleViolation("TRL-E-DOC-4001"));
        var sut = CreateSut();

        var act = () => sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitAssetService.DidNotReceiveWithAnyArgs().MarkOccupiedAsync(default);
        application.Status.Should().Be(ApplicationStatus.Approved);
        listing.Status.Should().Be(VacancyListingStatus.Listed);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_WhenTheUnitIsAlreadyOccupied_ARetryAfterAPartialFailure_StillCompletes()
    {
        var (application, _) = ArrangeSentApplication();
        _unitAssetService.MarkOccupiedAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.BusinessRuleViolation("TRL-E-AST-3000"));
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        application.Status.Should().Be(ApplicationStatus.Leased);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_StartsTheRentScheduleFromTheListingTerms_AsTheStaffMemberWhoSentIt()
    {
        var (application, listing) = ArrangeSentApplication();
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await _rentScheduleService.Received(1).EnsureScheduleForLeaseAsync(
            _tenantId, _unitId, application.ApplicationId, listing.RentAmount, listing.Currency, listing.AvailableFrom, _staffId);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_CreatesTheRentScheduleBeforeTheLeasingSave_SoARetryCanStillReachIt()
    {
        // After the Leasing save the application is Leased and a retry returns at the top — so the schedule must exist by then.
        ArrangeSentApplication();
        var order = new List<string>();
        _rentScheduleService.EnsureScheduleForLeaseAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>())
            .Returns(_ => { order.Add("rent"); return ServiceResult<bool>.Success(true); });
        _applicationRepository.SaveChangesAsync().Returns(_ => { order.Add("save"); return Task.CompletedTask; });
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        // The signed lease's own progress save comes first; the rent schedule lands before the final save.
        order.Should().Equal("save", "rent", "save");
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_WhenTheRentScheduleFails_Throws_SoTheProviderRetries_AndTheLeaseIsNotClosedOut()
    {
        var (application, listing) = ArrangeSentApplication();
        _rentScheduleService.EnsureScheduleForLeaseAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>())
            .Returns(ServiceResult<bool>.BusinessRuleViolation("TRL-E-RENT-12001"));
        var sut = CreateSut();

        var act = () => sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await act.Should().ThrowAsync<InvalidOperationException>();
        application.Status.Should().Be(ApplicationStatus.Approved);
        listing.Status.Should().Be(VacancyListingStatus.Listed);
        // Only the signed lease's progress save happened — the lease is not closed out.
        await _applicationRepository.Received(1).SaveChangesAsync();
        application.SignedLeaseVersionId.Should().Be(_signedVersionId);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_WhenTheTenantHasNoRentCollection_StillCompletesTheLease()
    {
        // EnsureScheduleForLeaseAsync returns Success(false) for an unentitled tenant (or an already-created schedule) — never blocks a signed lease.
        var (application, _) = ArrangeSentApplication();
        _rentScheduleService.EnsureScheduleForLeaseAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>())
            .Returns(ServiceResult<bool>.Success(false));
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        application.Status.Should().Be(ApplicationStatus.Leased);
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_ARetryAfterALaterFailure_DoesNotFileTheLeaseASecondTime()
    {
        // First delivery: the lease is filed, then the rent schedule fails and the provider is told to retry.
        var (application, _) = ArrangeSentApplication();
        var rentAttempts = 0;
        _rentScheduleService.EnsureScheduleForLeaseAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>())
            .Returns(_ => ++rentAttempts == 1
                ? ServiceResult<bool>.BusinessRuleViolation("TRL-E-RENT-12001")
                : ServiceResult<bool>.Success(true));
        var sut = CreateSut();

        var first = () => sut.ProcessSignatureCompletedAsync("env-1", "{}");
        await first.Should().ThrowAsync<InvalidOperationException>();
        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        application.Status.Should().Be(ApplicationStatus.Leased);
        await _documentService.Received(1).AddVersionAsync(
            Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>());
        await _eSignatureProvider.Received(1).DownloadSignedDocumentAsync("env-1");
    }

    [Fact]
    public async Task ProcessSignatureCompletedAsync_ForAnApplicationSentBeforeTheDraftWasTracked_FallsBackToFilingANewDocument()
    {
        var (application, _) = ArrangeSentApplication(withDraft: false);
        var sut = CreateSut();

        await sut.ProcessSignatureCompletedAsync("env-1", "{}");

        await _documentService.Received(1).CreateAsync(
            Arg.Is<DocumentUploadRequest>(r => r.DocumentType == "LEASE" && r.UnitAssetId == _unitId),
            Arg.Any<Stream>(), Arg.Any<string>(), "application/pdf", Arg.Any<long>(), _staffId);
        await _documentService.DidNotReceiveWithAnyArgs().AddVersionAsync(default, default!, default!, default!, default, default);
        application.SignedLeaseVersionId.Should().NotBeNull();
        application.Status.Should().Be(ApplicationStatus.Leased);
    }
}
