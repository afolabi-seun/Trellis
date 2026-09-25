using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Configuration;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Configuration;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Notification.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Application.Services;

public sealed class AuthService(
    IAppUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordResetTokenRepository resetTokenRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITotpService totpService,
    INotificationService notificationService,
    IAuditService auditService,
    ISessionService sessionService,
    IOptions<AuthOptions> options,
    IOptions<AppOptions> appOptions) : IAuthService
{
    private readonly AuthOptions _options = options.Value;
    private readonly AppOptions _appOptions = appOptions.Value;

    public async Task<ServiceResult<AppUserSummary>> CreateTenantAdminAsync(Guid tenantId, string adminEmail)
    {
        var user = AppUser.Create(tenantId, adminEmail, UserRole.TenantAdmin);
        await userRepository.AddAsync(user);
        await userRepository.SaveChangesAsync();

        // No separate "invite" flow is specified anywhere in the handbook — reusing the password
        // reset mechanism for initial activation rather than emailing a temporary password
        // keeps a single, already-audited path for "how does a Trellis user ever set a
        // password," instead of a second, parallel one.
        await IssuePasswordSetLinkAsync(user, requestedIp: null);

        return ServiceResult<AppUserSummary>.Success(new AppUserSummary(user.UserId, user.Username, user.TenantId));
    }

    public async Task<ServiceResult<AppUserSummary>> CreateVendorUserAsync(Guid vendorId, string email)
    {
        // trellis_auth.app_user's plain tenant_isolation policy evaluates
        // `tenant_id::text = current_tenant_id` — for a row with tenant_id IS NULL that's SQL
        // NULL (never true), regardless of the inserting caller's own tenant context. A Vendor's
        // app_user row would be rejected by RLS on INSERT from an ordinary Tenant Admin request
        // without this — same category of gap as LoginAsync's own identity-resolution bypass,
        // for a different reason (inserting a tenant-less row from inside a tenant-scoped
        // request, not resolving identity before a tenant is known).
        using var systemScope = SystemContext.Enter();

        var user = AppUser.CreateVendorUser(vendorId, email);
        await userRepository.AddAsync(user);
        await userRepository.SaveChangesAsync();

        await IssuePasswordSetLinkAsync(user, requestedIp: null);

        return ServiceResult<AppUserSummary>.Success(new AppUserSummary(user.UserId, user.Username, user.TenantId));
    }

    public async Task<ServiceResult<LoginResult>> LoginAsync(string username, string password)
    {
        // The whole flow — not just the username lookup — runs before any tenant context can
        // exist: the lockout counter update, the AUTH_SUCCESS/AUTH_LOCKOUT audit write, and
        // IssueSessionAsync's own writes all hit tables with the standard tenant_isolation RLS
        // policy, and none of them have a tenant claim to set TenantContext from yet (that's the
        // whole point of login). See SystemContext.Enter()'s doc comment and
        // multi_tenant_design.md's "Identity Resolution Before Tenant Is Known" — scoped to the
        // method, not just the lookup, since every write below has the identical problem.
        using var systemScope = SystemContext.Enter();

        var user = await userRepository.GetByUsernameAsync(username);
        if (user is null || user.PasswordHash is null)
        {
            // Same generic failure whether the account exists or not — no separate
            // "account not found" branch, matching password-reset's existence-non-disclosure
            // reasoning (security_guide.md) even though login itself doesn't state that rule
            // explicitly the same way.
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.InvalidCredentials);
        }

        if (user.IsLockedOut())
        {
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.AccountLockedOut);
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            user.RegisterFailedLogin(
                _options.FailedLoginLockoutThreshold, TimeSpan.FromMinutes(_options.FailedLoginLockoutDurationMinutes));
            await userRepository.SaveChangesAsync();

            if (user.IsLockedOut())
            {
                await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.AuthLockout);
            }

            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.InvalidCredentials);
        }

        user.RegisterSuccessfulLogin();
        await userRepository.SaveChangesAsync();

        if (user.RequiresMfa() && user.HasMfaEnrolled())
        {
            var partialToken = jwtTokenService.IssuePartialToken(user.UserId);
            return ServiceResult<LoginResult>.Success(new LoginResult(true, partialToken, null, null, null));
        }

        return await IssueSessionAsync(user);
    }

    public async Task<ServiceResult<LoginResult>> VerifyMfaAsync(MfaVerifyRequest request)
    {
        var userId = jwtTokenService.ValidatePartialToken(request.PartialToken);
        if (userId is null)
        {
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.MfaVerificationFailed);
        }

        // The partial token carries no tenant claim (issued before MFA confirms identity), so
        // the whole method runs before tenant context can exist — same reasoning as LoginAsync.
        using var systemScope = SystemContext.Enter();

        var user = await userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.MfaVerificationFailed);
        }

        if (!string.IsNullOrEmpty(request.TotpCode))
        {
            if (user.TotpSecretEncrypted is null)
            {
                return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.MfaVerificationFailed);
            }

            var secret = await totpService.DecryptAsync(user.TotpSecretEncrypted);
            if (!totpService.ValidateCode(secret, request.TotpCode))
            {
                return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.MfaVerificationFailed);
            }

            return await IssueSessionAsync(user);
        }

        if (!string.IsNullOrEmpty(request.RecoveryCode))
        {
            var codeHash = HashRecoveryCode(request.RecoveryCode);
            var recoveryCode = await recoveryCodeRepository.GetByHashAsync(user.UserId, codeHash);
            if (recoveryCode is null || !recoveryCode.IsUsable())
            {
                return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.MfaVerificationFailed);
            }

            recoveryCode.MarkUsed();
            await recoveryCodeRepository.SaveChangesAsync();

            await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.MfaRecoveryCodeUsed);
            await notificationService.SendAsync(
                user.TenantId, "MFA_RECOVERY_CODE_USED", user.Username, new { });

            return await IssueSessionAsync(user);
        }

        return ServiceResult<LoginResult>.ValidationFailed(
            [new ValidationDetail("totp_code|recovery_code", "One of totp_code or recovery_code is required.")]);
    }

    public async Task<ServiceResult<MfaEnrollResponse>> EnrollMfaAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<MfaEnrollResponse>.NotFound();
        }

        var (secretBase32, otpAuthUri) = totpService.GenerateSecret(user.Username);
        user.EnrollMfa(await totpService.EncryptAsync(secretBase32));
        await userRepository.SaveChangesAsync();

        return ServiceResult<MfaEnrollResponse>.Success(new MfaEnrollResponse(secretBase32, otpAuthUri));
    }

    public async Task<ServiceResult<MfaEnrollConfirmResponse>> ConfirmMfaEnrollmentAsync(Guid userId, string totpCode)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null || user.TotpSecretEncrypted is null)
        {
            return ServiceResult<MfaEnrollConfirmResponse>.NotFound();
        }

        var secret = await totpService.DecryptAsync(user.TotpSecretEncrypted);
        if (!totpService.ValidateCode(secret, totpCode))
        {
            return ServiceResult<MfaEnrollConfirmResponse>.BusinessRuleViolation(ErrorCodeRegistry.Auth.MfaEnrollmentConfirmationFailed);
        }

        var codes = await IssueRecoveryCodesAsync(user.UserId);
        return ServiceResult<MfaEnrollConfirmResponse>.Success(new MfaEnrollConfirmResponse(codes));
    }

    public async Task<ServiceResult<RecoveryCodesRegenerateResponse>> RegenerateRecoveryCodesAsync(Guid userId, string currentPassword)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return ServiceResult<RecoveryCodesRegenerateResponse>.Forbidden(ErrorCodeRegistry.Auth.ReauthenticationFailed);
        }

        await recoveryCodeRepository.DeleteAllForUserAsync(user.UserId);
        var codes = await IssueRecoveryCodesAsync(user.UserId);

        await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.MfaRecoveryCodesRegenerated);
        await notificationService.SendAsync(user.TenantId, "MFA_RECOVERY_CODES_REGENERATED", user.Username, new { });

        return ServiceResult<RecoveryCodesRegenerateResponse>.Success(new RecoveryCodesRegenerateResponse(codes));
    }

    public async Task RequestPasswordResetAsync(string username, string? requestedIp)
    {
        // Same reasoning as LoginAsync: the whole flow, including IssuePasswordSetLinkAsync's
        // write to password_reset_token and its notification_delivery insert, runs before any
        // tenant context can exist.
        using var systemScope = SystemContext.Enter();

        var user = await userRepository.GetByUsernameAsync(username);
        if (user is null)
        {
            // Existence-non-disclosure (security_guide.md): identical outcome, no exception,
            // whether or not the account exists — the caller (Controller) always returns the
            // same generic 200 regardless of what happens here.
            return;
        }

        await IssuePasswordSetLinkAsync(user, requestedIp);
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(string token, string newPassword)
    {
        // Same reasoning as LoginAsync: presenting a reset token is itself anonymous, so there is
        // no tenant context yet for the app_user lookup/update or the PASSWORD_RESET audit write
        // below. (This one previously had no SystemContext.Enter() at all — every real reset
        // would have failed RLS's app_user SELECT and returned a false PasswordResetTokenInvalid.)
        using var systemScope = SystemContext.Enter();

        var tokenHash = HashToken(token);
        var resetToken = await resetTokenRepository.GetByHashAsync(tokenHash);
        if (resetToken is null || !resetToken.IsUsable())
        {
            return ServiceResult<object>.BusinessRuleViolation(ErrorCodeRegistry.Auth.PasswordResetTokenInvalid);
        }

        var user = await userRepository.GetByIdAsync(resetToken.UserId);
        if (user is null)
        {
            return ServiceResult<object>.BusinessRuleViolation(ErrorCodeRegistry.Auth.PasswordResetTokenInvalid);
        }

        user.SetPasswordHash(passwordHasher.Hash(newPassword));
        resetToken.MarkUsed();

        try
        {
            await userRepository.SaveChangesAsync();
            await resetTokenRepository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // BDR-NFR-006 (coding_standards.md): the user's row changed between load and save
            // here (e.g. a concurrent login updating failed_login_count). Reject rather than
            // silently overwrite whatever else just happened to this account.
            return ServiceResult<object>.ConcurrencyConflict();
        }

        // security_guide.md's Session Revocation & Logout: a password change revokes every
        // active session for the user, through the same path logout uses.
        await sessionService.RevokeAllExceptAsync(user.UserId, exceptSessionId: null);

        await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.PasswordReset);

        return ServiceResult<object>.Success(new { });
    }

    public async Task<IReadOnlyList<AppUserSummary>> GetUsersByDepartmentAsync(Guid tenantId, Guid departmentId)
    {
        var users = await userRepository.GetByDepartmentAsync(tenantId, departmentId);
        return users.Select(u => new AppUserSummary(u.UserId, u.Username, u.TenantId)).ToList();
    }

    public async Task<AppUserSummary?> GetUserByIdAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        return user is null ? null : new AppUserSummary(user.UserId, user.Username, user.TenantId);
    }

    /// <summary>Only branch at token issuance (security_guide.md's Vendor Authentication) — a Vendor's token carries vendor_id instead of tenant_id, since its access spans tenants rather than belonging to one.</summary>
    private string IssueAccessTokenForUser(AppUser user, Guid sessionId) =>
        user.Role == UserRole.VendorUser
            ? jwtTokenService.IssueVendorAccessToken(user.UserId, user.VendorId!.Value, sessionId)
            : jwtTokenService.IssueAccessToken(user, sessionId);

    private async Task<ServiceResult<LoginResult>> IssueSessionAsync(AppUser user)
    {
        var refreshToken = GenerateOpaqueToken();
        var session = Session.Create(user.UserId, HashToken(refreshToken), TimeSpan.FromHours(_options.RefreshTokenExpiryHours));
        await sessionRepository.AddAsync(session);
        await sessionRepository.SaveChangesAsync();

        var accessToken = IssueAccessTokenForUser(user, session.SessionId);

        await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.AuthSuccess);

        return ServiceResult<LoginResult>.Success(new LoginResult(
            false, null, accessToken, DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes), refreshToken));
    }

    /// <summary>
    /// <c>POST /api/v1/auth/refresh</c> (security_guide.md's Authentication Architecture — the
    /// refresh half of the flow that turned out not to be fully specified: this is where that
    /// gap was closed). No rotation: the presented cookie value is hashed and matched against
    /// the session it was issued with, and — if that session is still active — a fresh access
    /// token is issued against the same session_id. The session's own expiry
    /// (<see cref="AuthOptions.RefreshTokenExpiryHours"/>) is the outer bound; nothing here
    /// extends it.
    /// </summary>
    public async Task<ServiceResult<LoginResult>> RefreshAsync(string presentedRefreshToken)
    {
        var session = await sessionRepository.GetByRefreshTokenHashAsync(HashToken(presentedRefreshToken));
        if (session is null || !session.IsActive())
        {
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.RefreshTokenInvalid);
        }

        // The presented cookie carries no tenant claim — same reasoning as LoginAsync/
        // VerifyMfaAsync/RequestPasswordResetAsync above.
        AppUser? user;
        using (SystemContext.Enter())
        {
            user = await userRepository.GetByIdAsync(session.UserId);
        }

        if (user is null)
        {
            return ServiceResult<LoginResult>.Forbidden(ErrorCodeRegistry.Auth.RefreshTokenInvalid);
        }

        var accessToken = IssueAccessTokenForUser(user, session.SessionId);

        return ServiceResult<LoginResult>.Success(new LoginResult(
            false, null, accessToken, DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes), null));
    }

    private async Task IssuePasswordSetLinkAsync(AppUser user, string? requestedIp)
    {
        var rawToken = GenerateOpaqueToken();
        var token = PasswordResetToken.Create(
            user.UserId, HashToken(rawToken), TimeSpan.FromMinutes(_options.PasswordResetTokenExpiryMinutes), requestedIp);

        await resetTokenRepository.AddAsync(token);
        await resetTokenRepository.SaveChangesAsync();

        // The raw token is never logged or stored — only its hash. It exists only in this
        // in-memory string and the outbound notification payload.
        await notificationService.SendAsync(
            user.TenantId, "PASSWORD_RESET", user.Username, new { resetLink = $"{_appOptions.BaseUrl}/reset-password?token={rawToken}" });
    }

    private async Task<IReadOnlyList<string>> IssueRecoveryCodesAsync(Guid userId)
    {
        var codes = Enumerable.Range(0, _options.MfaRecoveryCodeCount)
            .Select(_ => GenerateRecoveryCode())
            .ToList();

        var entities = codes.Select(c => MfaRecoveryCode.Create(userId, HashRecoveryCode(c)));
        await recoveryCodeRepository.AddRangeAsync(entities);
        await recoveryCodeRepository.SaveChangesAsync();

        return codes;
    }

    private static string GenerateRecoveryCode()
    {
        // XXXX-XXXX-XXXX in base32 (security_guide.md) — 10 random bytes, base32-encoded, split into 3 groups.
        var bytes = RandomNumberGenerator.GetBytes(10);
        var base32 = Base32(bytes).PadRight(15, '2')[..12];
        return $"{base32[..4]}-{base32[4..8]}-{base32[8..12]}";
    }

    private static string Base32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        var bits = 0;
        var value = 0;

        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(alphabet[(value << (5 - bits)) & 0x1F]);
        }

        return result.ToString();
    }

    private static string GenerateOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string HashRecoveryCode(string code) => HashToken(code);
}
