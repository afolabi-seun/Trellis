using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class RenewalOfferRepository(LeasingDbContext dbContext) : TenantAwareRepository<RenewalOffer>, IRenewalOfferRepository
{
    public async Task AddAsync(RenewalOffer offer) => await dbContext.RenewalOffers.AddAsync(offer);

    public Task<RenewalOffer?> GetByIdAsync(Guid renewalOfferId) =>
        Scoped(dbContext.RenewalOffers).FirstOrDefaultAsync(o => o.RenewalOfferId == renewalOfferId);

    public Task<bool> HasOpenOfferForLeaseAsync(Guid leaseId) =>
        Scoped(dbContext.RenewalOffers).AnyAsync(o => o.LeaseId == leaseId && o.Status == RenewalOfferStatus.Offered);

    public async Task<IReadOnlyList<RenewalOffer>> ListAsync(Guid? leaseId, RenewalOfferStatus? status)
    {
        var query = Scoped(dbContext.RenewalOffers);
        if (leaseId is { } lease)
        {
            query = query.Where(o => o.LeaseId == lease);
        }

        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        return await query.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.RenewalOfferId).ToListAsync();
    }

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
