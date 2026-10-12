using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Services;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Reporting;

public sealed class OwnerReportServiceTests : IDisposable
{
    private readonly IOwnerReportRepository _repository = Substitute.For<IOwnerReportRepository>();
    private readonly IReportExportService _exportService = Substitute.For<IReportExportService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();
    private readonly IOwnerService _ownerService = Substitute.For<IOwnerService>();
    private readonly IPropertyService _propertyService = Substitute.For<IPropertyService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IRentScheduleService _rentScheduleService = Substitute.For<IRentScheduleService>();
    private readonly IWorkflowInstanceService _workflowInstanceService = Substitute.For<IWorkflowInstanceService>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private readonly Guid _tenantId = Guid.NewGuid();

    public OwnerReportServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        _exportService.GenerateOwnerReportPdf(Arg.Any<OwnerReportContent>()).Returns([0x25, 0x50, 0x44, 0x46]);
    }

    public void Dispose() => TenantContext.Current = null;

    private OwnerReportService CreateSut() => new(
        _repository, _exportService, _auditService, _notificationService, _tenantService, _ownerService,
        _propertyService, _unitAssetService, _rentScheduleService, _workflowInstanceService, _documentService);

    private static OwnerResponse SampleOwner(string? email = "owner@example.com") =>
        new(Guid.NewGuid(), "Jane Owner", email, null, "ACTIVE", "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    [Fact]
    public async Task GenerateAsync_WhenOwnerNotFound_ReturnsNotFound()
    {
        _ownerService.GetByIdAsync(Arg.Any<Guid>()).Returns(ServiceResult<OwnerResponse>.NotFound());
        var sut = CreateSut();

        var result = await sut.GenerateAsync(Guid.NewGuid(), OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task GenerateAsync_WhenOwnerFound_AssemblesRealCrossModuleDataAndMarksReady()
    {
        var owner = SampleOwner();
        var ownerId = owner.OwnerId;
        _ownerService.GetByIdAsync(ownerId).Returns(ServiceResult<OwnerResponse>.Success(owner));
        _tenantService.GetByIdAsync(_tenantId).Returns(ServiceResult<TenantResponse>.Success(
            new TenantResponse(_tenantId, "Acme PM", "US", "ACTIVE", "PROFESSIONAL", DateTimeOffset.UtcNow)));

        var propertyIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _propertyService.ListPropertyIdsByOwnerAsync(ownerId).Returns((IReadOnlyList<Guid>)propertyIds);
        _workflowInstanceService.GetOwnerMaintenanceSummaryAsync(propertyIds, Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns((450.00m, 2));

        _documentService.ListByOwnerAsync(ownerId, null, 1000).Returns(ServiceResult<PagedResponse<DocumentResponse>>.Success(
            new PagedResponse<DocumentResponse>(
                [
                    new DocumentResponse(Guid.NewGuid(), null, null, ownerId, null, "LEASE", new DateOnly(2026, 2, 1), "ACTIVE", null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow),
                    new DocumentResponse(Guid.NewGuid(), null, null, ownerId, null, "LEASE", new DateOnly(2027, 1, 1), "ACTIVE", null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow),
                ],
                null, false, 2)));

        var sut = CreateSut();

        var result = await sut.GenerateAsync(ownerId, OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.PropertyCount.Should().Be(2);
        result.Data.TotalMaintenanceCost.Should().Be(450.00m);
        result.Data.MaintenanceCurrency.Should().Be("USD");
        result.Data.OpenMaintenanceCount.Should().Be(2);
        result.Data.Status.Should().Be("READY");
        await _repository.Received(2).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(_tenantId, Arg.Any<Guid>(), AuditActorType.HumanUser, AuditTypes.Reporting.OwnerReportGenerated, Arg.Any<string>());
    }

    [Fact]
    public async Task DownloadPdfAsync_WhenReportNotReady_ReturnsNotFound()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        var sut = CreateSut();

        var result = await sut.DownloadPdfAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task DownloadPdfAsync_WhenReady_ReturnsStoredBytes()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25, 0x50, 0x44, 0x46]);
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        var sut = CreateSut();

        var result = await sut.DownloadPdfAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().Equal(report.PdfContent);
    }

    [Fact]
    public async Task DeliverByEmailAsync_WhenOwnerHasNoEmail_ReturnsBusinessRuleViolation()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25]);
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        _ownerService.GetByIdAsync(report.OwnerId).Returns(ServiceResult<OwnerResponse>.Success(SampleOwner(email: null)));
        var sut = CreateSut();

        var result = await sut.DeliverByEmailAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _notificationService.DidNotReceiveWithAnyArgs().SendAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task DeliverByEmailAsync_WhenOwnerHasEmail_DispatchesNotificationAndRecordsAudit()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25]);
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        _ownerService.GetByIdAsync(report.OwnerId).Returns(ServiceResult<OwnerResponse>.Success(SampleOwner()));
        var sut = CreateSut();

        var result = await sut.DeliverByEmailAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        await _notificationService.Received(1).SendAsync(
            _tenantId, "OWNER_REPORT_READY", "owner@example.com", Arg.Any<object>(), Arg.Any<RelatedEntityRef>());
        await _auditService.Received(1).RecordAsync(_tenantId, Arg.Any<Guid>(), AuditActorType.HumanUser, AuditTypes.Reporting.OwnerReportDelivered, Arg.Any<string>());
    }

    private Guid ArrangeGenerationForOwner(out List<Guid> unitIds)
    {
        var owner = SampleOwner();
        _ownerService.GetByIdAsync(owner.OwnerId).Returns(ServiceResult<OwnerResponse>.Success(owner));
        _tenantService.GetByIdAsync(_tenantId).Returns(ServiceResult<TenantResponse>.Success(
            new TenantResponse(_tenantId, "Acme PM", "US", "ACTIVE", "PROFESSIONAL", DateTimeOffset.UtcNow)));

        var propertyIds = new List<Guid> { Guid.NewGuid() };
        _propertyService.ListPropertyIdsByOwnerAsync(owner.OwnerId).Returns((IReadOnlyList<Guid>)propertyIds);
        _workflowInstanceService.GetOwnerMaintenanceSummaryAsync(propertyIds, Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns((0m, 0));
        _documentService.ListByOwnerAsync(owner.OwnerId, null, 1000).Returns(ServiceResult<PagedResponse<DocumentResponse>>.Success(
            new PagedResponse<DocumentResponse>([], null, false, 0)));

        unitIds = [Guid.NewGuid(), Guid.NewGuid()];
        _unitAssetService.ListUnitAssetIdsByPropertiesAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns((IReadOnlyList<Guid>)unitIds);
        return owner.OwnerId;
    }

    [Fact]
    public async Task GenerateAsync_WhenRentCollectionIsEnabled_IncludesTheRentSection_InTheRenderedContentAndTheResponse()
    {
        var ownerId = ArrangeGenerationForOwner(out var unitIds);
        _rentScheduleService.GetOwnerRentSummaryAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new OwnerRentSummary(3, 1, 2, [new CurrencyAmount("USD", 4500.50m)]));
        var sut = CreateSut();

        var result = await sut.GenerateAsync(ownerId, OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        result.Data!.Rent.Should().NotBeNull();
        result.Data.Rent!.CurrentCount.Should().Be(3);
        result.Data.Rent.OverdueCount.Should().Be(1);
        result.Data.Rent.ArrearsCount.Should().Be(2);
        result.Data.Rent.ReceivedByCurrency.Should().Equal(new CurrencyAmount("USD", 4500.50m));
        _exportService.Received(1).GenerateOwnerReportPdf(Arg.Is<OwnerReportContent>(c => c.Rent != null && c.Rent.ArrearsCount == 2));
        await _rentScheduleService.Received(1).GetOwnerRentSummaryAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(unitIds)), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GenerateAsync_WhenRentCollectionIsNotEnabled_OmitsTheRentSection_NotAMisleadingZero()
    {
        var ownerId = ArrangeGenerationForOwner(out _);
        _rentScheduleService.GetOwnerRentSummaryAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns((OwnerRentSummary?)null);
        var sut = CreateSut();

        var result = await sut.GenerateAsync(ownerId, OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        result.Data!.Rent.Should().BeNull();
        _exportService.Received(1).GenerateOwnerReportPdf(Arg.Is<OwnerReportContent>(c => c.Rent == null));
    }

    [Fact]
    public async Task DownloadCsvAsync_ReproducesTheStoredRentSnapshot_NotALiveRequery()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        var rentJson = JsonSerializer.Serialize<IReadOnlyList<CurrencyAmount>>([new CurrencyAmount("USD", 4500.50m)]);
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25], 3, 1, 2, rentJson, new DateOnly(2026, 2, 3));
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        var sut = CreateSut();

        await sut.DownloadCsvAsync(report.OwnerReportId);

        var expected = new List<CurrencyAmount> { new("USD", 4500.50m) };
        _exportService.Received(1).GenerateOwnerReportCsv(Arg.Is<OwnerReportContent>(c =>
            c.Rent != null && c.Rent.CurrentCount == 3 && c.Rent.ArrearsCount == 2
            && c.Rent.ReceivedByCurrency.SequenceEqual(expected) && c.Rent.AsOf == new DateOnly(2026, 2, 3)));
        await _rentScheduleService.DidNotReceiveWithAnyArgs().GetOwnerRentSummaryAsync(default!, default, default, default);
    }

    [Fact]
    public async Task DownloadCsvAsync_ForAReportGeneratedBeforeRentCollectionExisted_HasNoRentSection()
    {
        var report = OwnerReport.Create(_tenantId, Guid.NewGuid(), "Jane Owner", OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());
        report.MarkReady(1, 0, "USD", 0, 0, 0, [0x25]);
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(report);
        var sut = CreateSut();

        await sut.DownloadCsvAsync(report.OwnerReportId);

        _exportService.Received(1).GenerateOwnerReportCsv(Arg.Is<OwnerReportContent>(c => c.Rent == null));
    }

    [Fact]
    public async Task GenerateAsync_WhenTheDocumentComplianceLookupFails_MarksTheReportFailed_NeverReportingAFalseCleanZero()
    {
        var ownerId = ArrangeGenerationForOwner(out _);
        _documentService.ListByOwnerAsync(ownerId, null, 1000).Returns(ServiceResult<PagedResponse<DocumentResponse>>.NotFound());
        var sut = CreateSut();

        var act = () => sut.GenerateAsync(ownerId, OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _exportService.DidNotReceiveWithAnyArgs().GenerateOwnerReportPdf(default!);
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task GenerateAsync_IncludesPaymentTimeliness_ForPeriodsDueInTheReportPeriod_WithEachPropertysAddress()
    {
        var ownerId = ArrangeGenerationForOwner(out var unitIds);
        var propertyId = Guid.NewGuid();
        _rentScheduleService.GetOwnerRentSummaryAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new OwnerRentSummary(1, 0, 0, []));
        var unitMap = unitIds.ToDictionary(u => u, _ => propertyId);
        _unitAssetService.MapUnitsToPropertiesAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(unitMap);
        var overall = new PaymentTimelinessDto(4, 3, 0.75m, 6m);
        _rentScheduleService.GetPaymentTimelinessAsync(Arg.Any<IReadOnlyDictionary<Guid, Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new RentTimelinessSummary(overall, [new PropertyPaymentTimelinessDto(propertyId, overall)]));
        _propertyService.GetByIdAsync(propertyId).Returns(ServiceResult<PropertyResponse>.Success(
            new PropertyResponse(propertyId, Guid.NewGuid(), "9 Elm St", "RESIDENTIAL", "ACTIVE", null, null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        var sut = CreateSut();

        var result = await sut.GenerateAsync(ownerId, OwnerReportPeriodType.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid());

        result.Data!.Rent!.Timeliness!.Overall.Should().Be(overall);
        result.Data.Rent.Timeliness.ByProperty.Should().ContainSingle().Which.PropertyAddress.Should().Be("9 Elm St");
        await _rentScheduleService.Received(1).GetPaymentTimelinessAsync(
            unitMap, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Arg.Any<DateOnly>());
        _exportService.Received(1).GenerateOwnerReportPdf(Arg.Is<OwnerReportContent>(c => c.Rent!.Timeliness!.Overall.PeriodsOnTime == 3));
    }
}
