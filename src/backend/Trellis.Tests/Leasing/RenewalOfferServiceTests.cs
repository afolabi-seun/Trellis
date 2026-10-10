using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class RenewalOfferServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _leaseId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly IRenewalOfferRepository _repository = Substitute.For<IRenewalOfferRepository>();
    private readonly ILeaseService _leaseService = Substitute.For<ILeaseService>();
    private readonly IRentScheduleService _rentScheduleService = Substitute.For<IRentScheduleService>();
    private readonly ITenantSettingsService _settings = Substitute.For<ITenantSettingsService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly CurrentEnd = Today.AddDays(60);
    private static readonly DateOnly NewEnd = CurrentEnd.AddYears(1);

    public RenewalOfferServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        ArrangeLease();
        _leaseService.RenewAsync(_leaseId, Arg.Any<LeaseRenewRequest>(), _staffId)
            .Returns(ServiceResult<LeaseResponse>.Success(Lease(end: NewEnd)));
        _rentScheduleService.ScheduleRentChangeForLeaseAsync(
                _tenantId, _leaseId, Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid?>(), _staffId)
            .Returns(ServiceResult<bool>.Success(true));
        _settings.GetRenewalDeclineReasonsAsync(_tenantId).Returns(["Rent too high"]);
    }

    public void Dispose() => TenantContext.Current = null;

    private RenewalOfferService CreateSut() => new(_repository, _leaseService, _rentScheduleService, _settings, _auditService);

    private LeaseResponse Lease(string status = "ACTIVE", string termType = "FIXED", DateOnly? end = null) => new(
        _leaseId, _unitId, "Unit 4B", Guid.NewGuid(), "1 Main St", "LEASING", Guid.NewGuid(), Today.AddYears(-1), termType,
        termType == "FIXED" ? end ?? CurrentEnd : null, 60, status, null, null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow);

    private void ArrangeLease(string status = "ACTIVE", string termType = "FIXED") =>
        _leaseService.GetByIdAsync(_leaseId).Returns(ServiceResult<LeaseResponse>.Success(Lease(status, termType)));

    private RenewalOfferCreateRequest Request(string term = "FIXED", DateOnly? end = null, decimal? rent = 1200m) =>
        new(_leaseId, term, term == "FIXED" ? end ?? NewEnd : null, rent, rent is null ? null : CurrentEnd.AddDays(1), Today.AddDays(14));

    private RenewalOffer ArrangeOffer(decimal? rent = 1200m, DateOnly? respondBy = null)
    {
        var offer = RenewalOffer.Create(
            _tenantId, _leaseId, _unitId, RenewalTerm.Fixed, NewEnd, rent, rent is null ? null : CurrentEnd.AddDays(1),
            respondBy ?? Today, Today, _staffId);
        _repository.GetByIdAsync(offer.RenewalOfferId).Returns(offer);
        return offer;
    }

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_RecordsTheOffer_WithTheLeasesDetails_AndAudits()
    {
        RenewalOffer? added = null;
        await _repository.AddAsync(Arg.Do<RenewalOffer>(o => added = o));
        var sut = CreateSut();

        var result = await sut.CreateAsync(Request(), _staffId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        added!.UnitAssetId.Should().Be(_unitId);
        result.Data!.Status.Should().Be("OFFERED");
        result.Data.UnitLabel.Should().Be("Unit 4B");
        result.Data.CurrentLeaseEndDate.Should().Be(CurrentEnd);
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENEWAL_OFFERED", Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_OnAnEndedLease_IsRejected()
    {
        ArrangeLease(status: "ENDED");

        (await CreateSut().CreateAsync(Request(), _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11007");
    }

    [Fact]
    public async Task CreateAsync_WhileAnotherOfferIsOpen_IsRejected()
    {
        _repository.HasOpenOfferForLeaseAsync(_leaseId).Returns(true);

        (await CreateSut().CreateAsync(Request(), _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11008");
    }

    [Fact]
    public async Task CreateAsync_ThatDoesntExtendTheLease_IsRejected()
    {
        var sut = CreateSut();

        (await sut.CreateAsync(Request(end: CurrentEnd), _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11009");

        ArrangeLease(termType: "MONTH_TO_MONTH");
        (await sut.CreateAsync(Request(term: "MONTH_TO_MONTH"), _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11009");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateAsync_WhenItLosesTheOneOpenOfferRace_ReturnsTheFriendlyError()
    {
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));

        (await CreateSut().CreateAsync(Request(), _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11008");
    }

    // ---- AcceptAsync ----

    [Fact]
    public async Task AcceptAsync_ChangesTheRentFromItsDate_ThenExtendsTheSameLease_AndClosesTheOffer()
    {
        var offer = ArrangeOffer();
        var order = new List<string>();
        _rentScheduleService.ScheduleRentChangeForLeaseAsync(_tenantId, _leaseId, CurrentEnd.AddDays(1), 1200m, offer.RenewalOfferId, _staffId)
            .Returns(_ => { order.Add("rent"); return ServiceResult<bool>.Success(true); });
        _leaseService.RenewAsync(_leaseId, Arg.Is<LeaseRenewRequest>(r => r.TermType == "FIXED" && r.NewEndDate == NewEnd), _staffId)
            .Returns(_ => { order.Add("lease"); return ServiceResult<LeaseResponse>.Success(Lease(end: NewEnd)); });
        var sut = CreateSut();

        var result = await sut.AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId);

        result.Data!.Status.Should().Be("ACCEPTED");
        result.Data.RentChangeApplied.Should().BeTrue();
        result.Data.CurrentLeaseEndDate.Should().Be(NewEnd);
        order.Should().Equal("rent", "lease");
        await _auditService.Received(1).RecordAsync(_tenantId, _staffId, AuditActorType.HumanUser, "RENEWAL_ACCEPTED", Arg.Any<string>());
    }

    [Fact]
    public async Task AcceptAsync_WithNoNewRent_TouchesNoRentSchedule()
    {
        var offer = ArrangeOffer(rent: null);

        var result = await CreateSut().AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId);

        result.Data!.RentChangeApplied.Should().BeNull();
        await _rentScheduleService.DidNotReceiveWithAnyArgs().ScheduleRentChangeForLeaseAsync(default, default, default, default, default, default);
    }

    [Fact]
    public async Task AcceptAsync_WhenNothingTracksTheRent_SaysSo_RatherThanClaimingTheRentChanged()
    {
        var offer = ArrangeOffer();
        _rentScheduleService.ScheduleRentChangeForLeaseAsync(
                _tenantId, _leaseId, Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid?>(), _staffId)
            .Returns(ServiceResult<bool>.Success(false));

        var result = await CreateSut().AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId);

        result.Data!.Status.Should().Be("ACCEPTED");
        result.Data.RentChangeApplied.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptAsync_WhenTheLeaseHasEndedSinceTheOffer_IsRejected_AndNothingChanges()
    {
        var offer = ArrangeOffer();
        ArrangeLease(status: "ENDED");

        var result = await CreateSut().AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId);

        result.ErrorCode.Should().Be("TRL-E-LSE-11007");
        offer.Status.Should().Be(RenewalOfferStatus.Offered);
        await _rentScheduleService.DidNotReceiveWithAnyArgs().ScheduleRentChangeForLeaseAsync(default, default, default, default, default, default);
        await _leaseService.DidNotReceiveWithAnyArgs().RenewAsync(default, default!, default);
    }

    [Fact]
    public async Task AcceptAsync_WhenTheRentChangeIsRejected_DoesNotRenewTheLease()
    {
        var offer = ArrangeOffer();
        _rentScheduleService.ScheduleRentChangeForLeaseAsync(
                _tenantId, _leaseId, Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid?>(), _staffId)
            .Returns(ServiceResult<bool>.BusinessRuleViolation("TRL-E-RENT-12005"));

        var result = await CreateSut().AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId);

        result.ErrorCode.Should().Be("TRL-E-RENT-12005");
        await _leaseService.DidNotReceiveWithAnyArgs().RenewAsync(default, default!, default);
        offer.Status.Should().Be(RenewalOfferStatus.Offered);
    }

    [Fact]
    public async Task AcceptAsync_OnAClosedOffer_IsRejected()
    {
        var offer = ArrangeOffer();
        offer.Decline(Today, Today, "Rent too high", null, ["Rent too high"], _staffId);

        (await CreateSut().AcceptAsync(offer.RenewalOfferId, new RenewalOfferAcceptRequest(Today), _staffId))
            .ErrorCode.Should().Be("TRL-E-LSE-11000");
    }

    // ---- DeclineAsync / ExpireAsync ----

    [Fact]
    public async Task DeclineAsync_UsesTheTenantsOwnReasons()
    {
        var offer = ArrangeOffer();
        var sut = CreateSut();

        (await sut.DeclineAsync(offer.RenewalOfferId, new RenewalOfferDeclineRequest(Today, "Buying a home", null), _staffId))
            .ErrorCode.Should().Be("TRL-E-LSE-11010");

        var result = await sut.DeclineAsync(offer.RenewalOfferId, new RenewalOfferDeclineRequest(Today, "Rent too high", null), _staffId);
        result.Data!.Status.Should().Be("DECLINED");
        result.Data.DeclineReason.Should().Be("Rent too high");
        await _leaseService.DidNotReceiveWithAnyArgs().RenewAsync(default, default!, default);
    }

    [Fact]
    public async Task ExpireAsync_BeforeTheRespondByDate_IsRejected()
    {
        var offer = ArrangeOffer(respondBy: Today);

        (await CreateSut().ExpireAsync(offer.RenewalOfferId, _staffId)).ErrorCode.Should().Be("TRL-E-LSE-11011");
    }

    [Fact]
    public async Task ListAsync_FlagsAnOpenOfferPastItsRespondByDate()
    {
        var offer = ArrangeOffer(respondBy: Today);
        typeof(RenewalOffer).GetProperty(nameof(RenewalOffer.RespondBy))!.SetValue(offer, Today.AddDays(-1));
        _repository.ListAsync(null, RenewalOfferStatus.Offered).Returns([offer]);

        var result = await CreateSut().ListAsync(null, "OFFERED");

        result.Data.Should().ContainSingle().Which.RespondByPassed.Should().BeTrue();
    }
}
