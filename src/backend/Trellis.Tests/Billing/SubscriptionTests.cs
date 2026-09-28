using FluentAssertions;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class SubscriptionTests
{
    [Fact]
    public void Create_StartsInTrialing()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 15);

        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotent_AndDoesNotThrow()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 15);
        subscription.Activate();

        var act = subscription.Activate;

        act.Should().NotThrow();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void MarkPastDue_FromTrialing_Succeeds_SinceAFirstEverChargeCanFailBeforeEverBeingActive()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 15);

        var act = subscription.MarkPastDue;

        act.Should().NotThrow();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public void MarkPastDue_WhenAlreadySuspended_ThrowsInvalidStatusTransitionException()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 15);
        subscription.MarkPastDue();
        subscription.Suspend();

        var act = subscription.MarkPastDue;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Suspend_OnlyAllowedFromPastDue()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 15);
        subscription.Activate();

        var act = subscription.Suspend;

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
