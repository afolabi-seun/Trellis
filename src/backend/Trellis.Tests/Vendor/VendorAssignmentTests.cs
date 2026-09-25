using FluentAssertions;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Vendor;

public sealed class VendorAssignmentTests
{
    [Fact]
    public void Create_StartsAssigned()
    {
        var assignment = VendorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());

        assignment.Status.Should().Be(VendorAssignmentStatus.Assigned);
        assignment.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void MarkInProgress_FromAssigned_Succeeds()
    {
        var assignment = VendorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());

        assignment.MarkInProgress();

        assignment.Status.Should().Be(VendorAssignmentStatus.InProgress);
    }

    [Fact]
    public void MarkInProgress_WhenAlreadyInProgress_Throws()
    {
        var assignment = VendorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());
        assignment.MarkInProgress();

        var act = assignment.MarkInProgress;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_FromAssignedOrInProgress_ClosesImmediately(bool startFirst)
    {
        // BDR-VEN-005's approval/sign-off gate is deferred — Complete() goes straight to Closed
        // regardless of whether the vendor started the work first, matching
        // operational_flows.md's own no-approval-configured branch.
        var assignment = VendorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());
        if (startFirst)
        {
            assignment.MarkInProgress();
        }

        assignment.Complete();

        assignment.Status.Should().Be(VendorAssignmentStatus.Closed);
        assignment.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WhenAlreadyClosed_Throws()
    {
        var assignment = VendorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());
        assignment.Complete();

        var act = assignment.Complete;

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
