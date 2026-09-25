using FluentAssertions;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Vendor;

public sealed class TenantVendorRelationshipTests
{
    [Fact]
    public void Create_StartsActive()
    {
        var relationship = TenantVendorRelationship.Create(Guid.NewGuid(), Guid.NewGuid());

        relationship.Status.Should().Be(TenantVendorRelationshipStatus.Active);
        relationship.TerminatedAt.Should().BeNull();
    }

    [Fact]
    public void Terminate_FromActive_Succeeds()
    {
        var relationship = TenantVendorRelationship.Create(Guid.NewGuid(), Guid.NewGuid());
        var terminatedBy = Guid.NewGuid();

        relationship.Terminate(terminatedBy);

        relationship.Status.Should().Be(TenantVendorRelationshipStatus.Terminated);
        relationship.TerminatedAt.Should().NotBeNull();
        relationship.TerminatedByUserId.Should().Be(terminatedBy);
    }

    [Fact]
    public void Terminate_WhenAlreadyTerminated_Throws()
    {
        // BDR-VEN-006's own text: terminating never touches existing VendorAssignment rows — this
        // test only proves the relationship's own guard, not that assignments stay visible
        // (VendorIsolationTests.cs's job).
        var relationship = TenantVendorRelationship.Create(Guid.NewGuid(), Guid.NewGuid());
        relationship.Terminate(Guid.NewGuid());

        var act = () => relationship.Terminate(Guid.NewGuid());

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
