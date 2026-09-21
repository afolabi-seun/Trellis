namespace Trellis.Api.Modules.PropertyAsset.Application.Dtos;

public sealed record OwnerCreateRequest(string Name, string? Email, string? Phone);

public sealed record OwnerUpdateRequest(string Name, string? Email, string? Phone, string RowVersion);

public sealed record OwnerResponse(
    Guid OwnerId, string Name, string? Email, string? Phone, string Status, string RowVersion, DateTimeOffset CreatedAt);
