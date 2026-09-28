using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;

namespace Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

public interface IUnitAssetService
{
    Task<ServiceResult<UnitAssetResponse>> CreateAsync(Guid propertyId, UnitAssetCreateRequest request);

    Task<ServiceResult<UnitAssetResponse>> GetByIdAsync(Guid unitAssetId);

    Task<ServiceResult<PagedResponse<UnitAssetResponse>>> ListByPropertyAsync(Guid propertyId, string? cursor, int limit);

    Task<ServiceResult<UnitAssetResponse>> UpdateAsync(Guid unitAssetId, UnitAssetUpdateRequest request);

    Task<ServiceResult<UnitAssetResponse>> ArchiveAsync(Guid unitAssetId);

    /// <summary>billing_design.md's usage metering — Billing's own UsageSnapshotBackgroundService depends on this, the same plain-constructor-dependency cross-module pattern DocumentService already uses for IWorkflowInstanceService/IExtractionService.</summary>
    Task<int> CountActiveForTenantAsync(Guid tenantId);
}
