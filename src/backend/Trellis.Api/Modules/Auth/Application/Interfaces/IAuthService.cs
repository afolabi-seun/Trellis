using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;

namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>
/// Auth's public contract with the rest of the app — the Tenant module calls
/// <see cref="CreateTenantAdminAsync"/> during provisioning (multi_tenant_design.md's
/// <c>ProvisionTenantAsync</c>), never `trellis_auth` directly.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates the first <c>TENANT_ADMIN</c> user for a newly provisioned tenant, participating
    /// in the caller's own transaction — this does not commit independently.
    /// </summary>
    Task<ServiceResult<AppUserSummary>> CreateTenantAdminAsync(Guid tenantId, string adminEmail);

    /// <summary>security_guide.md's login flow — Argon2id verification, lockout, and the MFA branch.</summary>
    Task<ServiceResult<LoginResult>> LoginAsync(string username, string password);

    /// <summary>The `mfa/verify` endpoint's two mutually exclusive branches — exactly one of <paramref name="request"/>'s TOTP/recovery-code fields is set.</summary>
    Task<ServiceResult<LoginResult>> VerifyMfaAsync(MfaVerifyRequest request);

    /// <summary>Re-issues an access token against the session the presented refresh token cookie belongs to. No rotation.</summary>
    Task<ServiceResult<LoginResult>> RefreshAsync(string presentedRefreshToken);

    /// <summary>Starts MFA enrollment — generates and returns a TOTP secret; not yet active until confirmed.</summary>
    Task<ServiceResult<MfaEnrollResponse>> EnrollMfaAsync(Guid userId);

    /// <summary>Confirms enrollment with a valid TOTP code, activates MFA, and issues the 10 recovery codes (one-time reveal).</summary>
    Task<ServiceResult<MfaEnrollConfirmResponse>> ConfirmMfaEnrollmentAsync(Guid userId, string totpCode);

    /// <summary>Requires re-authentication; invalidates every previously issued code.</summary>
    Task<ServiceResult<RecoveryCodesRegenerateResponse>> RegenerateRecoveryCodesAsync(Guid userId, string currentPassword);

    /// <summary>Always returns success regardless of whether the account exists — existence-non-disclosure (security_guide.md).</summary>
    Task RequestPasswordResetAsync(string username, string? requestedIp);

    Task<ServiceResult<object>> ResetPasswordAsync(string token, string newPassword);
}
