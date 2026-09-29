using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class PaymentMethodServiceTests
{
    private readonly IPaymentMethodRepository _repository = Substitute.For<IPaymentMethodRepository>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private PaymentMethodService CreateSut() => new(_repository, _paymentGateway, _auditService);

    [Fact]
    public async Task InitializeCardCaptureAsync_DelegatesToTheGateway_AndReturnsItsResult()
    {
        var tenantId = Guid.NewGuid();
        _paymentGateway.InitializeCardCaptureAsync(tenantId, "admin@example.com")
            .Returns(new TokenizeResult("https://paystack.test/pay/ref", "access-ref", "ref"));
        var sut = CreateSut();

        var result = await sut.InitializeCardCaptureAsync(tenantId, "admin@example.com");

        result.Data!.Reference.Should().Be("ref");
    }

    [Fact]
    public async Task ConfirmCardCaptureAsync_WhenAnOldDefaultExists_MarksItNotDefault_AndTheNewOneBecomesDefault()
    {
        var tenantId = Guid.NewGuid();
        var old = PaymentMethod.Create(tenantId, "PAYSTACK", "old-auth-code", "1111", "VISA");
        _repository.ListForTenantAsync(tenantId).Returns([old]);
        _paymentGateway.VerifyTransactionAsync("new-ref").Returns(new VerifyResult(true, "auth-new-ref", "4242", "VISA"));
        var sut = CreateSut();

        var result = await sut.ConfirmCardCaptureAsync(tenantId, new ConfirmCardCaptureRequest("new-ref"));

        old.IsDefault.Should().BeFalse();
        result.Data!.IsDefault.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Is<PaymentMethod>(m => m.ProviderAuthorizationCode == "auth-new-ref"));
    }

    [Fact]
    public async Task ConfirmCardCaptureAsync_WhenVerificationFails_ReturnsBusinessRuleViolation_AndNeverAddsAPaymentMethod()
    {
        var tenantId = Guid.NewGuid();
        _paymentGateway.VerifyTransactionAsync("bad-ref").Returns(new VerifyResult(false, "", "", ""));
        var sut = CreateSut();

        var result = await sut.ConfirmCardCaptureAsync(tenantId, new ConfirmCardCaptureRequest("bad-ref"));

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(Arg.Any<PaymentMethod>());
    }
}
