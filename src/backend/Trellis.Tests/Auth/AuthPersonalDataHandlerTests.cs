using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Application.Services;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Auth;

public sealed class AuthPersonalDataHandlerTests : IDisposable
{
    private const string SubjectUsername = "staff@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IAppUserRepository _userRepository = Substitute.For<IAppUserRepository>();
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();

    public AuthPersonalDataHandlerTests() => TenantContext.Current = new TenantContext(_tenantId);

    public void Dispose() => TenantContext.Current = null;

    private AuthPersonalDataHandler CreateSut() => new(_userRepository, _sessionService);

    [Fact]
    public async Task ExportAsync_ForAUserInAnotherTenant_ReturnsNull_EvenThoughTheUsernameResolved()
    {
        var otherTenantUser = AppUser.Create(Guid.NewGuid(), SubjectUsername, UserRole.TenantAdmin);
        _userRepository.GetByUsernameAsync(SubjectUsername).Returns(otherTenantUser);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectUsername);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EraseAsync_RedactsUsernamePasswordAndTotp_DeactivatesTheAccount_AndRevokesSessions()
    {
        var user = AppUser.Create(_tenantId, SubjectUsername, UserRole.DepartmentStaff);
        user.SetPasswordHash("hashed");
        user.EnrollMfa([1, 2, 3]);
        _userRepository.GetByUsernameAsync(SubjectUsername).Returns(user);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectUsername);

        user.Username.Should().NotBe(SubjectUsername).And.EndWith("@erased.invalid");
        user.PasswordHash.Should().BeNull();
        user.TotpSecretEncrypted.Should().BeNull();
        user.Status.Should().Be(UserStatus.Deactivated);
        summary.ModuleName.Should().Be("Auth");
        summary.RecordsAffected.Should().Be(1);
        await _userRepository.Received(1).SaveChangesAsync();
        await _sessionService.Received(1).RevokeAllExceptAsync(user.UserId, null);
    }

    [Fact]
    public async Task EraseAsync_WhenTheUsernameDoesNotResolve_ReturnsZero_AndRevokesNoSessions()
    {
        _userRepository.GetByUsernameAsync(SubjectUsername).Returns((AppUser?)null);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectUsername);

        summary.RecordsAffected.Should().Be(0);
        await _sessionService.DidNotReceiveWithAnyArgs().RevokeAllExceptAsync(default, default);
    }
}
