using FluentAssertions;
using NSubstitute;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class BillingServiceTests
{
    private readonly IModuleEntitlementService _moduleEntitlementService = Substitute.For<IModuleEntitlementService>();
    private readonly IDunningService _dunningService = Substitute.For<IDunningService>();

    private BillingService CreateSut() => new(_moduleEntitlementService, _dunningService);

    [Fact]
    public async Task IsModuleEntitledAsync_DelegatesToModuleEntitlementService()
    {
        var tenantId = Guid.NewGuid();
        _moduleEntitlementService.IsEntitledAsync(tenantId, "INSPECTION").Returns(true);
        var sut = CreateSut();

        (await sut.IsModuleEntitledAsync(tenantId, "INSPECTION")).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_ForAChargeSuccessEvent_CallsReactivateOnSuccess()
    {
        var subscriptionId = Guid.NewGuid();
        var payload = "{\"event\":\"charge.success\",\"data\":{\"subscriptionId\":\"" + subscriptionId + "\",\"reference\":\"ref-123\"}}";
        var sut = CreateSut();

        await sut.ProcessWebhookEventAsync(payload);

        await _dunningService.Received(1).ReactivateOnSuccessAsync(subscriptionId, "ref-123");
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_ForAnUnrecognizedEventType_DoesNothing()
    {
        var payload = """{"event":"subscription.create","data":{}}""";
        var sut = CreateSut();

        await sut.ProcessWebhookEventAsync(payload);

        await _dunningService.DidNotReceiveWithAnyArgs().ReactivateOnSuccessAsync(default, default!);
    }
}
