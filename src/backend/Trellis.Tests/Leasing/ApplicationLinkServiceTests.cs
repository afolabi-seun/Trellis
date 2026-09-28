using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Configuration;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Security;
using Trellis.Api.Modules.Notification.Application.Dtos;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class ApplicationLinkServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _listingId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly IApplicationLinkTokenRepository _tokenRepository = Substitute.For<IApplicationLinkTokenRepository>();
    private readonly IVacancyListingRepository _listingRepository = Substitute.For<IVacancyListingRepository>();
    private readonly IModuleEntitlementService _entitlements = Substitute.For<IModuleEntitlementService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();

    public void Dispose() => TenantContext.Current = null;

    private ApplicationLinkService CreateSut() => new(
        _tokenRepository, _listingRepository, _entitlements, _unitAssetService, _notificationService,
        Options.Create(new LeasingOptions { ApplicationLinkExpiryDays = 7 }),
        Options.Create(new AppOptions { BaseUrl = "https://app.example.com/" }));

    private VacancyListing ArrangeListing()
    {
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(true);
        _listingRepository.GetByIdForTenantAsync(_tenantId, _listingId).Returns(listing);
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(_unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "VACANT", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        return listing;
    }

    [Fact]
    public async Task IssueApplicationLinkAsync_StoresOnlyTheHash_AndEmailsTheRawTokenAsAProspectiveTenant()
    {
        ArrangeListing();
        ApplicationLinkToken? saved = null;
        await _tokenRepository.AddAsync(Arg.Do<ApplicationLinkToken>(t => saved = t));
        object? templateData = null;
        await _notificationService.SendAsync(
            Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Do<object>(d => templateData = d), Arg.Any<RelatedEntityRef>(), Arg.Any<NotificationRecipientType?>());
        var sut = CreateSut();

        var result = await sut.IssueApplicationLinkAsync(_tenantId, _listingId, "applicant@example.com");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        var rawToken = result.Data!;
        saved!.TokenHash.Should().Be(ApplicationLinkTokenGenerator.Hash(rawToken)).And.NotBe(rawToken);
        saved.ApplicantEmail.Should().Be("applicant@example.com");
        saved.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        templateData!.ToString().Should().Contain($"https://app.example.com/apply/{rawToken}");
        await _notificationService.Received(1).SendAsync(
            _tenantId, "APPLICATION_LINK", "applicant@example.com", Arg.Any<object>(), Arg.Any<RelatedEntityRef>(), NotificationRecipientType.ProspectiveTenant);
    }

    [Fact]
    public async Task IssueApplicationLinkAsync_WhenTheTenantIsNotEntitledToLeasing_ReturnsNotFound()
    {
        ArrangeListing();
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(false);
        var sut = CreateSut();

        var result = await sut.IssueApplicationLinkAsync(_tenantId, _listingId, "applicant@example.com");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _tokenRepository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task IssueApplicationLinkAsync_WhenTheListingIsWithdrawn_ReturnsNotFound()
    {
        var listing = ArrangeListing();
        listing.Withdraw();
        var sut = CreateSut();

        var result = await sut.IssueApplicationLinkAsync(_tenantId, _listingId, "applicant@example.com");

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    /// <summary>Same regression class as VacancyListingServiceTests's: the cross-module unit-label lookup (a Scoped() call) must run with the tenant already set.</summary>
    [Fact]
    public async Task IssueAndResolve_HaveTheTenantSetWhenTheCrossModuleUnitLookupRuns()
    {
        ArrangeListing();
        var token = ApplicationLinkToken.Create(_tenantId, _listingId, "a@example.com", ApplicationLinkTokenGenerator.Hash("raw"), DateTimeOffset.UtcNow.AddDays(1));
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("raw")).Returns(token);
        var seen = new List<Guid?>();
        _unitAssetService.GetByIdAsync(_unitId).Returns(_ =>
        {
            seen.Add(TenantContext.Current?.TenantId);
            return ServiceResult<UnitAssetResponse>.Success(
                new UnitAssetResponse(_unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "VACANT", null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow));
        });
        var sut = CreateSut();
        TenantContext.Current = null;

        await sut.IssueApplicationLinkAsync(_tenantId, _listingId, "a@example.com");
        TenantContext.Current = null;
        await sut.ResolveAsync("raw");

        seen.Should().Equal(_tenantId, _tenantId);
    }

    [Fact]
    public async Task ResolveAsync_ForAValidToken_ReturnsTheContext_WithTheServerTrustedEmail()
    {
        ArrangeListing();
        var token = ApplicationLinkToken.Create(_tenantId, _listingId, "verified@example.com", ApplicationLinkTokenGenerator.Hash("raw"), DateTimeOffset.UtcNow.AddDays(1));
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("raw")).Returns(token);
        var sut = CreateSut();

        var result = await sut.ResolveAsync("raw");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.ApplicantEmail.Should().Be("verified@example.com");
        result.Data.UnitLabel.Should().Be("Unit 4B");
        result.Data.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task ResolveAsync_IsRepeatable_BecauseAReloadMidFormFillMustStillWork()
    {
        ArrangeListing();
        var token = ApplicationLinkToken.Create(_tenantId, _listingId, "a@example.com", ApplicationLinkTokenGenerator.Hash("raw"), DateTimeOffset.UtcNow.AddDays(1));
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("raw")).Returns(token);
        var sut = CreateSut();

        (await sut.ResolveAsync("raw")).Outcome.Should().Be(ServiceResultOutcome.Success);
        (await sut.ResolveAsync("raw")).Outcome.Should().Be(ServiceResultOutcome.Success);
        token.Status.Should().Be(ApplicationLinkTokenStatus.Active);
    }

    [Fact]
    public async Task ResolveAsync_ForUnknownExpiredOrConsumedTokens_AllReturnTheSameNotFound()
    {
        ArrangeListing();
        var expired = ApplicationLinkToken.Create(_tenantId, _listingId, "a@example.com", ApplicationLinkTokenGenerator.Hash("expired"), DateTimeOffset.UtcNow.AddMinutes(-1));
        var consumed = ApplicationLinkToken.Create(_tenantId, _listingId, "a@example.com", ApplicationLinkTokenGenerator.Hash("consumed"), DateTimeOffset.UtcNow.AddDays(1));
        consumed.Consume(Guid.NewGuid());
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("expired")).Returns(expired);
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("consumed")).Returns(consumed);
        var sut = CreateSut();

        foreach (var raw in new[] { "unknown", "expired", "consumed" })
        {
            (await sut.ResolveAsync(raw)).Outcome.Should().Be(ServiceResultOutcome.NotFound, raw);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenEntitlementWasRevokedAfterIssuance_ReturnsNotFound()
    {
        ArrangeListing();
        var token = ApplicationLinkToken.Create(_tenantId, _listingId, "a@example.com", ApplicationLinkTokenGenerator.Hash("raw"), DateTimeOffset.UtcNow.AddDays(1));
        _tokenRepository.ResolveByTokenHashAsync(ApplicationLinkTokenGenerator.Hash("raw")).Returns(token);
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(false);
        var sut = CreateSut();

        (await sut.ResolveAsync("raw")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }
}
