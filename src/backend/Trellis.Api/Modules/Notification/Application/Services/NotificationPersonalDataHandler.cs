using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.Notification.Application.Interfaces;

namespace Trellis.Api.Modules.Notification.Application.Services;

/// <summary>Any subject (applicant, owner, or staff) can appear here as a RecipientAddress — matched directly, no cross-module resolution needed.</summary>
public sealed class NotificationPersonalDataHandler(INotificationDeliveryRepository deliveryRepository) : IPersonalDataHandler
{
    public string ModuleName => "Notification";

    public async Task<object?> ExportAsync(string subjectEmail)
    {
        var deliveries = await deliveryRepository.ListByRecipientAddressAsync(TenantContext.Current!.TenantId, subjectEmail);
        return deliveries.Count == 0
            ? null
            : deliveries.Select(d => new { d.DeliveryId, d.TemplateKey, Status = d.Status.ToString(), d.CreatedAt }).ToList();
    }

    public async Task<ErasureSummary> EraseAsync(string subjectEmail)
    {
        var deliveries = await deliveryRepository.ListByRecipientAddressAsync(TenantContext.Current!.TenantId, subjectEmail);
        foreach (var delivery in deliveries)
        {
            delivery.RedactRecipientAddress();
        }

        await deliveryRepository.SaveChangesAsync();

        return new ErasureSummary(ModuleName, deliveries.Count);
    }
}
