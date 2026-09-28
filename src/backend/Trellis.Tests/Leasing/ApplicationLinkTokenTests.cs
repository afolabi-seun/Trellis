using FluentAssertions;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class ApplicationLinkTokenTests
{
    private static ApplicationLinkToken CreateActive(DateTimeOffset? expiresAt = null) =>
        ApplicationLinkToken.Create(Guid.NewGuid(), Guid.NewGuid(), "a@example.com", "hash", expiresAt ?? DateTimeOffset.UtcNow.AddDays(7));

    [Fact]
    public void IsUsable_WhenActiveAndNotExpired_ReturnsTrue() =>
        CreateActive().IsUsable().Should().BeTrue();

    [Fact]
    public void IsUsable_WhenExpired_ReturnsFalse() =>
        CreateActive(DateTimeOffset.UtcNow.AddMinutes(-1)).IsUsable().Should().BeFalse();

    [Fact]
    public void Consume_MarksConsumed_AndRecordsTheApplication()
    {
        var token = CreateActive();
        var applicationId = Guid.NewGuid();

        token.Consume(applicationId);

        token.Status.Should().Be(ApplicationLinkTokenStatus.Consumed);
        token.ConsumedApplicationId.Should().Be(applicationId);
        token.ConsumedAt.Should().NotBeNull();
        token.IsUsable().Should().BeFalse();
    }

    [Fact]
    public void Consume_Twice_Throws_SoALinkCantSubmitTwoApplications()
    {
        var token = CreateActive();
        token.Consume(Guid.NewGuid());

        var act = () => token.Consume(Guid.NewGuid());

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Consume_WhenExpired_Throws()
    {
        var token = CreateActive(DateTimeOffset.UtcNow.AddMinutes(-1));

        var act = () => token.Consume(Guid.NewGuid());

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
