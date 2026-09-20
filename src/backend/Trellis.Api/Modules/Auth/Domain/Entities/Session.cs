namespace Trellis.Api.Modules.Auth.Domain.Entities;

/// <summary>
/// data_model.md's `trellis_auth.session` — the one revocation mechanism logout, password
/// reset, and tenant offboarding all share (security_guide.md).
/// </summary>
public sealed class Session
{
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// SHA-256 hash of the opaque refresh token issued alongside this session — the raw token
    /// is never stored, same principle as <c>password_reset_token</c> (security_guide.md).
    /// <c>POST /api/v1/auth/refresh</c> hashes the presented cookie value and looks a session up
    /// by this column; there is no rotation, so it never changes for the life of the session —
    /// a refresh just re-issues an access token against the same row, bounded by
    /// <see cref="ExpiresAt"/>.
    /// </summary>
    public string RefreshTokenHash { get; private set; } = null!;

    private Session() { }

    public static Session Create(Guid userId, string refreshTokenHash, TimeSpan expiry) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = userId,
        RefreshTokenHash = refreshTokenHash,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.Add(expiry),
    };

    public bool IsActive() => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke() => RevokedAt ??= DateTimeOffset.UtcNow;
}
