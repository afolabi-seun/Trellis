namespace Trellis.Api.Modules.Vendor.Application.Dtos;

public sealed record TenantVendorRelationshipResponse(
    Guid TenantVendorRelationshipId,
    Guid TenantId,
    Guid VendorId,
    string Status,
    DateTimeOffset? TerminatedAt,
    Guid? TerminatedByUserId,
    DateTimeOffset CreatedAt);
