using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IPaymentMethodService
{
    Task<ServiceResult<PaymentMethodResponse>> GetDefaultAsync(Guid tenantId);

    Task<ServiceResult<InitializeCardCaptureResponse>> InitializeCardCaptureAsync(Guid tenantId, string email);

    Task<ServiceResult<PaymentMethodResponse>> ConfirmCardCaptureAsync(Guid tenantId, ConfirmCardCaptureRequest request);

    /// <summary>UsageSnapshotBackgroundService's own charge-attempt step needs the domain entity's ProviderAuthorizationCode directly, not a masked Response.</summary>
    Task<PaymentMethod?> GetDefaultEntityAsync(Guid tenantId);
}
