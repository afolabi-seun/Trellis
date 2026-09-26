using FluentAssertions;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.Inspection.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Inspection;

public sealed class InspectionRecordTests
{
    [Fact]
    public void Create_StartsScheduled()
    {
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        inspection.Status.Should().Be(InspectionStatus.Scheduled);
        inspection.StartedAt.Should().BeNull();
        inspection.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void MarkInProgress_FromScheduled_Succeeds()
    {
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        inspection.MarkInProgress();

        inspection.Status.Should().Be(InspectionStatus.InProgress);
        inspection.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkInProgress_WhenAlreadyInProgress_Throws()
    {
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        inspection.MarkInProgress();

        var act = inspection.MarkInProgress;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Complete_FromInProgress_ClosesImmediately()
    {
        // BDR-INSP-005's comparison view is Could-priority with no approval-gate schema — Complete()
        // goes straight to Closed, skipping a rest at Completed, matching VendorAssignment.Complete()'s
        // own precedent for BDR-VEN-005.
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        inspection.MarkInProgress();

        inspection.Complete(12.34m, 56.78m);

        inspection.Status.Should().Be(InspectionStatus.Closed);
        inspection.CompletedAt.Should().NotBeNull();
        inspection.ClosedAt.Should().NotBeNull();
        inspection.Latitude.Should().Be(12.34m);
        inspection.Longitude.Should().Be(56.78m);
    }

    [Fact]
    public void Complete_WhenStillScheduled_Throws()
    {
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var act = () => inspection.Complete(null, null);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Complete_WhenAlreadyClosed_Throws()
    {
        var inspection = InspectionRecord.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        inspection.MarkInProgress();
        inspection.Complete(null, null);

        var act = () => inspection.Complete(null, null);

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
