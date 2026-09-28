using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// Unit-layer complement to <c>Isolation/AppUserConcurrencyTests.cs</c> — that test proves real
/// Postgres actually throws on a stale <c>xmin</c>; this one proves
/// <see cref="AuthService.ResetPasswordAsync"/> catches it and returns
/// <c>ConcurrencyConflict()</c> instead of letting it fall through to
/// <c>ExceptionHandlingMiddleware</c> as an unhandled 500 (coding_standards.md, BDR-NFR-006).
/// Mocked repositories, no database — testing_strategy.md's Unit layer.
/// </summary>
public sealed class AuthServiceConcurrencyTests
{
    [Fact]
    [Trait("BDR", "BDR-NFR-006")]
    public async Task ResetPasswordAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflictNotUnhandled()
    {
        var userId = Guid.NewGuid();
        var resetToken = PasswordResetToken.Create(userId, "irrelevant-hash", TimeSpan.FromMinutes(30), requestedIp: null);
        var user = AppUser.Create(Guid.NewGuid(), "someone@tenant.test", UserRole.TenantAdmin);

        var userRepository = Substitute.For<IAppUserRepository>();
        userRepository.GetByIdAsync(userId).Returns(user);
        userRepository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var resetTokenRepository = Substitute.For<IPasswordResetTokenRepository>();
        resetTokenRepository.GetByHashAsync(Arg.Any<string>()).Returns(resetToken);

        var sut = new AuthService(
            userRepository,
            Substitute.For<ISessionRepository>(),
            resetTokenRepository,
            Substitute.For<IMfaRecoveryCodeRepository>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IJwtTokenService>(),
            Substitute.For<ITotpService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<IAuditService>(),
            Substitute.For<ISessionService>(),
            Options.Create(new AuthOptions()),
            Options.Create(new AppOptions()));

        var result = await sut.ResetPasswordAsync("raw-token", "N3wP@ssword!");

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }
}
