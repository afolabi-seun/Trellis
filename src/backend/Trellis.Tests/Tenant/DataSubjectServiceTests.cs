using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Services;
using Xunit;

namespace Trellis.Tests.Tenant;

public sealed class DataSubjectServiceTests : IDisposable
{
    private const string SubjectEmail = "subject@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IPersonalDataHandler _handlerA = Substitute.For<IPersonalDataHandler>();
    private readonly IPersonalDataHandler _handlerB = Substitute.For<IPersonalDataHandler>();

    public DataSubjectServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        _authService.ReauthenticateAsync(_actorUserId, Arg.Any<string>()).Returns(ServiceResult<bool>.Success(true));
        _authService.CanEraseStaffAsync(Arg.Any<string>(), _actorUserId).Returns(true);
        _handlerA.ModuleName.Returns("ModuleA");
        _handlerB.ModuleName.Returns("ModuleB");
    }

    public void Dispose() => TenantContext.Current = null;

    private DataSubjectService CreateSut() => new([_handlerA, _handlerB], _authService, _auditService);

    [Fact]
    public async Task ExportAsync_CollectsEveryHandlersData_OmittingNullResults_AndAudits()
    {
        _handlerA.ExportAsync(SubjectEmail).Returns(new { some = "data" });
        _handlerB.ExportAsync(SubjectEmail).Returns((object?)null);
        var sut = CreateSut();

        var result = await sut.ExportAsync(new DataSubjectExportRequest(SubjectEmail), _actorUserId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Modules.Should().ContainKey("ModuleA");
        result.Data.Modules.Should().NotContainKey("ModuleB");
        await _auditService.Received(1).RecordAsync(
            _tenantId, _actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.DataSubjectExported, Arg.Any<string>());
    }

    [Fact]
    public async Task ExportAsync_NeverWritesTheRawSubjectEmailIntoTheAuditDetail()
    {
        _handlerA.ExportAsync(SubjectEmail).Returns((object?)null);
        _handlerB.ExportAsync(SubjectEmail).Returns((object?)null);
        string? capturedDetail = null;
        await _auditService.RecordAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<AuditActorType>(), Arg.Any<string>(),
            Arg.Do<string?>(d => capturedDetail = d));
        var sut = CreateSut();

        await sut.ExportAsync(new DataSubjectExportRequest(SubjectEmail), _actorUserId);

        capturedDetail.Should().NotContain(SubjectEmail);
    }

    [Fact]
    public async Task EraseAsync_WithTheWrongPassword_ReturnsForbidden_AndNeverCallsAnyHandler()
    {
        _authService.ReauthenticateAsync(_actorUserId, "wrong").Returns(ServiceResult<bool>.Forbidden("TRL-E-AUT-1004"));
        var sut = CreateSut();

        var result = await sut.EraseAsync(new EraseDataSubjectRequest(SubjectEmail, SubjectEmail, "wrong", null), _actorUserId);

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        await _handlerA.DidNotReceiveWithAnyArgs().EraseAsync(default!);
        await _handlerB.DidNotReceiveWithAnyArgs().EraseAsync(default!);
    }

    [Fact]
    public async Task EraseAsync_WhenTheSubjectIsTheCallerOrTheLastAdmin_ReturnsBusinessRuleViolation_AndNeverCallsAnyHandler()
    {
        _authService.CanEraseStaffAsync(SubjectEmail, _actorUserId).Returns(false);
        var sut = CreateSut();

        var result = await sut.EraseAsync(new EraseDataSubjectRequest(SubjectEmail, SubjectEmail, "correct", null), _actorUserId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        result.ErrorCode.Should().Be("TRL-E-TEN-2006");
        await _handlerA.DidNotReceiveWithAnyArgs().EraseAsync(default!);
    }

    [Fact]
    public async Task EraseAsync_CallsEveryHandler_AggregatesCounts_AndAudits()
    {
        _handlerA.EraseAsync(SubjectEmail).Returns(new ErasureSummary("ModuleA", 2));
        _handlerB.EraseAsync(SubjectEmail).Returns(new ErasureSummary("ModuleB", 0));
        var sut = CreateSut();

        var result = await sut.EraseAsync(new EraseDataSubjectRequest(SubjectEmail, SubjectEmail, "correct", "GDPR request"), _actorUserId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Counts.Should().BeEquivalentTo(
        [
            new ModuleErasureCount("ModuleA", 2),
            new ModuleErasureCount("ModuleB", 0),
        ]);
        await _auditService.Received(1).RecordAsync(
            _tenantId, _actorUserId, AuditActorType.HumanUser, AuditTypes.Tenant.DataErasure, Arg.Any<string>());
    }

    [Fact]
    public async Task EraseAsync_RunsReportingBeforePropertyAsset_RegardlessOfDiRegistrationOrder()
    {
        // Registered PropertyAsset-before-Reporting deliberately — DataSubjectService must not
        // depend on DI's own registration order: Reporting resolves an owner subject through
        // PropertyAsset's IOwnerService.GetByEmailAsync, which fails once PropertyAsset's own
        // handler has already redacted that email out from under it.
        var propertyAssetHandler = Substitute.For<IPersonalDataHandler>();
        propertyAssetHandler.ModuleName.Returns("PropertyAsset");
        propertyAssetHandler.EraseAsync(SubjectEmail).Returns(new ErasureSummary("PropertyAsset", 1));
        var reportingHandler = Substitute.For<IPersonalDataHandler>();
        reportingHandler.ModuleName.Returns("Reporting");
        reportingHandler.EraseAsync(SubjectEmail).Returns(new ErasureSummary("Reporting", 1));
        var sut = new DataSubjectService([propertyAssetHandler, reportingHandler], _authService, _auditService);

        await sut.EraseAsync(new EraseDataSubjectRequest(SubjectEmail, SubjectEmail, "correct", null), _actorUserId);

        Received.InOrder(() =>
        {
            reportingHandler.EraseAsync(SubjectEmail);
            propertyAssetHandler.EraseAsync(SubjectEmail);
        });
    }

    [Fact]
    public async Task EraseAsync_NeverWritesTheRawSubjectEmailIntoTheAuditDetail()
    {
        _handlerA.EraseAsync(SubjectEmail).Returns(new ErasureSummary("ModuleA", 1));
        _handlerB.EraseAsync(SubjectEmail).Returns(new ErasureSummary("ModuleB", 0));
        string? capturedDetail = null;
        await _auditService.RecordAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<AuditActorType>(), Arg.Any<string>(),
            Arg.Do<string?>(d => capturedDetail = d));
        var sut = CreateSut();

        await sut.EraseAsync(new EraseDataSubjectRequest(SubjectEmail, SubjectEmail, "correct", null), _actorUserId);

        capturedDetail.Should().NotContain(SubjectEmail);
    }
}
