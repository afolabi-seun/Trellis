using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Cors;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Domain.Entities;
using Xunit;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Tests.Tenant;

public sealed class TenantDomainServiceTests : IDisposable
{
    private readonly Guid _tenantId;
    private readonly Guid _actor = Guid.NewGuid();
    private readonly TenantEntity _tenant;
    private readonly ITenantDomainRepository _repository = Substitute.For<ITenantDomainRepository>();
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly IDomainOwnershipVerifier _verifier = Substitute.For<IDomainOwnershipVerifier>();
    private readonly ICustomDomainOriginProvider _origins = Substitute.For<ICustomDomainOriginProvider>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public TenantDomainServiceTests()
    {
        _tenant = TenantEntity.Create("Acme PM", "US", "acme");
        _tenantId = _tenant.TenantId;
        TenantContext.Current = new TenantContext(_tenantId);
        _tenantRepository.GetByIdAsync(_tenantId).Returns(_tenant);
    }

    public void Dispose() => TenantContext.Current = null;

    private TenantDomainService CreateSut() => new(
        _repository, _tenantRepository, _verifier, _origins, _auditService,
        Options.Create(new AppOptions { BaseUrl = "https://app.trellis-platform.com/", PlatformHosts = "localhost" }));

    private static void MakeEnterprise(TenantEntity tenant) =>
        typeof(TenantEntity).GetProperty(nameof(TenantEntity.SubscriptionTier))!.SetValue(tenant, SubscriptionTier.Enterprise);

    // ---- AddAsync ----

