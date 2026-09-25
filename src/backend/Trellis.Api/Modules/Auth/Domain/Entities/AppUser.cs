namespace Trellis.Api.Modules.Auth.Domain.Entities;

/// <summary>api_standards.md's RBAC Roles table.</summary>
public enum UserRole
{
    SuperAdmin,
    TenantAdmin,
    DepartmentStaff,
    VendorUser,
}

public enum UserStatus
{
    PendingActivation,
    Active,
    Locked,
    Deactivated,
}

/// <summary>
/// data_model.md's `app_user` — a Vendor user is a first-class row here too
/// (security_guide.md's Vendor Authentication section), with <see cref="TenantId"/> null; a
/// <c>SUPER_ADMIN</c> platform account is also tenant-less.
/// </summary>
public sealed class AppUser
{
    public Guid UserId { get; private set; }
    public Guid? TenantId { get; private set; }

    /// <summary>Set only for Role == VendorUser — which single Vendor identity's login this is (BDR-VEN-004: one login, reused across every tenant that engages this vendor).</summary>
    public Guid? VendorId { get; private set; }

    public string Username { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public Guid[]? DepartmentIds { get; private set; }
    public UserStatus Status { get; private set; }
    public short FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>AES-256-GCM encrypted (security_guide.md's Encryption Architecture) — null means MFA isn't enrolled yet.</summary>
    public byte[]? TotpSecretEncrypted { get; private set; }

    private AppUser() { }

    public static AppUser Create(Guid? tenantId, string username, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        return new AppUser
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Username = username,
            Role = role,
            Status = UserStatus.PendingActivation,
            FailedLoginCount = 0,
        };
    }

    /// <summary>A Vendor's login is never tenant-owned (TenantId stays null, same as SUPER_ADMIN) — its scope is entirely vendor_assignment-driven, resolved per request, not fixed at account creation.</summary>
    public static AppUser CreateVendorUser(Guid vendorId, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        return new AppUser
        {
            UserId = Guid.NewGuid(),
            TenantId = null,
            VendorId = vendorId,
            Username = username,
            Role = UserRole.VendorUser,
            Status = UserStatus.PendingActivation,
            FailedLoginCount = 0,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        if (Status == UserStatus.PendingActivation)
        {
            Status = UserStatus.Active;
        }
    }

    public bool IsLockedOut() => LockedUntil is { } until && until > DateTimeOffset.UtcNow;

    public void RegisterFailedLogin(int lockoutThreshold, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= lockoutThreshold)
        {
            LockedUntil = DateTimeOffset.UtcNow.Add(lockoutDuration);
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>BDR-SEC-003: MFA is required for TENANT_ADMIN and SUPER_ADMIN.</summary>
    public bool RequiresMfa() => Role is UserRole.TenantAdmin or UserRole.SuperAdmin;

    public bool HasMfaEnrolled() => TotpSecretEncrypted is not null;

    public void EnrollMfa(byte[] totpSecretEncrypted) => TotpSecretEncrypted = totpSecretEncrypted;
}
