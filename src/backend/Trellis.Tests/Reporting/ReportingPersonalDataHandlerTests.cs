using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Reporting;

public sealed class ReportingPersonalDataHandlerTests
{
    private const string SubjectEmail = "owner@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly IOwnerService _ownerService = Substitute.For<IOwnerService>();
    private readonly IOwnerReportRepository _ownerReportRepository = Substitute.For<IOwnerReportRepository>();
    private readonly IReportShareLinkRepository _shareLinkRepository = Substitute.For<IReportShareLinkRepository>();

    private ReportingPersonalDataHandler CreateSut() => new(_ownerService, _ownerReportRepository, _shareLinkRepository);

    private OwnerResponse OwnerFound() => new(_ownerId, "Jane Owner", SubjectEmail, null, "ACTIVE", "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    private OwnerReport CreateReport() => OwnerReport.Create(
        _tenantId, _ownerId, "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

    [Fact]
    public async Task ExportAsync_WhenTheEmailDoesNotResolveToAnOwner_ReturnsNull()
    {
        _ownerService.GetByEmailAsync(SubjectEmail).Returns(ServiceResult<OwnerResponse>.NotFound());
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        result.Should().BeNull();
        await _ownerReportRepository.DidNotReceiveWithAnyArgs().ListAllForOwnerAsync(default);
    }

    [Fact]
    public async Task EraseAsync_RedactsOwnerNameAndPdfContent_OnEveryReport_AndRevokesActiveShareLinks()
    {
        _ownerService.GetByEmailAsync(SubjectEmail).Returns(ServiceResult<OwnerResponse>.Success(OwnerFound()));
        var report = CreateReport();
        report.MarkReady(1, 500m, "ZAR", 1, 1, 0, [1, 2, 3]);
        _ownerReportRepository.ListAllForOwnerAsync(_ownerId).Returns([report]);
        var activeLink = ReportShareLink.Create(_tenantId, report.OwnerReportId, "hash", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        var revokedLink = ReportShareLink.Create(_tenantId, report.OwnerReportId, "hash2", DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        revokedLink.Revoke();
        _shareLinkRepository.ListByOwnerReportIdAsync(report.OwnerReportId).Returns([activeLink, revokedLink]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        report.OwnerName.Should().Be("Erased owner");
        report.PdfContent.Should().BeNull();
        activeLink.Status.Should().Be(ReportShareLinkStatus.Revoked);
        summary.ModuleName.Should().Be("Reporting");
        summary.RecordsAffected.Should().Be(1);
        await _ownerReportRepository.Received(1).SaveChangesAsync();
        await _shareLinkRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_WhenTheEmailDoesNotResolveToAnOwner_ReturnsZero()
    {
        _ownerService.GetByEmailAsync(SubjectEmail).Returns(ServiceResult<OwnerResponse>.NotFound());
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        summary.RecordsAffected.Should().Be(0);
    }
}
