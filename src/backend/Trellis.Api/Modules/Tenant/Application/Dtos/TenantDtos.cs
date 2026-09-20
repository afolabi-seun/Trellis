namespace Trellis.Api.Modules.Tenant.Application.Dtos;

/// <summary>api_standards.md's `POST /platform/tenants` request body.</summary>
public sealed record TenantProvisionRequest(string TenantName, string CountryCode, string AdminEmail);

// No RowVersion field: coding_standards.md's UseXminAsConcurrencyToken()/RowVersion pattern
// applies to entities with an Update/Patch endpoint, and Tenant has neither in Phase 1 — only
// provisioning (create) and the offboard action endpoint, not a generic field update. The
// concurrency token is still active at the EF Core level regardless (TenantConfiguration);
// nothing here needs to round-trip it yet. Auth's password reset flow is where Phase 1 actually
// exercises and tests optimistic concurrency (coding_standards.md's negative-path requirement).
public sealed record TenantResponse(
    Guid TenantId,
    string TenantName,
    string CountryCode,
    string Status,
    string SubscriptionTier,
    DateTimeOffset CreatedAt);
