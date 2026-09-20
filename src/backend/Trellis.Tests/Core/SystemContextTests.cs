using System.Reflection;
using FluentAssertions;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Core;

/// <summary>
/// testing_strategy.md: "app.is_system bypass is reachable only from Super Admin / background-job
/// code paths — a test that attempts to set it from a simulated request context should fail to
/// compile or fail at runtime, not silently succeed." <see cref="SystemContext.IsSystem"/> has no
/// public setter (`Core/Middleware/TenantContext.cs`) — <c>SystemContext.IsSystem = true;</c>
/// from anywhere outside that file is a compile error, which is the literal "fail to compile"
/// case the spec asks for and can't be exercised as a runnable test. This asserts that guarantee
/// mechanically instead, so a future refactor that reintroduces a public setter fails this test
/// rather than silently reopening the gap.
/// </summary>
public sealed class SystemContextTests
{
    [Fact]
    public void IsSystem_HasNoPubliclyAccessibleSetter()
    {
        var property = typeof(SystemContext).GetProperty(nameof(SystemContext.IsSystem));

        property.Should().NotBeNull();
        var setter = property!.GetSetMethod(nonPublic: false);
        setter.Should().BeNull("the only sanctioned way to enable the RLS bypass is SystemContext.Enter()");
    }

    [Fact]
    public void Enter_SetsIsSystemTrueForTheScopeAndRestoresPriorValueOnDispose()
    {
        SystemContext.IsSystem.Should().BeFalse();

        using (SystemContext.Enter())
        {
            SystemContext.IsSystem.Should().BeTrue();
        }

        SystemContext.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void Enter_WhenNested_RestoresOuterScopesValueNotFalse()
    {
        using (SystemContext.Enter())
        {
            using (SystemContext.Enter())
            {
                SystemContext.IsSystem.Should().BeTrue();
            }

            // Still true — the inner scope's Dispose must restore the outer scope's value,
            // not unconditionally flip the flag off underneath it.
            SystemContext.IsSystem.Should().BeTrue();
        }

        SystemContext.IsSystem.Should().BeFalse();
    }
}
