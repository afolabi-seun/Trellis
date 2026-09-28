using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Services;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.RentLedger;

/// <summary>
/// BDR-RENT-003/004. Every schedule here starts 2026-01-01, rent 1000, due the 5th, 5 days' grace, so
/// an as-of of 2026-01-20 puts January in arrears (alert key 2026-01-05) and 2026-02-20 adds February
/// (key 2026-02-05).
/// </summary>
public sealed class RentArrearsServiceTests : IDisposable
{
    private static readonly DateOnly January20 = new(2026, 1, 20);
    private static readonly DateOnly February20 = new(2026, 2, 20);

    private static readonly string[] AdminOnlyRoles = ["TENANT_ADMIN"];
    private static readonly string[] StaffOnlyRoles = ["DEPARTMENT_STAFF"];

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IRentScheduleRepository _scheduleRepository = Substitute.For<IRentScheduleRepository>();
    private readonly IRentPaymentRecordRepository _paymentRepository = Substitute.For<IRentPaymentRecordRepository>();
    private readonly IModuleEntitlementService _entitlements = Substitute.For<IModuleEntitlementService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ITenantSettingsService _tenantSettingsService = Substitute.For<ITenantSettingsService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public RentArrearsServiceTests()
    {
        _entitlements.IsEntitledAsync(Arg.Any<Guid>(), "RENT_COLLECTION").Returns(true);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(new Dictionary<Guid, decimal>());
        _tenantSettingsService.GetNotificationSettingsAsync(Arg.Any<Guid>()).Returns(
            new NotificationSettingsDto(["TENANT_ADMIN", "DEPARTMENT_STAFF"], ["TENANT_ADMIN", "DEPARTMENT_STAFF"]));
        _authService.GetUsersByRolesAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<string>>()).Returns(
            (IReadOnlyList<AppUserSummary>)[new AppUserSummary(Guid.NewGuid(), "admin@example.com", null), new AppUserSummary(Guid.NewGuid(), "staff@example.com", null)]);
        _unitAssetService.GetByIdAsync(Arg.Any<Guid>()).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(Guid.NewGuid(), Guid.NewGuid(), "Unit 4B", "ACTIVE", "OCCUPIED", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
    }

    public void Dispose() => TenantContext.Current = null;

    private RentArrearsService CreateSut() => new(
        _scheduleRepository, _paymentRepository, _entitlements, _authService, _tenantSettingsService, _unitAssetService, _notificationService, _auditService,
        NullLogger<RentArrearsService>.Instance);

    private RentSchedule Schedule(Guid? tenantId = null) =>
        RentSchedule.Create(tenantId ?? _tenantId, Guid.NewGuid(), null, 1000m, "USD", new DateOnly(2026, 1, 1), 5, 5, null);

