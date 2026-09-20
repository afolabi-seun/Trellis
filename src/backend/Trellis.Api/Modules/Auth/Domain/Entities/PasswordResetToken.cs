namespace Trellis.Api.Modules.Auth.Domain.Entities;

/// <summary>data_model.md's `password_reset_token` — only the SHA-256 hash is ever stored, never the raw token.</summary>
public sealed class PasswordResetToken
{
    public Guid TokenId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public string? RequestedIp { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string tokenHash, TimeSpan expiry, string? requestedIp) => new()
    {
        TokenId = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = DateTimeOffset.UtcNow.Add(expiry),
        RequestedIp = requestedIp,
    };

    public bool IsUsable() => UsedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
