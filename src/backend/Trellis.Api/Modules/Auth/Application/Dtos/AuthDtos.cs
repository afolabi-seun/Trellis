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

public sealed record MfaVerifyRequest(string PartialToken, string? TotpCode, string? RecoveryCode);

public sealed record MfaEnrollResponse(string TotpSecretBase32, string OtpAuthUri);

public sealed record MfaEnrollConfirmRequest(string TotpCode);

public sealed record MfaEnrollConfirmResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record RecoveryCodesRegenerateRequest(string CurrentPassword);

public sealed record RecoveryCodesRegenerateResponse(IReadOnlyList<string> Codes);

public sealed record PasswordForgotRequest(string Username);

public sealed record PasswordResetRequest(string Token, string NewPassword);
