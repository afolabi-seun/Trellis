using FluentAssertions;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Domain.Exceptions;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class InvoiceTests
{
    private static Invoice CreateDraft() =>
        Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "USD");

    [Fact]
    public void MarkPaid_WhenStillDraft_ThrowsInvalidStatusTransitionException()
    {
        var invoice = CreateDraft();

        var act = () => invoice.MarkPaid(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkFailed_WhenStillDraft_ThrowsInvalidStatusTransitionException()
    {
        var invoice = CreateDraft();

        var act = invoice.MarkFailed;

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Finalize_TwiceInARow_ThrowsOnTheSecondCall()
    {
        var invoice = CreateDraft();
        invoice.Finalize(100, 0, 100, DateTimeOffset.UtcNow);

        var act = () => invoice.Finalize(100, 0, 100, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Void_AfterAlreadyPaid_ThrowsInvalidStatusTransitionException()
    {
        var invoice = CreateDraft();
        invoice.Finalize(100, 0, 100, DateTimeOffset.UtcNow);
        invoice.MarkPaid(DateTimeOffset.UtcNow);

        var act = invoice.Void;

        act.Should().Throw<InvalidStatusTransitionException>();
    }
}
