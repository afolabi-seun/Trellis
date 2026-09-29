using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Trellis.Api.Core.Results;
using Trellis.Api.Core.Security;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Tests.Leasing;

/// <summary>
/// BDR-LSE-004's narrow, separately-audited full-report view. Mocked repositories, no database —
/// testing_strategy.md's Unit layer.
/// </summary>
public sealed class ScreeningReportServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly IApplicationRepository _applicationRepository = Substitute.For<IApplicationRepository>();
    private readonly IScreeningResultRepository _screeningResultRepository = Substitute.For<IScreeningResultRepository>();
    private readonly IApplicationEventRepository _eventRepository = Substitute.For<IApplicationEventRepository>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IEncryptionService _encryptionService = Substitute.For<IEncryptionService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private ScreeningReportService CreateSut() => new(
        _applicationRepository, _screeningResultRepository, _eventRepository, _authService, _encryptionService, _auditService);

    private LeasingApplication ArrangeApplicationWithReport(string ciphertext = "k1:cipher")
    {
        var application = LeasingApplication.Create(
            _tenantId, Guid.NewGuid(), "applicant@example.com", "Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, true);
        _applicationRepository.GetByIdAsync(application.ApplicationId).Returns(application);
        var screening = ScreeningResult.Create(_tenantId, application.ApplicationId, "NOOP", ScreeningOutcomeSummary.Pass, ciphertext);
        _screeningResultRepository.GetLatestForApplicationAsync(application.ApplicationId).Returns(screening);
        _authService.ReauthenticateAsync(_actorId, "correct-password").Returns(ServiceResult<bool>.Success(true));
        _encryptionService.DecryptAsync(ciphertext, $"{_tenantId}:{application.ApplicationId}").Returns("{\"plaintext\":true}");
        return application;
    }

    [Fact]
    public async Task ViewAsync_WithTheCorrectPassword_RecordsTheViewFirst_ThenReturnsTheDecryptedReport()
    {
        var application = ArrangeApplicationWithReport();
        var sut = CreateSut();

        var result = await sut.ViewAsync(application.ApplicationId, _actorId, "correct-password");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.ReportJson.Should().Be("{\"plaintext\":true}");
        result.Data.Provider.Should().Be("NOOP");
        result.Data.OutcomeSummary.Should().Be("PASS");
        await _eventRepository.Received(1).AddAsync(Arg.Is<ApplicationEvent>(e =>
            e.EventType == ApplicationEventTypes.ScreeningReportViewed && e.ApplicationId == application.ApplicationId));
        await _auditService.Received(1).RecordAsync(
            _tenantId, _actorId, Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType.HumanUser, AuditTypes.Leasing.ScreeningReportViewed, Arg.Any<string>());
        await _eventRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ViewAsync_WithTheWrongPassword_ReturnsForbidden_AndNeverDecrypts()
    {
        var application = ArrangeApplicationWithReport();
        _authService.ReauthenticateAsync(_actorId, "wrong-password").Returns(ServiceResult<bool>.Forbidden("TRL-E-AUT-1004"));
        var sut = CreateSut();

        var result = await sut.ViewAsync(application.ApplicationId, _actorId, "wrong-password");

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        result.ErrorCode.Should().Be("TRL-E-AUT-1004");
        await _encryptionService.DidNotReceiveWithAnyArgs().DecryptAsync(default!, default!);
        await _eventRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task ViewAsync_WhenRecordingTheViewFails_NeverDecrypts_AndThePlaintextNeverEscapes()
    {
        var application = ArrangeApplicationWithReport();
        _auditService.RecordAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("audit write failed"));
        var sut = CreateSut();

        var act = () => sut.ViewAsync(application.ApplicationId, _actorId, "correct-password");

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _encryptionService.DidNotReceiveWithAnyArgs().DecryptAsync(default!, default!);
    }

    [Fact]
    public async Task ViewAsync_ForAnUnknownApplication_ReturnsNotFound_AndNeverChecksThePassword()
    {
        _applicationRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((LeasingApplication?)null);
        var sut = CreateSut();

        var result = await sut.ViewAsync(Guid.NewGuid(), _actorId, "anything");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _authService.DidNotReceiveWithAnyArgs().ReauthenticateAsync(default, default!);
    }

    [Fact]
    public async Task ViewAsync_WhenScreeningHasNoReport_ReturnsNotFound_AndNeverChecksThePassword()
    {
        var application = LeasingApplication.Create(
            _tenantId, Guid.NewGuid(), "applicant@example.com", "Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, true);
        _applicationRepository.GetByIdAsync(application.ApplicationId).Returns(application);
        _screeningResultRepository.GetLatestForApplicationAsync(application.ApplicationId)
            .Returns(ScreeningResult.Create(_tenantId, application.ApplicationId, "NOOP", ScreeningOutcomeSummary.Pass, null));
        var sut = CreateSut();

        var result = await sut.ViewAsync(application.ApplicationId, _actorId, "anything");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _authService.DidNotReceiveWithAnyArgs().ReauthenticateAsync(default, default!);
    }

    [Fact]
    public async Task ViewAsync_WhenNoScreeningHasEverRun_ReturnsNotFound()
    {
        var application = LeasingApplication.Create(
            _tenantId, Guid.NewGuid(), "applicant@example.com", "Ada Applicant", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, true);
        _applicationRepository.GetByIdAsync(application.ApplicationId).Returns(application);
        _screeningResultRepository.GetLatestForApplicationAsync(application.ApplicationId).Returns((ScreeningResult?)null);
        var sut = CreateSut();

        var result = await sut.ViewAsync(application.ApplicationId, _actorId, "anything");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }
}
