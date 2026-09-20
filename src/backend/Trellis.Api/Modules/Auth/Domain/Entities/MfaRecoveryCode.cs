namespace Trellis.Api.Modules.Auth.Domain.Entities;

/// <summary>data_model.md's `mfa_recovery_code` — only the SHA-256 hash is ever stored; the raw code is shown once, at issuance.</summary>
public sealed class MfaRecoveryCode
{
    public Guid RecoveryCodeId { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    private MfaRecoveryCode() { }

    public static MfaRecoveryCode Create(Guid userId, string codeHash) => new()
    {
        RecoveryCodeId = Guid.NewGuid(),
        UserId = userId,
        CodeHash = codeHash,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public bool IsUsable() => UsedAt is null;

    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
