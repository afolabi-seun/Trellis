namespace Trellis.Api.Modules.Auth.Application.Dtos;

public sealed record LoginRequest(string Username, string Password);

/// <summary>
/// Carries <see cref="RefreshToken"/> so the Controller can set it as the httpOnly cookie
/// security_guide.md specifies — never serialized to the client body. The Controller builds a
/// separate, public-facing response without this field; this DTO never reaches
/// <c>ToActionResult()</c> directly, which is why <c>AuthController.Login</c> is documented as
/// the one hand-written exception to the thin-controller one-liner pattern.
/// </summary>
public sealed record LoginResult(
    bool MfaRequired,
    string? PartialToken,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAt,
    string? RefreshToken);

/// <summary>
/// The public-facing shape of a login response — everything <see cref="LoginResult"/> carries
/// except <see cref="LoginResult.RefreshToken"/>, which never leaves the Controller as JSON.
/// Named (not an anonymous object) so Swashbuckle can generate an accurate OpenAPI response
/// schema for <c>POST /api/v1/auth/login</c> — frontend_architecture.md's typed-client story
/// depends on every endpoint's response actually being documented, not just its request body.
/// </summary>
public sealed record LoginResponse(
    bool MfaRequired, string? PartialToken, string? AccessToken, DateTimeOffset? AccessTokenExpiresAt);

/// <summary>The shape <c>mfa/verify</c>'s success branch and <c>refresh</c> both return — a fresh access token, nothing else.</summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);

public sealed record MfaVerifyRequest(string PartialToken, string? TotpCode, string? RecoveryCode);

public sealed record MfaEnrollResponse(string TotpSecretBase32, string OtpAuthUri);

public sealed record MfaEnrollConfirmRequest(string TotpCode);

public sealed record MfaEnrollConfirmResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record RecoveryCodesRegenerateRequest(string CurrentPassword);

public sealed record RecoveryCodesRegenerateResponse(IReadOnlyList<string> Codes);

public sealed record PasswordForgotRequest(string Username);

public sealed record PasswordResetRequest(string Token, string NewPassword);
