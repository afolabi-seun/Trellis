using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Services;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.RentLedger;

public sealed class RentPaymentServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly IRentScheduleRepository _scheduleRepository = Substitute.For<IRentScheduleRepository>();
    private readonly IRentPaymentRecordRepository _paymentRepository = Substitute.For<IRentPaymentRecordRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public RentPaymentServiceTests() => TenantContext.Current = new TenantContext(_tenantId);

    public void Dispose() => TenantContext.Current = null;

    private RentPaymentService CreateSut() => new(_scheduleRepository, _paymentRepository, _auditService);

    private RentSchedule ArrangeSchedule(bool ended = false)
    {
        var schedule = RentSchedule.Create(_tenantId, Guid.NewGuid(), null, Guid.NewGuid(), 1000m, "USD", new DateOnly(2026, 1, 1), 5, 5, null);
        if (ended)
        {
            schedule.End();
        }

        _scheduleRepository.GetByIdAsync(schedule.RentScheduleId).Returns(schedule);
        return schedule;
    }

    [Fact]
    public async Task RecordAsync_StoresTheRecord_AsTheActingStaffMember_AndAudits()
    {
        var schedule = ArrangeSchedule();
        RentPaymentRecord? saved = null;
        await _paymentRepository.AddAsync(Arg.Do<RentPaymentRecord>(r => saved = r));
        var sut = CreateSut();

        var result = await sut.RecordAsync(schedule.RentScheduleId, new RentPaymentRecordCreateRequest(1000m, new DateOnly(2026, 1, 8), "EFT ref 88213"), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        saved!.RecordedByUserId.Should().Be(_staffId);
        saved.TenantId.Should().Be(_tenantId);
        saved.MethodReference.Should().Be("EFT ref 88213");
        await _paymentRepository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENT_PAYMENT_RECORDED", Arg.Any<string>());
    }

    [Fact]
    public async Task RecordAsync_ForAnUnknownSchedule_ReturnsNotFound()
    {
        _scheduleRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((RentSchedule?)null);
        var sut = CreateSut();

        var result = await sut.RecordAsync(Guid.NewGuid(), new RentPaymentRecordCreateRequest(1000m, new DateOnly(2026, 1, 8), null), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task RecordAsync_AgainstAnEndedSchedule_ReturnsScheduleNotActive()
    {
        var schedule = ArrangeSchedule(ended: true);
        var sut = CreateSut();

        var result = await sut.RecordAsync(schedule.RentScheduleId, new RentPaymentRecordCreateRequest(1000m, new DateOnly(2026, 1, 8), null), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12002");
        await _paymentRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task VoidAsync_VoidsTheRecord_WithWhoAndWhy_AndAudits()
    {
        var schedule = ArrangeSchedule();
        var record = RentPaymentRecord.Create(_tenantId, schedule.RentScheduleId, 1000m, new DateOnly(2026, 1, 8), null, _staffId);
        _paymentRepository.GetByIdAsync(record.RentPaymentRecordId).Returns(record);
        var voider = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.VoidAsync(schedule.RentScheduleId, record.RentPaymentRecordId, new RentPaymentRecordVoidRequest("Wrong unit"), voider);

        result.Data!.IsVoided.Should().BeTrue();
        record.VoidedBy.Should().Be(voider);
        await _auditService.Received(1).RecordAsync(_tenantId, voider, AuditActorType.HumanUser, "RENT_PAYMENT_VOIDED", Arg.Any<string>());
    }

    [Fact]
    public async Task VoidAsync_ForARecordOnAnotherSchedule_ReturnsNotFound()
    {
        var schedule = ArrangeSchedule();
        var record = RentPaymentRecord.Create(_tenantId, Guid.NewGuid(), 1000m, new DateOnly(2026, 1, 8), null, _staffId);
        _paymentRepository.GetByIdAsync(record.RentPaymentRecordId).Returns(record);
        var sut = CreateSut();

        var result = await sut.VoidAsync(schedule.RentScheduleId, record.RentPaymentRecordId, new RentPaymentRecordVoidRequest("x"), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task VoidAsync_OnAnAlreadyVoidedRecord_ReturnsInvalidStatusTransition()
    {
        var schedule = ArrangeSchedule();
        var record = RentPaymentRecord.Create(_tenantId, schedule.RentScheduleId, 1000m, new DateOnly(2026, 1, 8), null, _staffId);
        record.Void(_staffId, "first");
        _paymentRepository.GetByIdAsync(record.RentPaymentRecordId).Returns(record);
        var sut = CreateSut();

        var result = await sut.VoidAsync(schedule.RentScheduleId, record.RentPaymentRecordId, new RentPaymentRecordVoidRequest("again"), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12000");
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryRecord_IncludingVoidedOnes_ForTheAuditTrail()
    {
        var schedule = ArrangeSchedule();
        var kept = RentPaymentRecord.Create(_tenantId, schedule.RentScheduleId, 1000m, new DateOnly(2026, 1, 8), null, _staffId);
        var voided = RentPaymentRecord.Create(_tenantId, schedule.RentScheduleId, 50m, new DateOnly(2026, 1, 9), null, _staffId);
        voided.Void(_staffId, "typo");
        _paymentRepository.ListForScheduleAsync(schedule.RentScheduleId).Returns([kept, voided]);
        var sut = CreateSut();

        var result = await sut.ListAsync(schedule.RentScheduleId);

        result.Data!.Select(r => r.IsVoided).Should().Equal(false, true);
    }
}
