using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IOwnerService
{
    Task<ServiceResult<OwnerResponse>> CreateAsync(OwnerCreateRequest request);

    Task<ServiceResult<OwnerResponse>> GetByIdAsync(Guid ownerId);

    Task<ServiceResult<PagedResponse<OwnerResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<OwnerResponse>> UpdateAsync(Guid ownerId, OwnerUpdateRequest request);

    /// <summary>Archives (never hard-deletes) — rejected as a business-rule violation while the owner still has an active property.</summary>
    Task<ServiceResult<OwnerResponse>> ArchiveAsync(Guid ownerId);
}