    private void Arrange(params RentSchedule[] schedules)
    {
        _scheduleRepository.ListActiveAcrossTenantsAsync(null, Arg.Any<int>()).Returns(schedules.ToList());
        if (schedules.Length > 0)
        {
            _scheduleRepository.ListActiveAcrossTenantsAsync(schedules[^1].RentScheduleId, Arg.Any<int>()).Returns([]);
        }
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_NotifiesEveryStaffMemberOnce_MarksThePeriod_AndAuditsAsTheSystem()
    {
        var schedule = Schedule();
        Arrange(schedule);
        var sut = CreateSut();

        var raised = await sut.RaiseArrearsAlertsAsync(January20);

        raised.Should().Be(1);
        foreach (var email in new[] { "admin@example.com", "staff@example.com" })
        {
            await _notificationService.Received(1).SendAsync(
                _tenantId, "RENT_ARREARS_FLAGGED", email, Arg.Any<object>(),
                Arg.Is<RelatedEntityRef>(r => r.EntityType == "RentSchedule" && r.EntityId == schedule.RentScheduleId),
                Arg.Any<NotificationRecipientType?>());
        }

        schedule.LastArrearsAlertedPeriod.Should().Be(new DateOnly(2026, 1, 5));
        await _scheduleRepository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(_tenantId, _tenantId, AuditActorType.System, "RENT_ARREARS_FLAGGED", Arg.Any<string>());
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_RunTwiceForTheSamePeriod_AlertsOnlyOnce()
    {
        var schedule = Schedule();
        Arrange(schedule);
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);
        var second = await sut.RaiseArrearsAlertsAsync(January20.AddDays(3));

        second.Should().Be(0);
        await _notificationService.Received(2).SendAsync( // 2 staff, from the first run only
            Arg.Any<Guid?>(), "RENT_ARREARS_FLAGGED", Arg.Any<string>(), Arg.Any<object>(), Arg.Any<RelatedEntityRef?>(), Arg.Any<NotificationRecipientType?>());
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_WhenANewPeriodSlipsPastGrace_AlertsAgain()
    {
        var schedule = Schedule();
        Arrange(schedule);
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);
        var february = await sut.RaiseArrearsAlertsAsync(February20);

        february.Should().Be(1);
        schedule.LastArrearsAlertedPeriod.Should().Be(new DateOnly(2026, 2, 5));
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_ForAScheduleStillInsideItsGraceWindow_DoesNotAlert()
    {
        var schedule = Schedule();
        Arrange(schedule);
        var sut = CreateSut();

        var raised = await sut.RaiseArrearsAlertsAsync(new DateOnly(2026, 1, 8));

        raised.Should().Be(0);
        await _notificationService.DidNotReceiveWithAnyArgs().SendAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_ForAPaidUpSchedule_DoesNotAlert()
    {
        var schedule = Schedule();
        Arrange(schedule);
        _paymentRepository.GetReceivedTotalsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, decimal> { [schedule.RentScheduleId] = 1000m });
        var sut = CreateSut();

        (await sut.RaiseArrearsAlertsAsync(January20)).Should().Be(0);
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_ForATenantNotEntitledToRentCollection_SendsNothing()
    {
        var schedule = Schedule();
        Arrange(schedule);
        _entitlements.IsEntitledAsync(_tenantId, "RENT_COLLECTION").Returns(false);
        var sut = CreateSut();

        (await sut.RaiseArrearsAlertsAsync(January20)).Should().Be(0);
        await _notificationService.DidNotReceiveWithAnyArgs().SendAsync(default, default!, default!, default!);
        schedule.LastArrearsAlertedPeriod.Should().BeNull();
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_ChecksEachTenantsEntitlementOnce_NotOncePerSchedule()
    {
        Arrange(Schedule(), Schedule(), Schedule());
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        await _entitlements.Received(1).IsEntitledAsync(_tenantId, "RENT_COLLECTION");
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_HandlesSchedulesAcrossTenants_EachNotifiedThroughItsOwnTenant()
    {
        var otherTenant = Guid.NewGuid();
        Arrange(Schedule(), Schedule(otherTenant));
        var sut = CreateSut();

        var raised = await sut.RaiseArrearsAlertsAsync(January20);

        raised.Should().Be(2);
        await _authService.Received(1).GetUsersByRolesAsync(_tenantId, Arg.Any<IEnumerable<string>>());
        await _authService.Received(1).GetUsersByRolesAsync(otherTenant, Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_OneFailingSchedule_DoesNotStarveTheOthers_AndItsChangesAreDiscarded()
    {
        var failing = Schedule();
        var healthy = Schedule();
        Arrange(failing, healthy);
        _unitAssetService.GetByIdAsync(failing.UnitAssetId).Returns<ServiceResult<UnitAssetResponse>>(_ => throw new InvalidOperationException("boom"));
        var sut = CreateSut();

        var raised = await sut.RaiseArrearsAlertsAsync(January20);

        raised.Should().Be(1);
        _scheduleRepository.Received(1).DiscardChanges(failing);
        healthy.LastArrearsAlertedPeriod.Should().Be(new DateOnly(2026, 1, 5));
        failing.LastArrearsAlertedPeriod.Should().BeNull();
    }

    /// <summary>The Phase 9 regression class: IUnitAssetService.GetByIdAsync is a Scoped()/TenantContext-shaped call, so the tenant must already be ambient when it runs — and must not leak afterwards.</summary>
    [Fact]
    public async Task RaiseArrearsAlertsAsync_HasTheSchedulesTenantAmbient_WhenTheCrossModuleUnitLookupRuns_AndClearsItAfterwards()
    {
        var schedule = Schedule();
        Arrange(schedule);
        var seen = new List<Guid?>();
        _unitAssetService.GetByIdAsync(schedule.UnitAssetId).Returns(_ =>
        {
            seen.Add(TenantContext.Current?.TenantId);
            return ServiceResult<UnitAssetResponse>.Success(
                new UnitAssetResponse(schedule.UnitAssetId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "OCCUPIED", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow));
        });
        TenantContext.Current = null;
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        seen.Should().Equal(_tenantId);
        TenantContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_PutsTheUnitLabelAndAmountsInTheEmail_WordedAsRecordedPayments()
    {
        var schedule = Schedule();
        Arrange(schedule);
        object? data = null;
        await _notificationService.SendAsync(
            Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(d => data = d), Arg.Any<RelatedEntityRef?>(), Arg.Any<NotificationRecipientType?>());
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        var text = data!.ToString()!;
        text.Should().Contain("Unit 4B").And.Contain("1000.00").And.Contain("USD").And.Contain("2026-01-05");
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_NotifiesOnlyTheRolesTheTenantChose_ForRentArrears()
    {
        Arrange(Schedule());
        _tenantSettingsService.GetNotificationSettingsAsync(_tenantId).Returns(
            new NotificationSettingsDto(["TENANT_ADMIN", "DEPARTMENT_STAFF"], ["TENANT_ADMIN"]));
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        await _authService.Received(1).GetUsersByRolesAsync(_tenantId, Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(AdminOnlyRoles)));
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_ReadsEachTenantsRecipientSettingsOnce_NotOncePerSchedule()
    {
        Arrange(Schedule(), Schedule(), Schedule());
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        await _tenantSettingsService.Received(1).GetNotificationSettingsAsync(_tenantId);
    }

    [Fact]
    public async Task RaiseArrearsAlertsAsync_EachTenantUsesItsOwnRecipientSetting()
    {
        var otherTenant = Guid.NewGuid();
        Arrange(Schedule(), Schedule(otherTenant));
        _tenantSettingsService.GetNotificationSettingsAsync(_tenantId).Returns(
            new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN"]));
        _tenantSettingsService.GetNotificationSettingsAsync(otherTenant).Returns(
            new NotificationSettingsDto(["DEPARTMENT_STAFF"], ["DEPARTMENT_STAFF"]));
        var sut = CreateSut();

        await sut.RaiseArrearsAlertsAsync(January20);

        await _authService.Received(1).GetUsersByRolesAsync(_tenantId, Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(AdminOnlyRoles)));
        await _authService.Received(1).GetUsersByRolesAsync(otherTenant, Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(StaffOnlyRoles)));
    }
}
