namespace Trellis.Api.Modules.PropertyAsset.Application.Dtos;

public sealed record UnitAssetCreateRequest(string Label, decimal? SizeSqft, decimal? AcquisitionValue);

public sealed record UnitAssetUpdateRequest(string Label, decimal? SizeSqft, decimal? AcquisitionValue, string RowVersion);

public sealed record UnitAssetResponse(
    Guid UnitAssetId, Guid PropertyId, string Label, string Status,
    decimal? SizeSqft, decimal? AcquisitionValue, string RowVersion, DateTimeOffset CreatedAt);
