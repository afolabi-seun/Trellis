using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Configuration;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Reporting;

public sealed class ReportShareLinkServiceTests
{
    private readonly IReportShareLinkRepository _shareLinkRepository = Substitute.For<IReportShareLinkRepository>();
    private readonly IOwnerReportRepository _ownerReportRepository = Substitute.For<IOwnerReportRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private ReportShareLinkService CreateSut() =>
        new(_shareLinkRepository, _ownerReportRepository, _auditService, Options.Create(new ReportingOptions { ShareLinkExpiryDays = 14 }));

    private static OwnerReport CreateReadyReport(Guid tenantId)
    {
        var report = OwnerReport.Create(tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25, 0x50, 0x44, 0x46]);
        return report;
    }

    [Fact]
    public async Task CreateAsync_WhenReportIsReady_ReturnsRawTokenExactlyOnce()
    {
        var ownerReportId = Guid.NewGuid();
        var report = CreateReadyReport(Guid.NewGuid());
        _ownerReportRepository.GetByIdAsync(ownerReportId).Returns(report);
        var sut = CreateSut();

        var result = await sut.CreateAsync(ownerReportId, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Token.Should().NotBeNullOrEmpty();
        await _shareLinkRepository.Received(1).AddAsync(Arg.Any<ReportShareLink>());
    }

    [Fact]
    public async Task CreateAsync_WhenReportNotFound_ReturnsNotFound()
    {
        _ownerReportRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((OwnerReport?)null);
        var sut = CreateSut();

        var result = await sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenTokenIsUnknown_ReturnsNotFound()
    {
        _shareLinkRepository.ResolveByTokenHashAsync(Arg.Any<string>()).Returns((ReportShareLink?)null);
        var sut = CreateSut();

        var result = await sut.ResolveAsync("bogus-token");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenLinkExpired_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var ownerReportId = Guid.NewGuid();
        var link = ReportShareLink.Create(tenantId, ownerReportId, "hash", DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid());
        _shareLinkRepository.ResolveByTokenHashAsync(Arg.Any<string>()).Returns(link);
        var sut = CreateSut();

        var result = await sut.ResolveAsync("expired-token");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenLinkRevoked_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var ownerReportId = Guid.NewGuid();
        var link = ReportShareLink.Create(tenantId, ownerReportId, "hash", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        link.Revoke();
        _shareLinkRepository.ResolveByTokenHashAsync(Arg.Any<string>()).Returns(link);
        var sut = CreateSut();

        var result = await sut.ResolveAsync("revoked-token");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenValidAndReportReady_ReturnsPdfBytes_AndRecordsAccess()
    {
        var tenantId = Guid.NewGuid();
        var ownerReportId = Guid.NewGuid();
        var link = ReportShareLink.Create(tenantId, ownerReportId, "hash", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        var report = CreateReadyReport(tenantId);
        _shareLinkRepository.ResolveByTokenHashAsync(Arg.Any<string>()).Returns(link);
        _ownerReportRepository.GetByIdForTenantAsync(tenantId, ownerReportId).Returns(report);
        var sut = CreateSut();

        var result = await sut.ResolveAsync("valid-token");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.PdfContent.Should().NotBeEmpty();
        link.LastAccessedAt.Should().NotBeNull();
        await _shareLinkRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task RevokeAsync_WhenActive_TransitionsToRevoked()
    {
        var link = ReportShareLink.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        _shareLinkRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(link);
        var sut = CreateSut();

        var result = await sut.RevokeAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        link.Status.Should().Be(ReportShareLinkStatus.Revoked);
    }

    [Fact]
    public async Task RevokeAsync_WhenAlreadyRevoked_ReturnsBusinessRuleViolation()
    {
        var link = ReportShareLink.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        link.Revoke();
        _shareLinkRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(link);
        var sut = CreateSut();

        var result = await sut.RevokeAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }
}
