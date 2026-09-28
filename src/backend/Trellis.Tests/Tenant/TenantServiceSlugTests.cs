using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Domain.Entities;
using Xunit;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Tests.Tenant;

/// <summary>
/// Slug resolution and renaming. ProvisionTenantAsync's slug handling needs a real transaction, so it
/// is covered against real Postgres in TenantSlugProvisioningTests; nothing exercised here touches the
/// DbContext, which is why it can be passed as null.
/// </summary>
public sealed class TenantServiceSlugTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private TenantService CreateSut() => new(
        _repository, null!, Substitute.For<IAuthService>(), Substitute.For<ISessionService>(), _auditService);

    private static TenantEntity Tenant(string slug = "acme", TenantStatus? status = null)
    {
        var tenant = TenantEntity.Create("Acme PM", "US", slug);
        switch (status)
        {
            case TenantStatus.Active:
                tenant.Activate();
                break;
            case TenantStatus.Suspended:
                tenant.Activate();
                tenant.Suspend();
                break;
            case TenantStatus.Offboarding:
                tenant.Activate();
                tenant.BeginOffboarding();
                break;
        }

        return tenant;
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TenantStatus.Active)]
    public async Task ResolveSlugAsync_ResolvesOnboardingAndActiveTenants(TenantStatus? status)
    {
        var tenant = Tenant(status: status);
        _repository.GetBySlugAsync("acme").Returns(tenant);
        var sut = CreateSut();

        var result = await sut.ResolveSlugAsync("acme");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.TenantId.Should().Be(tenant.TenantId);
        result.Data.Name.Should().Be("Acme PM");
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Offboarding)]
    public async Task ResolveSlugAsync_ASuspendedOrOffboardingTenantsPublicPagesVanish(TenantStatus status)
    {
        _repository.GetBySlugAsync("acme").Returns(Tenant(status: status));
        var sut = CreateSut();

        (await sut.ResolveSlugAsync("acme")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveSlugAsync_ForAnUnknownSlug_IsNotFound()
    {
        _repository.GetBySlugAsync(Arg.Any<string>()).Returns((TenantEntity?)null);
        var sut = CreateSut();

        (await sut.ResolveSlugAsync("ghost")).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Theory]
    [InlineData("Not A Slug")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task ResolveSlugAsync_ForAnInvalidOrReservedSlug_IsNotFound_WithoutEvenQueryingTheDatabase(string slug)
    {
        var sut = CreateSut();

        (await sut.ResolveSlugAsync(slug)).Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().GetBySlugAsync(default!);
    }

    [Fact]
    public async Task ChangeSlugAsync_RenamesTheTenant_AndAuditsTheOldAndNewSlug()
    {
        var tenant = Tenant();
        _repository.GetByIdAsync(tenant.TenantId).Returns(tenant);
        _repository.SlugExistsAsync("acme-new").Returns(false);
        var actor = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.ChangeSlugAsync(tenant.TenantId, "acme-new", actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Slug.Should().Be("acme-new");
        await _repository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(
            tenant.TenantId, actor, AuditActorType.HumanUser, AuditTypes.Tenant.TenantSlugChanged,
            Arg.Is<string>(d => d.Contains("\"from\":\"acme\"") && d.Contains("\"to\":\"acme-new\"")));
    }

    [Fact]
    public async Task ChangeSlugAsync_ToASlugAnotherTenantHolds_IsAConflict_AndNothingIsSaved()
    {
        var tenant = Tenant();
        _repository.GetByIdAsync(tenant.TenantId).Returns(tenant);
        _repository.SlugExistsAsync("taken").Returns(true);
        var sut = CreateSut();

        var result = await sut.ChangeSlugAsync(tenant.TenantId, "taken", Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Conflict);
        result.ErrorCode.Should().Be("TRL-E-TEN-2003");
        tenant.Slug.Should().Be("acme");
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ChangeSlugAsync_ToTheSameSlug_IsNotAConflictWithItself()
    {
        var tenant = Tenant();
        _repository.GetByIdAsync(tenant.TenantId).Returns(tenant);
        _repository.SlugExistsAsync("acme").Returns(true);
        var sut = CreateSut();

        (await sut.ChangeSlugAsync(tenant.TenantId, "acme", Guid.NewGuid())).Outcome.Should().Be(ServiceResultOutcome.Success);
    }

    [Fact]
    public async Task ChangeSlugAsync_ToAReservedWord_IsRejected()
    {
        var tenant = Tenant();
        _repository.GetByIdAsync(tenant.TenantId).Returns(tenant);
        var sut = CreateSut();

        (await sut.ChangeSlugAsync(tenant.TenantId, "admin", Guid.NewGuid())).Outcome.Should().Be(ServiceResultOutcome.Conflict);
    }

    [Fact]
    public async Task ChangeSlugAsync_ForAnUnknownTenant_IsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((TenantEntity?)null);
        var sut = CreateSut();

        (await sut.ChangeSlugAsync(Guid.NewGuid(), "whatever", Guid.NewGuid())).Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public void Tenant_Create_RefusesAnInvalidSlug()
    {
        var act = () => TenantEntity.Create("Acme PM", "US", "Not A Slug");

        act.Should().Throw<ArgumentException>();
    }
}
