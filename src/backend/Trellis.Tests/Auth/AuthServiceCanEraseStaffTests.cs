using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Application.Services;
using Trellis.Api.Modules.Auth.Configuration;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Auth;

/// <summary>
/// DataSubjectService's own pre-flight guard against erasing the caller or the tenant's last
/// active TENANT_ADMIN. Unlike the real HTTP path (TENANT_ADMIN-only, so the actor is always
/// counted among the tenant's admins), these unit tests exercise the method's own logic directly
/// — including the "last admin, distinct from the caller" branch, which today's one calling path
/// can never actually reach (the actor being a TENANT_ADMIN means erasing the sole remaining
/// admin is always the self case first) but which the method itself must still get right for any
/// future caller.
/// </summary>
public sealed class AuthServiceCanEraseStaffTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IAppUserRepository _userRepository = Substitute.For<IAppUserRepository>();

    public AuthServiceCanEraseStaffTests() => TenantContext.Current = new TenantContext(_tenantId);

    public void Dispose() => TenantContext.Current = null;

    private AuthService CreateSut() => new(
        _userRepository,
        Substitute.For<ISessionRepository>(),
        Substitute.For<IPasswordResetTokenRepository>(),
        Substitute.For<IMfaRecoveryCodeRepository>(),
        Substitute.For<IPasswordHasher>(),
        Substitute.For<IJwtTokenService>(),
        Substitute.For<ITotpService>(),
        Substitute.For<INotificationService>(),
        Substitute.For<IAuditService>(),
        Substitute.For<ISessionService>(),
        Options.Create(new AuthOptions()),
        Options.Create(new AppOptions()));

    [Fact]
    public async Task CanEraseStaffAsync_ForTheCallersOwnLogin_ReturnsFalse()
    {
        var actor = AppUser.Create(_tenantId, "actor@example.com", UserRole.TenantAdmin);
        _userRepository.GetByUsernameAsync("actor@example.com").Returns(actor);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("actor@example.com", actor.UserId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanEraseStaffAsync_ForADepartmentStaffTarget_ReturnsTrue_RegardlessOfAdminCount()
    {
        var target = AppUser.Create(_tenantId, "staff@example.com", UserRole.DepartmentStaff);
        _userRepository.GetByUsernameAsync("staff@example.com").Returns(target);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("staff@example.com", Guid.NewGuid());

        result.Should().BeTrue();
        await _userRepository.DidNotReceiveWithAnyArgs().CountActiveTenantAdminsAsync(default);
    }

    [Fact]
    public async Task CanEraseStaffAsync_ForATenantAdminTarget_WithMoreThanOneActiveAdmin_ReturnsTrue()
    {
        var target = AppUser.Create(_tenantId, "admin2@example.com", UserRole.TenantAdmin);
        _userRepository.GetByUsernameAsync("admin2@example.com").Returns(target);
        _userRepository.CountActiveTenantAdminsAsync(_tenantId).Returns(2);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("admin2@example.com", Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEraseStaffAsync_ForTheTenantsLastActiveAdmin_DistinctFromTheCaller_ReturnsFalse()
    {
        var target = AppUser.Create(_tenantId, "sole-admin@example.com", UserRole.TenantAdmin);
        _userRepository.GetByUsernameAsync("sole-admin@example.com").Returns(target);
        _userRepository.CountActiveTenantAdminsAsync(_tenantId).Returns(1);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("sole-admin@example.com", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanEraseStaffAsync_WhenTheUsernameDoesNotResolveInThisTenant_ReturnsTrue_NothingToGuard()
    {
        _userRepository.GetByUsernameAsync("nobody@example.com").Returns((AppUser?)null);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("nobody@example.com", Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEraseStaffAsync_WhenTheUsernameResolvesInADifferentTenant_ReturnsTrue()
    {
        var target = AppUser.Create(Guid.NewGuid(), "cross-tenant@example.com", UserRole.TenantAdmin);
        _userRepository.GetByUsernameAsync("cross-tenant@example.com").Returns(target);
        var sut = CreateSut();

        var result = await sut.CanEraseStaffAsync("cross-tenant@example.com", Guid.NewGuid());

        result.Should().BeTrue();
    }
}
