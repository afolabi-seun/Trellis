namespace Trellis.Api.Modules.PropertyAsset.Application.Dtos;

public sealed record PropertyCreateRequest(
    Guid OwnerId,
    string Address,
    string PropertyType,
    decimal? SizeSqft,
    DateOnly? AcquisitionDate,
    decimal? AcquisitionValue,
    decimal? CurrentValuation);

public sealed record PropertyUpdateRequest(
    Guid OwnerId,
    string Address,
    string PropertyType,
    decimal? SizeSqft,
    DateOnly? AcquisitionDate,
    decimal? AcquisitionValue,
    decimal? CurrentValuation,
    string RowVersion);

public sealed record PropertyResponse(
    Guid PropertyId,
    Guid OwnerId,
    string Address,
    string PropertyType,
    string Status,
    decimal? SizeSqft,
    DateOnly? AcquisitionDate,
    decimal? AcquisitionValue,
    decimal? CurrentValuation,
    string RowVersion,
    DateTimeOffset CreatedAt);
