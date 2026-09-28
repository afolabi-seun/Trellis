using Trellis.Api.Modules.Billing.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Interfaces;

public interface IPaymentMethodRepository
{
    Task AddAsync(PaymentMethod paymentMethod);

    Task<PaymentMethod?> GetDefaultAsync(Guid tenantId);

    /// <summary>All of this tenant's payment methods (default and superseded) — used to flip the old default off when a new one is added.</summary>
    Task<IReadOnlyList<PaymentMethod>> ListForTenantAsync(Guid tenantId);

    Task SaveChangesAsync();
}
