using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>BDR-LSE-010 — staff record a renewal offer on any active lease, and its outcome. Requires the caller's TenantContext.Current.</summary>
public interface IRenewalOfferService
{
    Task<ServiceResult<RenewalOfferResponse>> CreateAsync(RenewalOfferCreateRequest request, Guid actorUserId);

    /// <summary>Extends the lease (PropertyAsset) and, for a new rent, schedules the rent change from its date (Rent Collection) — the lease keeps its id, and no new application is involved.</summary>
    Task<ServiceResult<RenewalOfferResponse>> AcceptAsync(Guid renewalOfferId, RenewalOfferAcceptRequest request, Guid actorUserId);

    Task<ServiceResult<RenewalOfferResponse>> DeclineAsync(Guid renewalOfferId, RenewalOfferDeclineRequest request, Guid actorUserId);

    Task<ServiceResult<RenewalOfferResponse>> ExpireAsync(Guid renewalOfferId, Guid actorUserId);

    Task<ServiceResult<RenewalOfferResponse>> GetByIdAsync(Guid renewalOfferId);

    /// <summary>Status: OFFERED, ACCEPTED, DECLINED, or EXPIRED. Both filters optional.</summary>
    Task<ServiceResult<IReadOnlyList<RenewalOfferResponse>>> ListAsync(Guid? leaseId, string? status);
}
