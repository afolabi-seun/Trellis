using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Configuration;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class InvoiceServiceTests
{
    private readonly IInvoiceRepository _repository = Substitute.For<IInvoiceRepository>();
    private readonly IModuleEntitlementRepository _entitlementRepository = Substitute.For<IModuleEntitlementRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly List<InvoiceLineItem> _capturedLineItems = [];

    public InvoiceServiceTests() =>
        _repository.AddLineItemAsync(Arg.Do<InvoiceLineItem>(l => _capturedLineItems.Add(l)));

    private InvoiceService CreateSut() =>
        new(_repository, _entitlementRepository, _auditService, Options.Create(new BillingOptions { PerUnitRate = 5.00m }));

    [Fact]
    public async Task GenerateForPeriodAsync_WithNoActiveEntitlements_ProducesOneUsageLineItem_AndFinalizesToOpen()
    {
        var tenantId = Guid.NewGuid();
        _entitlementRepository.ListForTenantAsync(tenantId).Returns([]);
        var sut = CreateSut();

        var invoice = await sut.GenerateForPeriodAsync(
            tenantId, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "USD", 20);

        invoice.Status.Should().Be(InvoiceStatus.Open);
        invoice.SubtotalAmount.Should().Be(100.00m);
        invoice.TotalAmount.Should().Be(100.00m);
        _capturedLineItems.Should().ContainSingle();
        _capturedLineItems[0].Amount.Should().Be(100.00m);
    }

    [Fact]
    public async Task GenerateForPeriodAsync_WithAnActiveNonZeroPriceEntitlement_AddsASecondLineItem()
    {
        var tenantId = Guid.NewGuid();
        var entitlement = ModuleEntitlement.Create(tenantId, "INSPECTION", 49.00m);
        _entitlementRepository.ListForTenantAsync(tenantId).Returns([entitlement]);
        var sut = CreateSut();

        var invoice = await sut.GenerateForPeriodAsync(
            tenantId, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "USD", 10);

        _capturedLineItems.Should().HaveCount(2);
        invoice.TotalAmount.Should().Be(50.00m + 49.00m);
    }

    [Fact]
    public async Task GenerateForPeriodAsync_SkipsADisabledEntitlement_AndAZeroPricedOne()
    {
        var tenantId = Guid.NewGuid();
        var disabled = ModuleEntitlement.Create(tenantId, "AI", 49.00m);
        disabled.Disable();
        var zeroPriced = ModuleEntitlement.Create(tenantId, "INSPECTION", 0);
        _entitlementRepository.ListForTenantAsync(tenantId).Returns([disabled, zeroPriced]);
        var sut = CreateSut();

        await sut.GenerateForPeriodAsync(tenantId, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "USD", 10);

        _capturedLineItems.Should().ContainSingle();
    }
}
