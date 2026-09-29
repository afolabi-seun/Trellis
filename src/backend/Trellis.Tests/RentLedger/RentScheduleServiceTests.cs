using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Services;
using Trellis.Api.Modules.RentLedger.Configuration;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.RentLedger;

public sealed class RentScheduleServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly IRentScheduleRepository _scheduleRepository = Substitute.For<IRentScheduleRepository>();
    private readonly IRentPaymentRecordRepository _paymentRepository = Substitute.For<IRentPaymentRecordRepository>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IModuleEntitlementService _entitlements = Substitute.For<IModuleEntitlementService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public RentScheduleServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(true);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(new Dictionary<Guid, decimal>());
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(Unit()));
        _unitAssetService.MarkOccupiedAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(Unit("OCCUPIED")));
    }

    public void Dispose() => TenantContext.Current = null;

    private RentScheduleService CreateSut() => new(
        _scheduleRepository, _paymentRepository, _unitAssetService, _entitlements, _auditService,
        Options.Create(new RentLedgerOptions { DefaultGracePeriodDays = 5 }));

    private UnitAssetResponse Unit(string occupancy = "VACANT", string status = "ACTIVE") =>
        new(_unitId, Guid.NewGuid(), "Unit 4B", status, occupancy, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    private RentSchedule ActiveSchedule(DateOnly? start = null, decimal rent = 1000m, Guid? unit = null) =>
        RentSchedule.Create(_tenantId, unit ?? _unitId, null, rent, "USD", start ?? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6), 5, 5, _staffId);

    private static RentScheduleCreateRequest CreateRequest(Guid unitId, int? grace = null) =>
        new(unitId, 1500m, "usd", new DateOnly(2026, 1, 1), 5, grace);

    // ---- CreateAsync (manual) ----

    [Fact]
    public async Task CreateAsync_ForAnUnknownUnit_ReturnsNotFound()
    {
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.NotFound());
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateAsync_ForAnArchivedUnit_ReturnsNotFound()
    {
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(Unit(status: "ARCHIVED")));
        var sut = CreateSut();

        (await sut.CreateAsync(CreateRequest(_unitId), _staffId)).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task CreateAsync_WhenTheUnitAlreadyHasAnActiveSchedule_ReturnsTheFriendlyError()
    {
        _scheduleRepository.GetActiveForUnitAsync(_tenantId, _unitId).Returns(ActiveSchedule());
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        result.ErrorCode.Should().Be("TRL-E-RENT-12001");
    }

    [Fact]
    public async Task CreateAsync_SavesTheScheduleWithTheDefaultGracePeriod_MarksTheUnitOccupied_AndAudits()
    {
        RentSchedule? saved = null;
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(s => saved = s));
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        saved!.GracePeriodDays.Should().Be(5);
        saved.Currency.Should().Be("USD");
        saved.TenantId.Should().Be(_tenantId);
        saved.LeaseApplicationId.Should().BeNull();
        result.Data!.UnitLabel.Should().Be("Unit 4B");
        await _unitAssetService.Received(1).MarkOccupiedAsync(_unitId);
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENT_SCHEDULE_CREATED", Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_HonoursAnExplicitGracePeriod()
    {
        RentSchedule? saved = null;
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(s => saved = s));
        var sut = CreateSut();

        await sut.CreateAsync(CreateRequest(_unitId, grace: 12), _staffId);

        saved!.GracePeriodDays.Should().Be(12);
    }

    [Fact]
    public async Task CreateAsync_WhenTheUnitIsAlreadyOccupied_StillSucceeds()
    {
        _unitAssetService.MarkOccupiedAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.BusinessRuleViolation("TRL-E-AST-3000"));
        var sut = CreateSut();

        (await sut.CreateAsync(CreateRequest(_unitId), _staffId)).Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenItLosesARaceAgainstTheUniqueIndex_ReturnsTheFriendlyError_AndDoesNotMarkTheUnitOccupied()
    {
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12001");
        await _unitAssetService.DidNotReceiveWithAnyArgs().MarkOccupiedAsync(default);
    }

    // ---- Update / End ----

    [Fact]
    public async Task UpdateAsync_OnAnEndedSchedule_ReturnsScheduleNotActive()
    {
        var schedule = ActiveSchedule();
        schedule.End();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.UpdateAsync(schedule.RentScheduleId, new RentScheduleUpdateRequest(1200m, 1, 3, "AAAAAAAAAAA="), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12002");
    }

    [Fact]
    public async Task UpdateAsync_OnAConcurrentEdit_ReturnsConcurrencyConflict()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));
        var sut = CreateSut();

        var result = await sut.UpdateAsync(schedule.RentScheduleId, new RentScheduleUpdateRequest(1200m, 1, 3, "AAAAAAAAAAA="), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheTermsAndAudits()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.UpdateAsync(schedule.RentScheduleId, new RentScheduleUpdateRequest(1200m, 1, 3, "AAAAAAAAAAA="), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        schedule.RentAmount.Should().Be(1200m);
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENT_SCHEDULE_UPDATED", Arg.Any<string>());
    }

    [Fact]
    public async Task EndAsync_EndsTheSchedule_ThenRefusesToEndItAgain()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        (await sut.EndAsync(schedule.RentScheduleId, _staffId)).Outcome.Should().Be(ServiceResultOutcome.Success);
        var second = await sut.EndAsync(schedule.RentScheduleId, _staffId);

        second.ErrorCode.Should().Be("TRL-E-RENT-12000");
    }

    [Fact]
    public async Task GetByIdAsync_ForAnEndedSchedule_HasNoRentStatus()
    {
        var schedule = ActiveSchedule();
        schedule.End();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(schedule.RentScheduleId);

        result.Data!.Status.Should().Be("ENDED");
        result.Data.RentStatus.Should().BeNull();
    }

    // ---- Arrears list ----

    [Fact]
    public async Task ListArrearsAsync_ReturnsOnlyInArrearsSchedules_OldestUnpaidFirst()
    {
        var older = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-8));
        var newer = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-3));
        var paidUp = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6));
        _scheduleRepository.ListActiveForTenantAsync().Returns([newer, paidUp, older]);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, decimal> { [paidUp.RentScheduleId] = 100_000m });
        var sut = CreateSut();

        var result = await sut.ListArrearsAsync();

        result.Data!.Select(r => r.RentScheduleId).Should().Equal(older.RentScheduleId, newer.RentScheduleId);
        result.Data.Should().OnlyContain(r => r.RentStatus == "IN_ARREARS" && r.PeriodsInArrears > 0);
    }

    // ---- Lease-execution hook ----

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WhenNotEntitledToRentCollection_DoesNothing()
    {
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 17), _staffId);

        result.Data.Should().BeFalse();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_CreatesTheScheduleFromTheLeaseTerms_WithAnAvailabilityDerivedDueDay()
    {
        var applicationId = Guid.NewGuid();
        RentSchedule? saved = null;
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(s => saved = s));
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, 1500m, "USD", new DateOnly(2026, 11, 17), _staffId);

        result.Data.Should().BeTrue();
        saved!.LeaseApplicationId.Should().Be(applicationId);
        saved.RentAmount.Should().Be(1500m);
        saved.StartDate.Should().Be(new DateOnly(2026, 11, 17));
        saved.DueDayOfMonth.Should().Be(17);
        saved.GracePeriodDays.Should().Be(5);
    }

    [Theory]
    [InlineData(28, 28)]
    [InlineData(29, 28)]
    [InlineData(31, 28)]
    public async Task EnsureScheduleForLeaseAsync_CapsTheDerivedDueDayAt28(int availableDay, int expectedDueDay)
    {
        RentSchedule? saved = null;
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(s => saved = s));
        var sut = CreateSut();

        await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 12, availableDay), _staffId);

        saved!.DueDayOfMonth.Should().Be(expectedDueDay);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_IsIdempotentByLeaseApplication_AWebhookRetryNeverMakesASecondSchedule()
    {
        var applicationId = Guid.NewGuid();
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, applicationId).Returns(ActiveSchedule());
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeFalse();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_EndsThePriorTenanciesScheduleFirst_SoASignedLeaseIsNeverBlocked()
    {
        var prior = ActiveSchedule();
        _scheduleRepository.GetActiveForUnitAsync(_tenantId, _unitId).Returns(prior);
        var order = new List<string>();
        _scheduleRepository.SaveChangesAsync().Returns(_ => { order.Add(prior.Status == RentScheduleStatus.Ended ? "save-after-end" : "save"); return Task.CompletedTask; });
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(_ => order.Add("add")));
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Data.Should().BeTrue();
        prior.Status.Should().Be(RentScheduleStatus.Ended);
        // The partial unique index is checked per statement, so the end must be saved before the new insert.
        order.Should().Equal("save-after-end", "add", "save-after-end");
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WhenAConcurrentDeliveryWonTheRace_TreatsItAsAlreadyDone()
    {
        var applicationId = Guid.NewGuid();
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, applicationId).Returns(null as RentSchedule, ActiveSchedule());
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WhenTheInsertFailsForAnyOtherReason_ReturnsAFailure_SoTheWebhookRetries()
    {
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("boom")));
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WithNoActingUser_AuditsAsTheSystem()
    {
        var sut = CreateSut();

        await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), null);

        await _auditService.Received(1).RecordAsync(_tenantId, _tenantId, AuditActorType.System, "RENT_SCHEDULE_CREATED", Arg.Any<string>());
    }

    // ---- EndForLeaseAsync ----

    [Fact]
    public async Task EndForLeaseAsync_EndsTheScheduleForThatLeaseApplication_AndAudits()
    {
        var leaseApplicationId = Guid.NewGuid();
        var schedule = RentSchedule.Create(_tenantId, _unitId, leaseApplicationId, 1500m, "USD", new DateOnly(2026, 1, 1), 1, 5, null);
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, leaseApplicationId).Returns(schedule);
        var staffId = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.EndForLeaseAsync(_tenantId, leaseApplicationId, staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeTrue();
        schedule.Status.Should().Be(RentScheduleStatus.Ended);
        await _scheduleRepository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(_tenantId, staffId, AuditActorType.HumanUser, "RENT_SCHEDULE_ENDED", Arg.Any<string>());
    }

    [Fact]
    public async Task EndForLeaseAsync_WhenThereIsNoScheduleForThatLease_IsAToleratedNoOp()
    {
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, Arg.Any<Guid>()).Returns((RentSchedule?)null);
        var sut = CreateSut();

        var result = await sut.EndForLeaseAsync(_tenantId, Guid.NewGuid(), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeFalse();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task EndForLeaseAsync_WhenTheScheduleIsAlreadyEnded_IsAToleratedNoOp()
    {
        var leaseApplicationId = Guid.NewGuid();
        var schedule = RentSchedule.Create(_tenantId, _unitId, leaseApplicationId, 1500m, "USD", new DateOnly(2026, 1, 1), 1, 5, null);
        schedule.End();
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, leaseApplicationId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.EndForLeaseAsync(_tenantId, leaseApplicationId, Guid.NewGuid());

        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task EndForLeaseAsync_WhenNotEntitled_IsAToleratedNoOp_AndNeverLooksUpTheSchedule()
    {
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        var result = await sut.EndForLeaseAsync(_tenantId, Guid.NewGuid(), Guid.NewGuid());

        result.Data.Should().BeFalse();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().GetByLeaseApplicationIdAsync(default, default);
    }

    // ---- Owner report rollup ----

    [Fact]
    public async Task GetOwnerRentSummaryAsync_WhenNotEntitled_ReturnsNull_SoTheReportOmitsRentRatherThanShowingZero()
    {
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        var result = await sut.GetOwnerRentSummaryAsync([_unitId], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 1));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOwnerRentSummaryAsync_ForAnOwnerWithNoUnits_ReturnsZeros_WithoutQuerying()
    {
        var sut = CreateSut();

        var result = await sut.GetOwnerRentSummaryAsync([], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 1));

        result.Should().BeEquivalentTo(new OwnerRentSummary(0, 0, 0, []));
        await _scheduleRepository.DidNotReceiveWithAnyArgs().ListForUnitsAsync(default!);
    }

    [Fact]
    public async Task GetOwnerRentSummaryAsync_CountsCurrentOverdueAndArrears_IgnoresEndedSchedules_AndSumsReceivedInPeriod()
    {
        var asOf = new DateOnly(2026, 1, 8);
        var start = new DateOnly(2026, 1, 1);
        var current = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 1000m, "USD", start, 5, 5, null);
        var overdue = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 1000m, "USD", start, 5, 5, null);
        var arrearsLater = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 1000m, "USD", start, 5, 1, null);
        var ended = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 1000m, "USD", start, 5, 0, null);
        ended.End();
        _scheduleRepository.ListForUnitsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns([current, overdue, arrearsLater, ended]);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, decimal> { [current.RentScheduleId] = 1000m });
        _paymentRepository.GetReceivedInPeriodByCurrencyAsync(Arg.Any<IReadOnlyDictionary<Guid, string>>(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31))
            .Returns(new Dictionary<string, decimal> { ["USD"] = 1000m });
        var sut = CreateSut();

        var result = await sut.GetOwnerRentSummaryAsync([Guid.NewGuid()], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), asOf);

        // Jan 8: 3 days past the 5th. Grace 5 → overdue; grace 1 → in arrears; the ended one is excluded.
        result.Should().BeEquivalentTo(new OwnerRentSummary(1, 1, 1, [new CurrencyAmount("USD", 1000m)]));
    }

    [Fact]
    public async Task GetOwnerRentSummaryAsync_KeepsDifferentCurrenciesSeparate_NeverSummingThemTogether()
    {
        var start = new DateOnly(2026, 1, 1);
        var usdSchedule = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 1000m, "USD", start, 5, 5, null);
        var zarSchedule = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, 15000m, "ZAR", start, 5, 5, null);
        _scheduleRepository.ListForUnitsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns([usdSchedule, zarSchedule]);
        _paymentRepository.GetReceivedInPeriodByCurrencyAsync(Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new Dictionary<string, decimal> { ["USD"] = 1000m, ["ZAR"] = 15000m });
        var sut = CreateSut();

        var result = await sut.GetOwnerRentSummaryAsync([Guid.NewGuid()], start, start.AddMonths(1), start);

        result!.ReceivedByCurrency.Should().BeEquivalentTo([new CurrencyAmount("USD", 1000m), new CurrencyAmount("ZAR", 15000m)]);
    }
}
