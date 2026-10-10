using FluentAssertions;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;
using Xunit;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Tests.Leasing;

public sealed class ApplicationTests
{
    private static LeasingApplication CreateSubmitted() =>
        LeasingApplication.Create(
            Guid.NewGuid(), Guid.NewGuid(), "applicant@example.com", "Ada Applicant", "+27821234567",
            new DateOnly(1990, 1, 1), "1 Main Rd", "Acme", 5000m, true);

    [Fact]
    public void Create_StartsSubmitted_WithNoEnvelope()
    {
        var application = CreateSubmitted();

        application.Status.Should().Be(ApplicationStatus.Submitted);
        application.EnvelopeId.Should().BeNull();
        application.SentForSignatureBy.Should().BeNull();
    }

    [Fact]
    public void BeginScreening_FromSubmitted_MovesToScreening()
    {
        var application = CreateSubmitted();

        application.BeginScreening();

        application.Status.Should().Be(ApplicationStatus.Screening);
    }

    [Fact]
    public void BeginScreening_Twice_Throws()
    {
        var application = CreateSubmitted();
        application.BeginScreening();

        var act = application.BeginScreening;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Approve_FromSubmittedOrScreening_Succeeds_ScreeningIsNotAHardPrerequisite(bool screenFirst)
    {
        var application = CreateSubmitted();
        if (screenFirst)
        {
            application.BeginScreening();
        }

        application.Approve();

        application.Status.Should().Be(ApplicationStatus.Approved);
    }

    [Fact]
    public void Approve_AfterDeclined_Throws()
    {
        var application = CreateSubmitted();
        application.Decline();

        var act = application.Approve;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Decline_FromApproved_Succeeds()
    {
        var application = CreateSubmitted();
        application.Approve();

        application.Decline();

        application.Status.Should().Be(ApplicationStatus.Declined);
    }

    [Fact]
    public void Decline_AfterLeased_Throws()
    {
        var application = CreateSubmitted();
        application.Approve();
        application.MarkLeased();

        var act = application.Decline;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkSentForSignature_WhenApproved_RecordsEnvelopeAndStaffActor_WithoutChangingStatus()
    {
        var application = CreateSubmitted();
        application.Approve();
        var staff = Guid.NewGuid();

        application.MarkSentForSignature("env-1", staff);

        application.EnvelopeId.Should().Be("env-1");
        application.SentForSignatureBy.Should().Be(staff);
        application.Status.Should().Be(ApplicationStatus.Approved);
    }

    [Fact]
    public void MarkSentForSignature_RecordsTheDraftDocument_SoTheExecutedLeaseCanBeFiledAsItsNextVersion()
    {
        var application = CreateSubmitted();
        application.Approve();
        var draftId = Guid.NewGuid();

        application.MarkSentForSignature("env-1", Guid.NewGuid(), draftId);

        application.DraftDocumentId.Should().Be(draftId);
        application.SignedLeaseVersionId.Should().BeNull();
    }

    [Fact]
    public void RecordSignedLeaseFiled_StoresTheVersionId()
    {
        var application = CreateSubmitted();
        var versionId = Guid.NewGuid();

        application.RecordSignedLeaseFiled(versionId);

        application.SignedLeaseVersionId.Should().Be(versionId);
    }

    [Fact]
    public void MarkSentForSignature_BeforeApproval_Throws()
    {
        var application = CreateSubmitted();

        var act = () => application.MarkSentForSignature("env-1", Guid.NewGuid());

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkLeased_FromApproved_Succeeds_ButNotFromSubmitted()
    {
        var approved = CreateSubmitted();
        approved.Approve();
        approved.MarkLeased();
        approved.Status.Should().Be(ApplicationStatus.Leased);

        var submitted = CreateSubmitted();
        var act = submitted.MarkLeased;
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkLeaseEnded_FromLeased_Succeeds_AndRecordsTheEffectiveDate()
    {
        var application = CreateSubmitted();
        application.Approve();
        application.MarkLeased();

        application.MarkLeaseEnded(new DateOnly(2026, 6, 30));

        application.Status.Should().Be(ApplicationStatus.LeaseEnded);
        application.LeaseEndedDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarkLeaseEnded_OnAnythingOtherThanLeased_Throws(bool approveFirst)
    {
        var application = CreateSubmitted();
        if (approveFirst)
        {
            application.Approve();
        }

        var act = () => application.MarkLeaseEnded(new DateOnly(2026, 6, 30));

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkLeaseEnded_CalledTwice_ThrowsOnTheSecondCall()
    {
        var application = CreateSubmitted();
        application.Approve();
        application.MarkLeased();
        application.MarkLeaseEnded(new DateOnly(2026, 6, 30));

        var act = () => application.MarkLeaseEnded(new DateOnly(2026, 7, 1));

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkSentForSignature_RecordsTheLeaseTerm()
    {
        var application = CreateSubmitted();
        application.Approve();

        application.MarkSentForSignature("env-1", Guid.NewGuid(), null, new DateOnly(2026, 11, 1), new DateOnly(2027, 10, 31));

        application.LeaseStartDate.Should().Be(new DateOnly(2026, 11, 1));
        application.LeaseEndDate.Should().Be(new DateOnly(2027, 10, 31));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2026-11-01")]
    [InlineData("2026-12-01")]
    public void MarkSentForSignature_WithAnEndDateButNoValidStartBeforeIt_Throws(string? start)
    {
        var application = CreateSubmitted();
        application.Approve();

        var act = () => application.MarkSentForSignature(
            "env-1", Guid.NewGuid(), null, start is null ? null : DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), new DateOnly(2026, 11, 1));

        act.Should().Throw<ArgumentException>();
    }
}
