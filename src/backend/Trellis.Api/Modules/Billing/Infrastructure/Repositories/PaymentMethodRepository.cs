using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Billing.Infrastructure.Data;

namespace Trellis.Api.Modules.Billing.Infrastructure.Repositories;

public sealed class PaymentMethodRepository(BillingDbContext dbContext) : TenantAwareRepository<PaymentMethod>, IPaymentMethodRepository
{
    public async Task AddAsync(PaymentMethod paymentMethod) => await dbContext.PaymentMethods.AddAsync(paymentMethod);

    // Not Scoped() — the caller (DunningService.AttemptChargeAsync, PaymentMethodService) always
    // supplies an explicit tenantId, either from TenantContext.Current at the Presentation
    // boundary or from a background job with no ambient tenant at all.
    public Task<PaymentMethod?> GetDefaultAsync(Guid tenantId) =>
        dbContext.PaymentMethods.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsDefault);

    public async Task<IReadOnlyList<PaymentMethod>> ListForTenantAsync(Guid tenantId) =>
        await dbContext.PaymentMethods.Where(p => p.TenantId == tenantId).ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
