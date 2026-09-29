using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Application.Services;
using Trellis.Api.Modules.Auth.Configuration;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Auth;

/// <summary>
/// IAuthService.ReauthenticateAsync — the password re-entry check a narrow, separately-audited
/// action (P6's screening-report view) gates on, extracted from RegenerateRecoveryCodesAsync's
/// own inline check. Mocked repositories, no database — testing_strategy.md's Unit layer.
/// </summary>
public sealed class AuthServiceReauthenticateTests
{
    private readonly IAppUserRepository _userRepository = Substitute.For<IAppUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private AuthService CreateSut() => new(
        _userRepository,
        Substitute.For<ISessionRepository>(),
        Substitute.For<IPasswordResetTokenRepository>(),
        Substitute.For<IMfaRecoveryCodeRepository>(),
        _passwordHasher,
        Substitute.For<IJwtTokenService>(),
        Substitute.For<ITotpService>(),
        Substitute.For<INotificationService>(),
        _auditService,
        Substitute.For<ISessionService>(),
        Options.Create(new AuthOptions()),
        Options.Create(new AppOptions()));

    [Fact]
    public async Task ReauthenticateAsync_WithTheCorrectPassword_ReturnsSuccess_AndWritesNoAuditRecord()
    {
        var tenantId = Guid.NewGuid();
        var user = AppUser.Create(tenantId, "admin@example.com", UserRole.TenantAdmin);
        user.SetPasswordHash("hashed");
        _userRepository.GetByIdAsync(user.UserId).Returns(user);
        _passwordHasher.Verify("correct-password", "hashed").Returns(true);
        var sut = CreateSut();

        var result = await sut.ReauthenticateAsync(user.UserId, "correct-password");

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data.Should().BeTrue();
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task ReauthenticateAsync_WithTheWrongPassword_ReturnsForbidden_AndAuditsTheFailedAttempt()
    {
        var tenantId = Guid.NewGuid();
        var user = AppUser.Create(tenantId, "admin@example.com", UserRole.TenantAdmin);
        user.SetPasswordHash("hashed");
        _userRepository.GetByIdAsync(user.UserId).Returns(user);
        _passwordHasher.Verify("wrong-password", "hashed").Returns(false);
        var sut = CreateSut();

        var result = await sut.ReauthenticateAsync(user.UserId, "wrong-password");

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        result.ErrorCode.Should().Be("TRL-E-AUT-1004");
        await _auditService.Received(1).RecordAsync(tenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.ReauthenticationFailed);
    }

    [Fact]
    public async Task ReauthenticateAsync_ForAUserWithNoPasswordSet_ReturnsForbidden_NotAnException()
    {
        var user = AppUser.CreateVendorUser(Guid.NewGuid(), "vendor@example.com");
        _userRepository.GetByIdAsync(user.UserId).Returns(user);
        var sut = CreateSut();

        var result = await sut.ReauthenticateAsync(user.UserId, "anything");

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
    }

    [Fact]
    public async Task ReauthenticateAsync_ForAnUnknownUserId_ReturnsForbidden_AndNeverAudits()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((AppUser?)null);
        var sut = CreateSut();

        var result = await sut.ReauthenticateAsync(Guid.NewGuid(), "anything");

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default, default, default!, default);
    }
}
