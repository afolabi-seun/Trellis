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

    /// <summary>Leasing's e-signature completion (BDR-LSE-006) — requires the caller's TenantContext.Current to be set (Scoped() lookup); LeasingWebhookService sets it deliberately after resolving the owning tenant.</summary>
    Task<ServiceResult<UnitAssetResponse>> MarkOccupiedAsync(Guid unitAssetId);

    Task<ServiceResult<UnitAssetResponse>> MarkVacantAsync(Guid unitAssetId);

    /// <summary>billing_design.md's usage metering — Billing's own UsageSnapshotBackgroundService depends on this, the same plain-constructor-dependency cross-module pattern DocumentService already uses for IWorkflowInstanceService/IExtractionService.</summary>
    Task<int> CountActiveForTenantAsync(Guid tenantId);
}
