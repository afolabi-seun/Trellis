using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Services;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

public sealed class LeaseServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly ILeaseRepository _repository = Substitute.For<ILeaseRepository>();
    private readonly IUnitAssetRepository _unitRepository = Substitute.For<IUnitAssetRepository>();
    private readonly IPropertyRepository _propertyRepository = Substitute.For<IPropertyRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly UnitAsset _unit;

    public LeaseServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        var property = Property.Create(_tenantId, Guid.NewGuid(), "1 Main St", PropertyType.Residential, null, null, null, null);
        _unit = UnitAsset.Create(_tenantId, property.PropertyId, "4B", null, null);
        _unitRepository.GetByIdAsync(_unit.UnitAssetId).Returns(_unit);
        _propertyRepository.GetByIdAsync(property.PropertyId).Returns(property);
        _repository.GetRowVersion(Arg.Any<Lease>()).Returns("AAAAAAAAAAA=");
    }

    public void Dispose() => TenantContext.Current = null;

    private LeaseService CreateSut() => new(_repository, _unitRepository, _propertyRepository, _auditService);

    private LeaseStartRequest Start(Guid? applicationId = null, bool termRecorded = true, DateOnly? end = null) =>
        new(_unit.UnitAssetId, applicationId, new DateOnly(2026, 1, 1), termRecorded, termRecorded ? end ?? new DateOnly(2026, 12, 31) : null);

    private Lease ActiveLease(Guid? applicationId = null)
    {
        var lease = Lease.Start(_tenantId, _unit.UnitAssetId, applicationId, new DateOnly(2025, 1, 1), LeaseTermType.Fixed, new DateOnly(2025, 12, 31), null);
        _repository.GetByIdAsync(lease.LeaseId).Returns(lease);
        return lease;
    }

    // ---- StartAsync ----

    [Fact]
    public async Task StartAsync_CreatesAFixedTermLease_MarksTheUnitOccupied_InOneSave_AndAudits()
    {
        Lease? added = null;
        await _repository.AddAsync(Arg.Do<Lease>(l => added = l));
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        added!.TermType.Should().Be(LeaseTermType.Fixed);
        added.EndDate.Should().Be(new DateOnly(2026, 12, 31));
        _unit.OccupancyStatus.Should().Be(UnitAssetOccupancyStatus.Occupied);
        _unit.VacantSince.Should().BeNull();
        await _repository.Received(1).SaveChangesAsync();
        result.Data!.UnitLabel.Should().Be("4B");
        result.Data.PropertyAddress.Should().Be("1 Main St");
        result.Data.TermType.Should().Be("FIXED");
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "LEASE_STARTED", Arg.Any<string>());
    }

    [Fact]
    public async Task StartAsync_WithNoEndDate_IsMonthToMonth_AndWithNoTerm_IsNotRecorded()
    {
        var sut = CreateSut();

        (await sut.StartAsync(new LeaseStartRequest(_unit.UnitAssetId, null, new DateOnly(2026, 1, 1), true, null), _staffId))
            .Data!.TermType.Should().Be("MONTH_TO_MONTH");

        _unit.MarkVacant(new DateOnly(2026, 1, 1));
        (await sut.StartAsync(Start(Guid.NewGuid(), termRecorded: false), _staffId)).Data!.TermType.Should().Be("NOT_RECORDED");
    }

    [Fact]
    public async Task StartAsync_ForAnApplicationThatAlreadyHasALease_ReturnsIt_ARetriedWebhookNeverStartsTwo()
    {
        var applicationId = Guid.NewGuid();
        var existing = ActiveLease(applicationId);
        _repository.GetByLeaseApplicationIdAsync(applicationId).Returns(existing);
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(applicationId), _staffId);

        result.Data!.LeaseId.Should().Be(existing.LeaseId);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task StartAsync_ManualLease_OnAUnitWithAnActiveLease_IsRejected()
    {
        var active = ActiveLease();
        _repository.GetActiveForUnitAsync(_unit.UnitAssetId).Returns(active);
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(), _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3003");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task StartAsync_SignedLease_SupersedesTheUnitsEarlierLease_TheDayBeforeItStarts()
    {
        var earlier = ActiveLease();
        _repository.GetActiveForUnitAsync(_unit.UnitAssetId).Returns(earlier);
        _unit.MarkOccupied();
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(Guid.NewGuid()), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        earlier.Status.Should().Be(LeaseStatus.Ended);
        earlier.EndedOn.Should().Be(new DateOnly(2025, 12, 31));
        earlier.EndReason.Should().Be(LeaseEndReason.Other);
        earlier.EndNote.Should().NotBeNullOrWhiteSpace();
        _unit.OccupancyStatus.Should().Be(UnitAssetOccupancyStatus.Occupied);
        // Saved separately first: the one-active-per-unit index is checked per statement.
        await _repository.Received(2).SaveChangesAsync();
    }

    [Fact]
    public async Task StartAsync_WithAnInvalidTerm_ReturnsInvalidLeaseTerm_AndChangesNothing()
    {
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(end: new DateOnly(2025, 12, 31)), _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3004");
        _unit.OccupancyStatus.Should().Be(UnitAssetOccupancyStatus.Vacant);
    }

    [Fact]
    public async Task StartAsync_OnAnArchivedOrUnknownUnit_ReturnsNotFound()
    {
        _unit.Archive();
        var sut = CreateSut();

        (await sut.StartAsync(Start(), _staffId)).Outcome.Should().Be(ServiceResultOutcome.NotFound);
        (await sut.StartAsync(Start() with { UnitAssetId = Guid.NewGuid() }, _staffId)).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task StartAsync_WhenAConcurrentWebhookDeliveryWonTheRace_ReturnsTheWinnersLease()
    {
        var applicationId = Guid.NewGuid();
        var winner = ActiveLease(applicationId);
        _repository.GetByLeaseApplicationIdAsync(applicationId).Returns((Lease?)null, winner);
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));
        var sut = CreateSut();

        var result = await sut.StartAsync(Start(applicationId), _staffId);

        result.Data!.LeaseId.Should().Be(winner.LeaseId);
    }

    // ---- EndAsync ----

    [Fact]
    public async Task EndAsync_RecordsTheReason_VacatesTheUnitFromTheEndDate_AndAudits()
    {
        var lease = ActiveLease();
        _unit.MarkOccupied();
        var sut = CreateSut();

        var result = await sut.EndAsync(lease.LeaseId, new LeaseEndRequest(new DateOnly(2025, 9, 30), "LESSEE_BREACH", "PM_COMPANY", null), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.EndReason.Should().Be("LESSEE_BREACH");
        result.Data.EndedBy.Should().Be("PM_COMPANY");
        _unit.OccupancyStatus.Should().Be(UnitAssetOccupancyStatus.Vacant);
        _unit.VacantSince.Should().Be(new DateOnly(2025, 9, 30));
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "LEASE_ENDED", Arg.Is<string>(d => d.Contains("LESSEE_BREACH")));
    }

    [Theory]
    [InlineData("NOT_RECORDED", "LESSEE")]
    [InlineData("SOMETHING", "LESSEE")]
    [InlineData("NON_RENEWAL", "LANDLORD")]
    public async Task EndAsync_WithAReasonOrPartyOutsideTheList_IsRejected(string reason, string endedBy)
    {
        var lease = ActiveLease();
        var sut = CreateSut();

        var result = await sut.EndAsync(lease.LeaseId, new LeaseEndRequest(new DateOnly(2025, 9, 30), reason, endedBy, null), _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3005");
        lease.Status.Should().Be(LeaseStatus.Active);
    }

    [Fact]
    public async Task EndAsync_OnAnEndedLease_ReturnsInvalidStatusTransition()
    {
        var lease = ActiveLease();
        lease.End(new DateOnly(2025, 9, 30), LeaseEndReason.NonRenewal, LeaseEndedBy.Lessee, null);
        var sut = CreateSut();

        var result = await sut.EndAsync(lease.LeaseId, new LeaseEndRequest(new DateOnly(2025, 10, 1), "NON_RENEWAL", "LESSEE", null), _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3000");
    }

    // ---- RecordTermAsync / lists ----

    [Fact]
    public async Task RecordTermAsync_RecordsTheTerm_AndAudits()
    {
        var lease = Lease.Start(_tenantId, _unit.UnitAssetId, Guid.NewGuid(), new DateOnly(2026, 1, 1), LeaseTermType.NotRecorded, null, null);
        _repository.GetByIdAsync(lease.LeaseId).Returns(lease);
        var sut = CreateSut();

        var result = await sut.RecordTermAsync(lease.LeaseId, new LeaseTermRequest("FIXED", new DateOnly(2026, 12, 31), "AAAAAAAAAAA="), _staffId);

        result.Data!.TermType.Should().Be("FIXED");
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "LEASE_TERM_RECORDED", Arg.Any<string>());
    }

    [Fact]
    public async Task RecordTermAsync_OnAConcurrentEdit_ReturnsConcurrencyConflict()
    {
        var lease = ActiveLease();
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));
        var sut = CreateSut();

        var result = await sut.RecordTermAsync(lease.LeaseId, new LeaseTermRequest("MONTH_TO_MONTH", null, "AAAAAAAAAAA="), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }

    [Fact]
    public async Task ListExpiringAsync_AsksForLeasesEndingWithinTheWindowFromToday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sut = CreateSut();

        await sut.ListExpiringAsync(60);

        await _repository.Received(1).ListActiveFixedEndingOnOrBeforeAsync(today.AddDays(60));
    }
}