    [Fact]
    public async Task AddAsync_OnAStarterTenant_IsRefused_WithTheEnterpriseCode()
    {
        var sut = CreateSut();

        var result = await sut.AddAsync(new TenantDomainCreateRequest("listings.acme.com"), _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        result.ErrorCode.Should().Be("TRL-E-TEN-2002");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task AddAsync_OnAnEnterpriseTenant_RegistersItPending_WithTheDnsInstructions()
    {
        MakeEnterprise(_tenant);
        TenantDomain? saved = null;
        await _repository.AddAsync(Arg.Do<TenantDomain>(d => saved = d));
        var sut = CreateSut();

        var result = await sut.AddAsync(new TenantDomainCreateRequest("Listings.Acme.com"), _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        saved!.Domain.Should().Be("listings.acme.com");
        saved.Status.Should().Be(TenantDomainStatus.PendingVerification);
        result.Data!.Status.Should().Be("PENDING_VERIFICATION");
        result.Data.VerificationRecordName.Should().Be("_trellis-verify.listings.acme.com");
        result.Data.VerificationRecordValue.Should().Be($"trellis-verify={saved.VerificationToken}");
        await _auditService.Received(1).RecordAsync(_tenantId, _actor, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainAdded, Arg.Any<string>());
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("192.168.1.1")]
    [InlineData("https://acme.com")]
    [InlineData("not a domain")]
    public async Task AddAsync_RejectsInvalidOrPlatformDomains(string domain)
    {
        MakeEnterprise(_tenant);
        var sut = CreateSut();

        var result = await sut.AddAsync(new TenantDomainCreateRequest(domain), _actor);

        result.ErrorCode.Should().Be("TRL-E-TEN-2004");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task AddAsync_RejectsTheFrontendsOwnHostAndItsSubdomains()
    {
        MakeEnterprise(_tenant);
        var sut = CreateSut();

        (await sut.AddAsync(new TenantDomainCreateRequest("app.trellis-platform.com"), _actor)).ErrorCode.Should().Be("TRL-E-TEN-2004");
        (await sut.AddAsync(new TenantDomainCreateRequest("evil.app.trellis-platform.com"), _actor)).ErrorCode.Should().Be("TRL-E-TEN-2004");
    }

    // ---- VerifyAsync ----

    private TenantDomain Pending(string domain = "listings.acme.com")
    {
        var d = TenantDomain.Create(_tenantId, domain, "tok123");
        _repository.GetByIdAsync(d.TenantDomainId).Returns(d);
        return d;
    }

    [Fact]
    public async Task VerifyAsync_WhenTheTxtRecordIsPublished_MarksItVerified_RefreshesCors_AndAudits()
    {
        var domain = Pending();
        _verifier.HasTxtRecordAsync("_trellis-verify.listings.acme.com", "trellis-verify=tok123").Returns(true);
        var sut = CreateSut();

        var result = await sut.VerifyAsync(domain.TenantDomainId, _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("VERIFIED");
        result.Data.VerificationRecordValue.Should().BeNull("the token has done its job once verified");
        domain.Status.Should().Be(TenantDomainStatus.Verified);
        _origins.Received(1).Invalidate();
        await _auditService.Received(1).RecordAsync(_tenantId, _actor, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainVerified, Arg.Any<string>());
    }

    [Fact]
    public async Task VerifyAsync_WhenTheRecordIsMissing_StaysPending_AndRefreshesNothing()
    {
        var domain = Pending();
        _verifier.HasTxtRecordAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var sut = CreateSut();

        var result = await sut.VerifyAsync(domain.TenantDomainId, _actor);

        result.ErrorCode.Should().Be("TRL-E-TEN-2005");
        domain.Status.Should().Be(TenantDomainStatus.PendingVerification);
        _origins.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task VerifyAsync_WhenAnotherTenantVerifiedTheSameDomainFirst_IsRefused()
    {
        var domain = Pending();
        _verifier.HasTxtRecordAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateException("duplicate")));
        var sut = CreateSut();

        var result = await sut.VerifyAsync(domain.TenantDomainId, _actor);

        result.ErrorCode.Should().Be("TRL-E-TEN-2005");
        _origins.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task VerifyAsync_OnAnAlreadyVerifiedDomain_IsAnIdempotentSuccess_WithoutAnotherDnsLookup()
    {
        var domain = Pending();
        domain.MarkVerified();
        var sut = CreateSut();

        var result = await sut.VerifyAsync(domain.TenantDomainId, _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        await _verifier.DidNotReceiveWithAnyArgs().HasTxtRecordAsync(default!, default!);
    }

    [Fact]
    public async Task VerifyAsync_ForAnUnknownDomain_IsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((TenantDomain?)null);
        var sut = CreateSut();

        (await sut.VerifyAsync(Guid.NewGuid(), _actor)).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    // ---- RemoveAsync ----

    [Fact]
    public async Task RemoveAsync_DeletesTheDomain_RefreshesCors_AndAudits()
    {
        var domain = Pending();
        var sut = CreateSut();

        var result = await sut.RemoveAsync(domain.TenantDomainId, _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        _repository.Received(1).Remove(domain);
        _origins.Received(1).Invalidate();
        await _auditService.Received(1).RecordAsync(_tenantId, _actor, AuditActorType.HumanUser, AuditTypes.Tenant.TenantDomainRemoved, Arg.Any<string>());
    }

    // ---- Anonymous resolution ----

    [Fact]
    public async Task ResolveHostAsync_ForAVerifiedDomainOfALiveTenant_ReturnsTheTenant()
    {
        var domain = Pending();
        domain.MarkVerified();
        _repository.GetVerifiedByHostAsync("listings.acme.com").Returns(domain);
        TenantContext.Current = null;
        var sut = CreateSut();

        var result = await sut.ResolveHostAsync("Listings.Acme.com");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task ResolveHostAsync_ForAnUnverifiedOrUnknownHost_IsNotFound()
    {
        _repository.GetVerifiedByHostAsync(Arg.Any<string>()).Returns((TenantDomain?)null);
        var sut = CreateSut();

        (await sut.ResolveHostAsync("listings.acme.com")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
        (await sut.ResolveHostAsync("not a host")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveHostAsync_ForASuspendedTenant_IsNotFound()
    {
        var domain = Pending();
        domain.MarkVerified();
        _repository.GetVerifiedByHostAsync("listings.acme.com").Returns(domain);
        var suspended = TenantEntity.Create("Acme PM", "US", "acme");
        suspended.Activate();
        suspended.Suspend();
        _tenantRepository.GetByIdAsync(_tenantId).Returns(suspended);
        var sut = CreateSut();

        (await sut.ResolveHostAsync("listings.acme.com")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveHostAsync_QueriesTheCrossTenantTableUnderSystemContext()
    {
        var seenSystem = false;
        _repository.GetVerifiedByHostAsync(Arg.Any<string>()).Returns(_ =>
        {
            seenSystem = SystemContext.IsSystem;
            return (TenantDomain?)null;
        });
        TenantContext.Current = null;
        var sut = CreateSut();

        await sut.ResolveHostAsync("listings.acme.com");

        seenSystem.Should().BeTrue();
        SystemContext.IsSystem.Should().BeFalse("the elevation is scoped to the lookup only");
    }

    // ---- Link base URL ----

    [Fact]
    public async Task GetLinkBaseUrlAsync_UsesTheTenantsVerifiedDomain_OverHttps()
    {
        var domain = Pending();
        domain.MarkVerified();
        _repository.GetPrimaryVerifiedForTenantAsync(_tenantId).Returns(domain);
        var sut = CreateSut();

        (await sut.GetLinkBaseUrlAsync(_tenantId)).Should().Be("https://listings.acme.com");
    }

    [Fact]
    public async Task GetLinkBaseUrlAsync_WithNoVerifiedDomain_FallsBackToThePlatformFrontend_WithoutATrailingSlash()
    {
        _repository.GetPrimaryVerifiedForTenantAsync(_tenantId).Returns((TenantDomain?)null);
        var sut = CreateSut();

        (await sut.GetLinkBaseUrlAsync(_tenantId)).Should().Be("https://app.trellis-platform.com");
    }
}
