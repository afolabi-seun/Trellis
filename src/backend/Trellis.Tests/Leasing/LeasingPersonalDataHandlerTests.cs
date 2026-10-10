using FluentAssertions;
using NSubstitute;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Xunit;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Tests.Leasing;

public sealed class LeasingPersonalDataHandlerTests
{
    private const string SubjectEmail = "applicant@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IApplicationRepository _applicationRepository = Substitute.For<IApplicationRepository>();
    private readonly IApplicationLinkTokenRepository _tokenRepository = Substitute.For<IApplicationLinkTokenRepository>();
    private readonly IScreeningResultRepository _screeningResultRepository = Substitute.For<IScreeningResultRepository>();
    private readonly ILeasingWebhookEventRepository _webhookEventRepository = Substitute.For<ILeasingWebhookEventRepository>();
    private readonly ILeaseService _leaseService = Substitute.For<ILeaseService>();

    private LeasingPersonalDataHandler CreateSut() =>
        new(_applicationRepository, _tokenRepository, _screeningResultRepository, _webhookEventRepository, _leaseService);

    private LeasingApplication CreateApplication() => LeasingApplication.Create(
        _tenantId, Guid.NewGuid(), SubjectEmail, "Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", "Acme", 5000m, true);

    [Fact]
    public async Task ExportAsync_WithNoMatchingApplications_ReturnsNull()
    {
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExportAsync_WithMatchingApplications_ReturnsThem()
    {
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([CreateApplication()]);
        _leaseService.ListEndNotesForApplicationsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns([]);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportAsync_IncludesTheEndNoteOfEachApplicationsLease()
    {
        var withNote = CreateApplication();
        var withoutNote = CreateApplication();
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([withNote, withoutNote]);
        _leaseService.ListEndNotesForApplicationsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns([new LeaseEndNote(Guid.NewGuid(), withNote.ApplicationId, "Moved out for medical treatment")]);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        await _leaseService.Received(1).ListEndNotesForApplicationsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(withNote.ApplicationId) && ids.Contains(withoutNote.ApplicationId)));
        var exported = ((System.Collections.IEnumerable)result!).Cast<object>().ToList();
        LeaseEndNoteOf(exported[0]).Should().Be("Moved out for medical treatment");
        LeaseEndNoteOf(exported[1]).Should().BeNull();
    }

    private static string? LeaseEndNoteOf(object exported) =>
        (string?)exported.GetType().GetProperty("LeaseEndNote")!.GetValue(exported);

    [Fact]
    public async Task EraseAsync_RedactsApplicantPii_OnEveryMatchingApplication()
    {
        var application = CreateApplication();
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([application]);
        _tokenRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        application.ApplicantEmail.Should().NotBe(SubjectEmail);
        application.ApplicantEmail.Should().Contain("erased-").And.EndWith("@erased.invalid");
        application.ApplicantFullName.Should().Be("Erased applicant");
        application.ApplicantPhone.Should().Be("[erased]");
        application.ApplicantCurrentAddress.Should().Be("[erased]");
        application.ApplicantDateOfBirth.Should().Be(DateOnly.MinValue);
        application.ApplicantEmployerName.Should().BeNull();
        application.ApplicantMonthlyIncome.Should().BeNull();
        summary.ModuleName.Should().Be("Leasing");
        summary.RecordsAffected.Should().Be(1);
        await _applicationRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_AlsoRedactsTheApplicationLinkTokenAndCountsIt()
    {
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        var token = ApplicationLinkToken.Create(_tenantId, Guid.NewGuid(), SubjectEmail, "hash", DateTimeOffset.UtcNow.AddDays(7));
        _tokenRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([token]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        token.ApplicantEmail.Should().NotBe(SubjectEmail);
        summary.RecordsAffected.Should().Be(1);
        await _tokenRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_WhenTheApplicationHasAnEnvelopeId_RedactsItsWebhookEventPayload()
    {
        var application = CreateApplication();
        application.Approve();
        application.MarkSentForSignature("env-123", Guid.NewGuid(), null);
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([application]);
        _tokenRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        var webhookEvent = LeasingWebhookEvent.Create("env-123", "envelope.completed", "{\"applicant\":\"sensitive\"}");
        _webhookEventRepository.GetByEnvelopeIdAsync("env-123").Returns(webhookEvent);
        var sut = CreateSut();

        await sut.EraseAsync(SubjectEmail);

        webhookEvent.PayloadJson.Should().Be("{\"redacted\":true}");
        await _webhookEventRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_AsksPropertyAssetToRedactTheEndNotesOfThoseApplicationsLeases_AndCountsThem()
    {
        var first = CreateApplication();
        var second = CreateApplication();
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([first, second]);
        _tokenRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        _leaseService.RedactEndNotesForApplicationsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(1);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        await _leaseService.Received(1).RedactEndNotesForApplicationsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(first.ApplicationId) && ids.Contains(second.ApplicationId)));
        summary.RecordsAffected.Should().Be(3);
    }

    [Fact]
    public async Task EraseAsync_IsIdempotent_ASecondCallOnAnAlreadyErasedSubjectAffectsNothing()
    {
        _applicationRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        _tokenRepository.ListByApplicantEmailAsync(SubjectEmail).Returns([]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        summary.RecordsAffected.Should().Be(0);
    }
}
