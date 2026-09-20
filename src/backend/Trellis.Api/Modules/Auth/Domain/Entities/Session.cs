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

    private Session() { }

    public static Session Create(Guid userId, TimeSpan expiry) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = userId,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.Add(expiry),
    };

    public bool IsActive() => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke() => RevokedAt ??= DateTimeOffset.UtcNow;
}
