using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Configuration;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class SubscriptionServiceTests
{
    private readonly ISubscriptionRepository _repository = Substitute.For<ISubscriptionRepository>();
    private readonly IUsageSnapshotRepository _usageSnapshotRepository = Substitute.For<IUsageSnapshotRepository>();
    private readonly IInvoiceRepository _invoiceRepository = Substitute.For<IInvoiceRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private SubscriptionService CreateSut() =>
        new(_repository, _usageSnapshotRepository, _invoiceRepository, _auditService, Options.Create(new BillingOptions { PerUnitRate = 5.00m }));

    [Fact]
    public async Task GetOrCreateForTenantAsync_WhenNoSubscriptionExists_CreatesTrialingWithMappedCurrencyAndClampedAnchorDay()
    {
        _repository.GetByTenantIdAsync(Arg.Any<Guid>()).Returns((Subscription?)null);
        var sut = CreateSut();

        var subscription = await sut.GetOrCreateForTenantAsync(Guid.NewGuid(), "ZA", new DateTimeOffset(2026, 1, 30, 0, 0, 0, TimeSpan.Zero));

        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.Currency.Should().Be("ZAR");
        subscription.BillingCycleAnchorDay.Should().Be(28);
        await _repository.Received(1).AddAsync(Arg.Any<Subscription>());
    }

    [Fact]
    public async Task GetOrCreateForTenantAsync_WhenSubscriptionAlreadyExists_ReturnsItWithoutCreatingAnother()
    {
        var existing = Subscription.Create(Guid.NewGuid(), "USD", 15);
        _repository.GetByTenantIdAsync(Arg.Any<Guid>()).Returns(existing);
        var sut = CreateSut();

        var result = await sut.GetOrCreateForTenantAsync(existing.TenantId, "US", DateTimeOffset.UtcNow);

        result.Should().BeSameAs(existing);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Subscription>());
    }

    [Fact]
    public async Task GetSummaryForTenantAsync_WhenNoSubscriptionExists_ReturnsNotFound()
    {
        _repository.GetByTenantIdAsync(Arg.Any<Guid>()).Returns((Subscription?)null);
        var sut = CreateSut();

        var result = await sut.GetSummaryForTenantAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task GetSummaryForTenantAsync_WhenALatestSnapshotExists_ComputesProjectedTotalFromPerUnitRate()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "USD", 1);
        _repository.GetByTenantIdAsync(subscription.TenantId).Returns(subscription);
        var snapshot = UsageSnapshot.Create(subscription.TenantId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 20);
        _usageSnapshotRepository.GetLatestForTenantAsync(subscription.TenantId).Returns(snapshot);
        var sut = CreateSut();

        var result = await sut.GetSummaryForTenantAsync(subscription.TenantId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.LatestActiveUnitCount.Should().Be(20);
        result.Data.ProjectedInvoiceTotal.Should().Be(100.00m);
    }
}
