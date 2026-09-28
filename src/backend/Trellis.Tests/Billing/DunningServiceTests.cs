using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class DunningServiceTests
{
    private readonly IInvoiceRepository _invoiceRepository = Substitute.For<IInvoiceRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IPaymentTransactionRepository _transactionRepository = Substitute.For<IPaymentTransactionRepository>();
    private readonly IPaymentMethodRepository _paymentMethodRepository = Substitute.For<IPaymentMethodRepository>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public DunningServiceTests() =>
        _tenantService.SuspendAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ServiceResult<TenantResponse>.Success(new TenantResponse(Guid.NewGuid(), "Acme", "US", "SUSPENDED", "STARTER", DateTimeOffset.UtcNow)));

    private DunningService CreateSut() =>
        new(_invoiceRepository, _subscriptionRepository, _transactionRepository, _paymentMethodRepository, _paymentGateway, _tenantService, _auditService);

    private (Invoice Invoice, Subscription Subscription) CreateInvoiceAndSubscription(SubscriptionStatus status, DateTimeOffset? dueAt = null)
    {
        var tenantId = Guid.NewGuid();
        var subscription = Subscription.Create(tenantId, "USD", 1);
        subscription.Activate();
        if (status == SubscriptionStatus.PastDue)
        {
            subscription.MarkPastDue();
        }

        var invoice = Invoice.Create(tenantId, subscription.SubscriptionId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "USD");
        invoice.Finalize(100, 0, 100, dueAt ?? DateTimeOffset.UtcNow);
        _subscriptionRepository.GetByIdAsync(subscription.SubscriptionId).Returns(subscription);
        _paymentMethodRepository.GetDefaultAsync(tenantId).Returns(PaymentMethod.Create(tenantId, "PAYSTACK", "auth-code", "4242", "VISA"));

        return (invoice, subscription);
    }

    [Fact]
    public async Task AttemptChargeAsync_WhenChargeSucceeds_MarksInvoicePaid_AndActivatesSubscription()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.Active);
        _paymentGateway.ChargeAsync("auth-code", 100, "USD", Arg.Any<string>())
            .Returns(new ChargeResult(true, "provider-ref", null));
        var sut = CreateSut();

        var succeeded = await sut.AttemptChargeAsync(invoice);

        succeeded.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task AttemptChargeAsync_WhenTheFirstAttemptFails_MarksSubscriptionPastDue_ButDoesNotSuspendYet()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.Active);
        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ChargeResult(false, "provider-ref", null));
        var sut = CreateSut();

        var succeeded = await sut.AttemptChargeAsync(invoice);

        succeeded.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        invoice.Status.Should().Be(InvoiceStatus.Open);
        await _tenantService.DidNotReceive().SuspendAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AttemptChargeAsync_WhenTheThirdTotalAttemptFails_SuspendsSubscriptionAndTenant_AndMarksInvoiceFailed()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.PastDue);
        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ChargeResult(false, "provider-ref", null));
        _transactionRepository.ListForInvoiceAsync(invoice.InvoiceId).Returns(
        [
            PaymentTransaction.Create(invoice.TenantId, invoice.InvoiceId, "r1", 100, PaymentTransactionStatus.Failed, null),
            PaymentTransaction.Create(invoice.TenantId, invoice.InvoiceId, "r2", 100, PaymentTransactionStatus.Failed, null),
            PaymentTransaction.Create(invoice.TenantId, invoice.InvoiceId, "r3", 100, PaymentTransactionStatus.Failed, null),
        ]);
        var sut = CreateSut();

        await sut.AttemptChargeAsync(invoice);

        invoice.Status.Should().Be(InvoiceStatus.Failed);
        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
        await _tenantService.Received(1).SuspendAsync(invoice.TenantId, "non-payment");
    }

    [Fact]
    public async Task AttemptChargeAsync_WhenNoDefaultPaymentMethodExists_IsTreatedAsAFailedAttempt()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.Active);
        _paymentMethodRepository.GetDefaultAsync(invoice.TenantId).Returns((PaymentMethod?)null);
        var sut = CreateSut();

        var succeeded = await sut.AttemptChargeAsync(invoice);

        succeeded.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        await _paymentGateway.DidNotReceive().ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RetryDueSubscriptionsAsync_WhenTheDayTwoRetryIsDue_AttemptsChargeAgain()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.PastDue, DateTimeOffset.UtcNow.AddDays(-3));
        _subscriptionRepository.ListPastDueAsync().Returns([subscription]);
        _invoiceRepository.GetOpenForTenantAsync(invoice.TenantId).Returns(invoice);
        _transactionRepository.ListForInvoiceAsync(invoice.InvoiceId).Returns(
            [PaymentTransaction.Create(invoice.TenantId, invoice.InvoiceId, "r1", 100, PaymentTransactionStatus.Failed, null)]);
        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ChargeResult(true, "provider-ref", null));
        var sut = CreateSut();

        await sut.RetryDueSubscriptionsAsync();

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task RetryDueSubscriptionsAsync_WhenTheRetryIsNotYetDue_DoesNothing()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.PastDue);
        _subscriptionRepository.ListPastDueAsync().Returns([subscription]);
        _invoiceRepository.GetOpenForTenantAsync(invoice.TenantId).Returns(invoice);
        _transactionRepository.ListForInvoiceAsync(invoice.InvoiceId).Returns(
            [PaymentTransaction.Create(invoice.TenantId, invoice.InvoiceId, "r1", 100, PaymentTransactionStatus.Failed, null)]);
        var sut = CreateSut();

        await sut.RetryDueSubscriptionsAsync();

        invoice.Status.Should().Be(InvoiceStatus.Open);
        await _paymentGateway.DidNotReceive().ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReactivateOnSuccessAsync_ReversesBothTheSubscriptionAndTheTenantSuspension()
    {
        var (invoice, subscription) = CreateInvoiceAndSubscription(SubscriptionStatus.PastDue);
        subscription.Suspend();
        _invoiceRepository.GetOpenForTenantAsync(invoice.TenantId).Returns(invoice);
        var sut = CreateSut();

        await sut.ReactivateOnSuccessAsync(subscription.SubscriptionId, "provider-ref");

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        await _tenantService.Received(1).ReactivateAsync(invoice.TenantId);
    }
}
