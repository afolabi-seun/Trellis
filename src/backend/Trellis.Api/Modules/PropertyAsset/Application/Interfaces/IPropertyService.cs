using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IPropertyService
{
    Task<ServiceResult<PropertyResponse>> CreateAsync(PropertyCreateRequest request);

    Task<ServiceResult<PropertyResponse>> GetByIdAsync(Guid propertyId);

    Task<ServiceResult<PagedResponse<PropertyResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<PropertyResponse>> UpdateAsync(Guid propertyId, PropertyUpdateRequest request);

    /// <summary>Archives (never hard-deletes) — rejected as a business-rule violation while the property still has an active unit/asset.</summary>
    Task<ServiceResult<PropertyResponse>> ArchiveAsync(Guid propertyId);
}
