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
    private readonly ILeaseService _leaseService = Substitute.For<ILeaseService>();
    private readonly Guid _leaseId = Guid.NewGuid();
    private readonly IModuleEntitlementService _entitlements = Substitute.For<IModuleEntitlementService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public RentScheduleServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(true);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(new Dictionary<Guid, decimal>());
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(Unit()));
        // No active lease on the unit by default, so a manual schedule starts one.
        _leaseService.GetActiveForUnitAsync(_unitId).Returns(ServiceResult<LeaseResponse>.NotFound());
        _leaseService.StartAsync(Arg.Any<LeaseStartRequest>(), Arg.Any<Guid?>()).Returns(ServiceResult<LeaseResponse>.Success(Lease("MANUAL")));
        _leaseService.GetByIdAsync(Arg.Any<Guid>()).Returns(ServiceResult<LeaseResponse>.Success(Lease("MANUAL")));
    }

    public void Dispose() => TenantContext.Current = null;

    private RentScheduleService CreateSut() => new(
        _scheduleRepository, _paymentRepository, _unitAssetService, _leaseService, _entitlements, _auditService,
        Options.Create(new RentLedgerOptions { DefaultGracePeriodDays = 5 }));

    private UnitAssetResponse Unit(string occupancy = "VACANT", string status = "ACTIVE") =>
        new(_unitId, Guid.NewGuid(), "Unit 4B", status, occupancy, null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    private LeaseResponse Lease(string source, string status = "ACTIVE") => new(
        _leaseId, _unitId, "Unit 4B", Guid.NewGuid(), "1 Main St", source, source == "LEASING" ? Guid.NewGuid() : null,
        new DateOnly(2026, 1, 1), "FIXED", new DateOnly(2026, 12, 31), 100, status, null, null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    private RentSchedule ActiveSchedule(DateOnly? start = null, decimal rent = 1000m, Guid? unit = null) =>
        RentSchedule.Create(_tenantId, unit ?? _unitId, null, Guid.NewGuid(), rent, "USD", start ?? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6), 5, 5, _staffId);

    private static RentScheduleCreateRequest CreateRequest(
        Guid unitId, int? grace = null, DateOnly? leaseEnd = null, bool? monthToMonth = null, bool withTerm = true) =>
        new(unitId, 1500m, "usd", new DateOnly(2026, 1, 1), 5, grace,
            withTerm && monthToMonth != true ? leaseEnd ?? new DateOnly(2026, 12, 31) : leaseEnd, monthToMonth);

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
    public async Task CreateAsync_SavesTheScheduleWithTheDefaultGracePeriod_StartsAManualLeaseWithItsTerm_AndAudits()
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
        saved.LeaseId.Should().Be(_leaseId);
        result.Data!.UnitLabel.Should().Be("Unit 4B");
        result.Data.LeaseSource.Should().Be("MANUAL");
        // Starting the lease is what marks the unit occupied (BDR-LSE-009).
        await _leaseService.Received(1).StartAsync(
            Arg.Is<LeaseStartRequest>(r => r.UnitAssetId == _unitId && r.LeaseApplicationId == null && r.StartDate == new DateOnly(2026, 1, 1)
                && r.TermRecorded && r.EndDate == new DateOnly(2026, 12, 31)),
            _staffId);
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENT_SCHEDULE_CREATED", Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_MonthToMonth_StartsALeaseWithNoEndDate()
    {
        var sut = CreateSut();

        await sut.CreateAsync(CreateRequest(_unitId, monthToMonth: true), _staffId);

        await _leaseService.Received(1).StartAsync(Arg.Is<LeaseStartRequest>(r => r.TermRecorded && r.EndDate == null), _staffId);
    }

    [Fact]
    public async Task CreateAsync_WithNoLeaseTerm_OnAUnitWithNoLease_IsRejected_BeforeAnythingIsSaved()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId, withTerm: false), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12003");
        await _leaseService.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateAsync_OnAUnitThatAlreadyHasAnActiveLease_AttachesToIt_AndNeedsNoTerm()
    {
        // A Leasing tenant turning Rent Collection on later: the lease exists, the schedule joins it.
        _leaseService.GetActiveForUnitAsync(_unitId).Returns(ServiceResult<LeaseResponse>.Success(Lease("LEASING")));
        RentSchedule? saved = null;
        await _scheduleRepository.AddAsync(Arg.Do<RentSchedule>(s => saved = s));
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId, withTerm: false), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        saved!.LeaseId.Should().Be(_leaseId);
        await _leaseService.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_WhenStartingTheLeaseIsRejected_PassesTheReasonOn_AndSavesNoSchedule()
    {
        _leaseService.StartAsync(Arg.Any<LeaseStartRequest>(), Arg.Any<Guid?>())
            .Returns(ServiceResult<LeaseResponse>.BusinessRuleViolation("TRL-E-AST-3003"));
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3003");
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
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
    public async Task CreateAsync_WhenItLosesARaceAgainstTheUniqueIndex_ReturnsTheFriendlyError()
    {
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));
        var sut = CreateSut();

        var result = await sut.CreateAsync(CreateRequest(_unitId), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12001");
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

    private static readonly RentScheduleEndRequest EndDetails = new(new DateOnly(2026, 6, 30), "NON_RENEWAL", "LESSEE", null);

    [Fact]
    public async Task EndAsync_OnAManualLeasesSchedule_EndsTheLeaseWithItsReason_ThenTheSchedule_ThenRefusesToEndItAgain()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        _leaseService.EndAsync(Arg.Any<Guid>(), Arg.Any<LeaseEndRequest>(), _staffId).Returns(ServiceResult<LeaseResponse>.Success(Lease("MANUAL", "ENDED")));
        var sut = CreateSut();

        (await sut.EndAsync(schedule.RentScheduleId, EndDetails, _staffId)).Outcome.Should().Be(ServiceResultOutcome.Success);
        var second = await sut.EndAsync(schedule.RentScheduleId, EndDetails, _staffId);

        schedule.Status.Should().Be(RentScheduleStatus.Ended);
        await _leaseService.Received(1).EndAsync(
            schedule.LeaseId, Arg.Is<LeaseEndRequest>(r => r.Reason == "NON_RENEWAL" && r.EndedBy == "LESSEE"), _staffId);
        second.ErrorCode.Should().Be("TRL-E-RENT-12000");
    }

    [Fact]
    public async Task EndAsync_OnAManualLeasesSchedule_WithoutEndDetails_IsRejected_AndTheScheduleKeepsRunning()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.EndAsync(schedule.RentScheduleId, null, _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12004");
        schedule.Status.Should().Be(RentScheduleStatus.Active);
        await _leaseService.DidNotReceiveWithAnyArgs().EndAsync(default, default!, default);
    }

    [Fact]
    public async Task EndAsync_WhenEndingTheLeaseIsRejected_PassesTheReasonOn_AndTheScheduleKeepsRunning()
    {
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        _leaseService.EndAsync(Arg.Any<Guid>(), Arg.Any<LeaseEndRequest>(), _staffId).Returns(ServiceResult<LeaseResponse>.BusinessRuleViolation("TRL-E-AST-3005"));
        var sut = CreateSut();

        var result = await sut.EndAsync(schedule.RentScheduleId, EndDetails, _staffId);

        result.ErrorCode.Should().Be("TRL-E-AST-3005");
        schedule.Status.Should().Be(RentScheduleStatus.Active);
    }

    [Fact]
    public async Task EndAsync_OnALeasingLeasesSchedule_EndsOnlyTheSchedule_AndNeedsNoEndDetails()
    {
        // A Leasing-started lease is ended through Leasing; this only stops tracking rent.
        var schedule = ActiveSchedule();
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        _leaseService.GetByIdAsync(schedule.LeaseId).Returns(ServiceResult<LeaseResponse>.Success(Lease("LEASING")));
        var sut = CreateSut();

        var result = await sut.EndAsync(schedule.RentScheduleId, null, _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        await _leaseService.DidNotReceiveWithAnyArgs().EndAsync(default, default!, default);
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

    [Fact]
    public async Task CountInArrearsAsync_CountsOnlyInArrearsSchedules()
    {
        var older = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-8));
        var newer = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-3));
        var paidUp = ActiveSchedule(start: DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6));
        _scheduleRepository.ListActiveForTenantAsync().Returns([newer, paidUp, older]);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, decimal> { [paidUp.RentScheduleId] = 100_000m });
        var sut = CreateSut();

        var result = await sut.CountInArrearsAsync();

        result.Should().Be(2);
    }

    [Fact]
    public async Task CountInArrearsAsync_WhenNotEntitledToRentCollection_ReturnsNull_NotZero()
    {
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        var result = await sut.CountInArrearsAsync();

        result.Should().BeNull();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().ListActiveForTenantAsync();
    }

    // ---- Lease-execution hook ----

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WhenNotEntitledToRentCollection_DoesNothing()
    {
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 17), _staffId);

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

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 17), _staffId);

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

        await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 12, availableDay), _staffId);

        saved!.DueDayOfMonth.Should().Be(expectedDueDay);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_IsIdempotentByLeaseApplication_AWebhookRetryNeverMakesASecondSchedule()
    {
        var applicationId = Guid.NewGuid();
        _scheduleRepository.GetByLeaseApplicationIdAsync(_tenantId, applicationId).Returns(ActiveSchedule());
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

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

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

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

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, applicationId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WhenTheInsertFailsForAnyOtherReason_ReturnsAFailure_SoTheWebhookRetries()
    {
        _scheduleRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("boom")));
        var sut = CreateSut();

        var result = await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task EnsureScheduleForLeaseAsync_WithNoActingUser_AuditsAsTheSystem()
    {
        var sut = CreateSut();

        await sut.EnsureScheduleForLeaseAsync(_tenantId, _unitId, Guid.NewGuid(), Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), null);

        await _auditService.Received(1).RecordAsync(_tenantId, _tenantId, AuditActorType.System, "RENT_SCHEDULE_CREATED", Arg.Any<string>());
    }

    // ---- EndForLeaseAsync ----

    [Fact]
    public async Task EndForLeaseAsync_EndsTheScheduleForThatLeaseApplication_AndAudits()
    {
        var leaseApplicationId = Guid.NewGuid();
        var schedule = RentSchedule.Create(_tenantId, _unitId, leaseApplicationId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 1, 1), 1, 5, null);
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
        var schedule = RentSchedule.Create(_tenantId, _unitId, leaseApplicationId, Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 1, 1), 1, 5, null);
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
        var current = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", start, 5, 5, null);
        var overdue = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", start, 5, 5, null);
        var arrearsLater = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", start, 5, 1, null);
        var ended = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", start, 5, 0, null);
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
        var usdSchedule = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", start, 5, 5, null);
        var zarSchedule = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 15000m, "ZAR", start, 5, 5, null);
        _scheduleRepository.ListForUnitsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns([usdSchedule, zarSchedule]);
        _paymentRepository.GetReceivedInPeriodByCurrencyAsync(Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new Dictionary<string, decimal> { ["USD"] = 1000m, ["ZAR"] = 15000m });
        var sut = CreateSut();

        var result = await sut.GetOwnerRentSummaryAsync([Guid.NewGuid()], start, start.AddMonths(1), start);

        result!.ReceivedByCurrency.Should().BeEquivalentTo([new CurrencyAmount("USD", 1000m), new CurrencyAmount("ZAR", 15000m)]);
    }

    // ---- ScheduleRentChangeForLeaseAsync (BDR-LSE-010) ----

    [Fact]
    public async Task ScheduleRentChangeForLeaseAsync_AddsADatedChangeToTheLeasesActiveSchedule_AndAudits()
    {
        var schedule = ActiveSchedule(start: new DateOnly(2026, 1, 1));
        _scheduleRepository.GetActiveForLeaseAsync(_tenantId, _leaseId).Returns(schedule);
        var offerId = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, new DateOnly(2027, 1, 1), 1200m, offerId, _staffId);

        result.Data.Should().BeTrue();
        schedule.RentChanges.Should().ContainSingle().Which.RenewalOfferId.Should().Be(offerId);
        await _scheduleRepository.Received(1).AddRentChangeAsync(Arg.Is<RentChange>(c => c.RentAmount == 1200m));
        await _scheduleRepository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENT_CHANGE_SCHEDULED", Arg.Any<string>());
    }

    [Fact]
    public async Task ScheduleRentChangeForLeaseAsync_ARetryOnTheSameDate_AddsNoSecondRow()
    {
        var schedule = ActiveSchedule(start: new DateOnly(2026, 1, 1));
        schedule.ScheduleRentChange(new DateOnly(2027, 1, 1), 1200m, null, _staffId);
        _scheduleRepository.GetActiveForLeaseAsync(_tenantId, _leaseId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, new DateOnly(2027, 1, 1), 1200m, null, _staffId);

        result.Data.Should().BeTrue();
        await _scheduleRepository.DidNotReceiveWithAnyArgs().AddRentChangeAsync(default!);
    }

    [Fact]
    public async Task ScheduleRentChangeForLeaseAsync_WithNoRentCollection_OrNoSchedule_ChangesNothing()
    {
        var sut = CreateSut();
        _scheduleRepository.GetActiveForLeaseAsync(_tenantId, _leaseId).Returns((RentSchedule?)null);
        (await sut.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, new DateOnly(2027, 1, 1), 1200m, null, _staffId)).Data.Should().BeFalse();

        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        (await sut.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, new DateOnly(2027, 1, 1), 1200m, null, _staffId)).Data.Should().BeFalse();

        await _scheduleRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task ScheduleRentChangeForLeaseAsync_BeforeTheScheduleStarts_IsRejected()
    {
        _scheduleRepository.GetActiveForLeaseAsync(_tenantId, _leaseId).Returns(ActiveSchedule(start: new DateOnly(2026, 1, 1)));
        var sut = CreateSut();

        var result = await sut.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, new DateOnly(2025, 12, 1), 1200m, null, _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12005");
    }

    [Fact]
    public async Task GetByIdAsync_ShowsTheRentInEffectToday_AndTheUpcomingChange()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedule = ActiveSchedule(start: today.AddMonths(-3));
        schedule.ScheduleRentChange(today.AddMonths(2), 1400m, null, _staffId);
        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(schedule.RentScheduleId);

        result.Data!.RentAmount.Should().Be(1000m);
        result.Data.UpcomingRentAmount.Should().Be(1400m);
        result.Data.UpcomingRentFrom.Should().Be(today.AddMonths(2));
    }
}
