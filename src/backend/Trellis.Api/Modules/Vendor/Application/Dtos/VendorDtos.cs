namespace Trellis.Api.Modules.Vendor.Application.Dtos;

/// <summary>Creates the Vendor identity and the caller's own first TenantVendorRelationship in one step — the common case (a tenant onboarding a vendor it's never engaged before via Trellis).</summary>
public sealed record VendorCreateRequest(string VendorName, string Category);

public sealed record VendorInviteUserRequest(string Email);

// No RowVersion/Status: VendorRecord has no Update/Archive (BDR-VEN-006's own reasoning, see
// VendorRecord.cs's doc comment) — Create, GetById, and Search are the whole surface.
public sealed record VendorResponse(Guid VendorId, string VendorName, string Category, DateTimeOffset CreatedAt);
