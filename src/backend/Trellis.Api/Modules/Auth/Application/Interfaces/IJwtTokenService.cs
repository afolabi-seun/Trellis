using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>security_guide.md's JWT Structure — RS256, the exact Tenant-side/Vendor claim shapes.</summary>
public interface IJwtTokenService
{
    /// <summary>Issues the Tenant-side (or platform, for SUPER_ADMIN) access token — carries `tenant_id`, never `vendor_id`.</summary>
    string IssueAccessToken(AppUser user, Guid sessionId);

    /// <summary>Issues a Vendor access token — carries `vendor_id`, never `tenant_id` (multi_tenant_design.md). Not exercised until the Vendor module exists.</summary>
    string IssueVendorAccessToken(Guid vendorUserId, Guid vendorId, Guid sessionId);

    /// <summary>The 5-minute partial token issued between password verification and MFA verification.</summary>
    string IssuePartialToken(Guid userId);

    /// <summary>Returns the user id if the partial token is valid and unexpired, else null.</summary>
    Guid? ValidatePartialToken(string partialToken);

    /// <summary>The RS256 public key, JWKS-formatted, for `/api/v1/auth/.well-known/jwks.json`.</summary>
    string GetJwksJson();
}
