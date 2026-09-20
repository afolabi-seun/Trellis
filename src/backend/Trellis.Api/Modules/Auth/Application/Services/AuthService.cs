using System.Security.Cryptography;
using System.Text;
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

    public async Task<ServiceResult<LoginResult>> LoginAsync(string username, string password)
    {
        // Resolving a username to its owning tenant is the one thing app_user's RLS can't gate
        // on tenant_id, since the tenant isn't known until this lookup returns it — see
        // SystemContext.Enter()'s doc comment and multi_tenant_design.md.
        AppUser? user;
        using (SystemContext.Enter())
        {
            user = await userRepository.GetByUsernameAsync(username);
        }

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
        // this lookup runs before tenant context can exist — same reasoning as LoginAsync above.
        AppUser? user;
        using (SystemContext.Enter())
        {
            user = await userRepository.GetByIdAsync(userId.Value);
        }

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
        // Same reasoning as LoginAsync: resolving username → tenant is the lookup that
        // establishes tenant context, so it can't be gated by tenant context itself.
        AppUser? user;
        using (SystemContext.Enter())
        {
            user = await userRepository.GetByUsernameAsync(username);
        }

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
        await userRepository.SaveChangesAsync();
        await resetTokenRepository.SaveChangesAsync();

        // security_guide.md's Session Revocation & Logout: a password change revokes every
        // active session for the user, through the same path logout uses.
        await sessionService.RevokeAllExceptAsync(user.UserId, exceptSessionId: null);

        await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.PasswordReset);

        return ServiceResult<object>.Success(new { });
    }

    private async Task<ServiceResult<LoginResult>> IssueSessionAsync(AppUser user)
    {
        var session = Session.Create(user.UserId, TimeSpan.FromHours(_options.RefreshTokenExpiryHours));
        await sessionRepository.AddAsync(session);
        await sessionRepository.SaveChangesAsync();

        var accessToken = jwtTokenService.IssueAccessToken(user, session.SessionId);
        var refreshToken = GenerateOpaqueToken();

        await auditService.RecordAsync(user.TenantId, user.UserId, AuditActorType.HumanUser, AuditTypes.Auth.AuthSuccess);

        return ServiceResult<LoginResult>.Success(new LoginResult(
            false, null, accessToken, DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes), refreshToken));
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
