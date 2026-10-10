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

    /// <summary>BDR-RENT-005 — the Owner report's rent rollup needs every unit id under an Owner's properties; mirrors IPropertyService.ListPropertyIdsByOwnerAsync's plain-list shape. Requires the caller's TenantContext.Current (Scoped()).</summary>
    Task<IReadOnlyList<Guid>> ListUnitAssetIdsByPropertiesAsync(IReadOnlyCollection<Guid> propertyIds);

    /// <summary>billing_design.md's usage metering — Billing's own UsageSnapshotBackgroundService depends on this, the same plain-constructor-dependency cross-module pattern DocumentService already uses for IWorkflowInstanceService/IExtractionService.</summary>
    Task<int> CountActiveForTenantAsync(Guid tenantId);
}
