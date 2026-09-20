using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Auth.Configuration;

/// <summary>
/// coding_standards.md's Configuration pattern — every value here is the literal number
/// security_guide.md's login/MFA/password-reset flows state, made configurable rather than
/// hardcoded, bound from <c>Modules__Auth__*</c> environment variables. The JWT signing key
/// itself is NOT here — that's a secret, read through <c>ISecretProvider</c>, never bound as a
/// plain option (security_guide.md's Secrets Management).
/// </summary>
public class AuthOptions
{
    [Range(1, 120)]
    public int AccessTokenExpiryMinutes { get; init; } = 15;

    [Range(1, 24)]
    public int RefreshTokenExpiryHours { get; init; } = 8;

    /// <summary>The short-lived token issued between password verification and MFA verification.</summary>
    [Range(1, 15)]
    public int MfaPartialTokenExpiryMinutes { get; init; } = 5;

    [Range(5, 120)]
    public int PasswordResetTokenExpiryMinutes { get; init; } = 30;

    [Range(1, 20)]
    public int MfaRecoveryCodeCount { get; init; } = 10;

    [Range(1, 20)]
    public int FailedLoginLockoutThreshold { get; init; } = 5;

    [Range(1, 60)]
    public int FailedLoginLockoutWindowMinutes { get; init; } = 15;

    [Range(1, 1440)]
    public int FailedLoginLockoutDurationMinutes { get; init; } = 30;

    /// <summary>The `auth-sensitive` rate limiter's permit count per window (security_guide.md).</summary>
    [Range(1, 100)]
    public int RateLimitPermitLimit { get; init; } = 10;

    [Range(1, 60)]
    public int RateLimitWindowMinutes { get; init; } = 5;
}
