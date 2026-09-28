using FluentAssertions;
using Trellis.Api.Modules.RentLedger.Domain.Entities;
using Trellis.Api.Modules.RentLedger.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.RentLedger;

public sealed class RentPaymentRecordTests
{
    private static RentPaymentRecord Create() =>
        RentPaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 1000m, new DateOnly(2026, 1, 8), "EFT ref 88213", Guid.NewGuid());

    [Fact]
    public void Create_RecordsWhoEnteredIt_AndStartsNotVoided()
    {
        var recordedBy = Guid.NewGuid();

        var record = RentPaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), 1000m, new DateOnly(2026, 1, 8), null, recordedBy);

        record.RecordedByUserId.Should().Be(recordedBy);
        record.IsVoided.Should().BeFalse();
        record.RecordedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_RejectsANonPositiveAmount(decimal amount)
    {
        var act = () => RentPaymentRecord.Create(Guid.NewGuid(), Guid.NewGuid(), amount, new DateOnly(2026, 1, 8), null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Void_MarksItVoided_WithWhoAndWhy_AndLeavesTheAmountUntouched()
    {
        var record = Create();
        var voidedBy = Guid.NewGuid();

        record.Void(voidedBy, "Entered against the wrong unit");

        record.IsVoided.Should().BeTrue();
        record.VoidedBy.Should().Be(voidedBy);
        record.VoidReason.Should().Be("Entered against the wrong unit");
        record.AmountReceived.Should().Be(1000m);
    }

    [Fact]
    public void Void_CalledTwice_Throws()
    {
        var record = Create();
        record.Void(Guid.NewGuid(), "typo");

        var act = () => record.Void(Guid.NewGuid(), "again");

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
