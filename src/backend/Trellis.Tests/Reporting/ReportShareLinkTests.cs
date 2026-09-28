using FluentAssertions;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Reporting;

public sealed class ReportShareLinkTests
{
    private static ReportShareLink CreateActive(DateTimeOffset? expiresAt = null) =>
        ReportShareLink.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", expiresAt ?? DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());

    [Fact]
    public void Create_SetsStatusToActive()
    {
        var link = CreateActive();

        link.Status.Should().Be(ReportShareLinkStatus.Active);
        link.LastAccessedAt.Should().BeNull();
    }

    [Fact]
    public void RecordAccess_SetsLastAccessedAt()
    {
        var link = CreateActive();

        link.RecordAccess();

        link.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_WhenActive_TransitionsToRevoked()
    {
        var link = CreateActive();

        link.Revoke();

        link.Status.Should().Be(ReportShareLinkStatus.Revoked);
    }

    [Fact]
    public void Revoke_CalledTwice_ThrowsOnTheSecondCall()
    {
        var link = CreateActive();
        link.Revoke();

        var act = link.Revoke;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void IsUsable_WhenActiveAndNotExpired_ReturnsTrue() =>
        CreateActive(DateTimeOffset.UtcNow.AddDays(1)).IsUsable().Should().BeTrue();

    [Fact]
    public void IsUsable_WhenExpired_ReturnsFalse() =>
        CreateActive(DateTimeOffset.UtcNow.AddDays(-1)).IsUsable().Should().BeFalse();

    [Fact]
    public void IsUsable_WhenRevoked_ReturnsFalse()
    {
        var link = CreateActive();
        link.Revoke();

        link.IsUsable().Should().BeFalse();
    }
}
