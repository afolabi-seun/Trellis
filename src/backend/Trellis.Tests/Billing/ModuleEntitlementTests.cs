using FluentAssertions;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class ModuleEntitlementTests
{
    [Fact]
    public void Create_StartsActive()
    {
        var entitlement = ModuleEntitlement.Create(Guid.NewGuid(), "AI", 0);

        entitlement.Status.Should().Be(EntitlementStatus.Active);
    }

    [Fact]
    public void Disable_TwiceInARow_ThrowsOnTheSecondCall()
    {
        var entitlement = ModuleEntitlement.Create(Guid.NewGuid(), "AI", 0);
        entitlement.Disable();

        var act = entitlement.Disable;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Enable_WhenAlreadyActive_ThrowsInvalidStatusTransitionException()
    {
        var entitlement = ModuleEntitlement.Create(Guid.NewGuid(), "AI", 0);

        var act = entitlement.Enable;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Enable_AfterDisable_ClearsDisabledAt()
    {
        var entitlement = ModuleEntitlement.Create(Guid.NewGuid(), "AI", 0);
        entitlement.Disable();

        entitlement.Enable();

        entitlement.Status.Should().Be(EntitlementStatus.Active);
        entitlement.DisabledAt.Should().BeNull();
    }
}
