using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IRenewalOfferRepository
{
    Task AddAsync(RenewalOffer offer);

    Task<RenewalOffer?> GetByIdAsync(Guid renewalOfferId);

    Task<bool> HasOpenOfferForLeaseAsync(Guid leaseId);

    /// <summary>Newest first; both filters optional.</summary>
    Task<IReadOnlyList<RenewalOffer>> ListAsync(Guid? leaseId, RenewalOfferStatus? status);

    Task SaveChangesAsync();
}
