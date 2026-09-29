using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Notification.Application.Interfaces;
using Trellis.Api.Modules.Notification.Application.Services;
using Trellis.Api.Modules.Notification.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Notification;

public sealed class NotificationPersonalDataHandlerTests : IDisposable
{
    private const string SubjectEmail = "someone@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly INotificationDeliveryRepository _deliveryRepository = Substitute.For<INotificationDeliveryRepository>();

    public NotificationPersonalDataHandlerTests() => TenantContext.Current = new TenantContext(_tenantId);

    public void Dispose() => TenantContext.Current = null;

    private NotificationPersonalDataHandler CreateSut() => new(_deliveryRepository);

    private NotificationDelivery CreateDelivery() => NotificationDelivery.Create(
        _tenantId, NotificationRecipientType.ProspectiveTenant, SubjectEmail, "SCREENING_COMPLETE", NotificationChannel.Email, null, null);

    [Fact]
    public async Task ExportAsync_WithNoDeliveries_ReturnsNull()
    {
        _deliveryRepository.ListByRecipientAddressAsync(_tenantId, SubjectEmail).Returns([]);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EraseAsync_RedactsTheRecipientAddress_OnEveryMatchingDelivery_KeepsTheRow()
    {
        var delivery = CreateDelivery();
        _deliveryRepository.ListByRecipientAddressAsync(_tenantId, SubjectEmail).Returns([delivery]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        delivery.RecipientAddress.Should().Be("[erased]");
        summary.ModuleName.Should().Be("Notification");
        summary.RecordsAffected.Should().Be(1);
        await _deliveryRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_IsIdempotent_ASecondCallFindsNothingLeftToRedact()
    {
        _deliveryRepository.ListByRecipientAddressAsync(_tenantId, SubjectEmail).Returns([]);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        summary.RecordsAffected.Should().Be(0);
    }
}
