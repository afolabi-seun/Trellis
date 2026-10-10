namespace Trellis.Api.Modules.PropertyAsset.Application.Dtos;

public sealed record UnitAssetCreateRequest(string Label, decimal? SizeSqft, decimal? AcquisitionValue);

public sealed record UnitAssetUpdateRequest(string Label, decimal? SizeSqft, decimal? AcquisitionValue, string RowVersion);

/// <summary>VacantSince (BDR-AST-005) is null while occupied, and null for a vacant unit whose vacancy start is unknown — never a guessed date.</summary>
public sealed record UnitAssetResponse(
    Guid UnitAssetId, Guid PropertyId, string Label, string Status, string OccupancyStatus, DateOnly? VacantSince,
    decimal? SizeSqft, decimal? AcquisitionValue, string RowVersion, DateTimeOffset CreatedAt);
