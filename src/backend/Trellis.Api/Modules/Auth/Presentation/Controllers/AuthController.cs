using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Application.Dtos;
using Trellis.Api.Modules.Auth.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Presentation.Controllers;

/// <summary>api_standards.md's Authentication endpoints.</summary>
[ApiController]
[Route("api/v1/auth")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class AuthController(IAuthService authService, ISessionService sessionService) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [HttpPost("login")]
    [EnableRateLimiting(AuthModuleExtensions.AuthSensitiveRateLimitPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // The one hand-written exception to the thin-controller one-liner pattern in this
        // codebase (documented on LoginResult itself): the refresh token has to become an
        // httpOnly cookie, never a JSON body field, so this can't just be
        // `(await service.LoginAsync(...)).ToActionResult(HttpContext)`.
        var result = await authService.LoginAsync(request.Username, request.Password);
        if (result.Outcome != ServiceResultOutcome.Success)
        {
            return result.ToActionResult(HttpContext);
        }

        if (result.Data!.RefreshToken is { } refreshToken)
        {
            SetRefreshCookie(refreshToken);
        }

        var publicResult = ServiceResult<object>.Success(new
        {
            mfa_required = result.Data.MfaRequired,
            partial_token = result.Data.PartialToken,
            access_token = result.Data.AccessToken,
            access_token_expires_at = result.Data.AccessTokenExpiresAt,
        });

        return publicResult.ToActionResult(HttpContext);
    }

    [HttpPost("mfa/verify")]
    [EnableRateLimiting(AuthModuleExtensions.AuthSensitiveRateLimitPolicy)]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        var result = await authService.VerifyMfaAsync(request);
        if (result.Outcome == ServiceResultOutcome.Success && result.Data!.RefreshToken is { } refreshToken)
        {
            SetRefreshCookie(refreshToken);
            var publicResult = ServiceResult<object>.Success(new
            {
                access_token = result.Data.AccessToken,
                access_token_expires_at = result.Data.AccessTokenExpiresAt,
            });
            return publicResult.ToActionResult(HttpContext);
        }

        return result.ToActionResult(HttpContext);
    }

    [HttpPost("mfa/enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollMfa() =>
        (await authService.EnrollMfaAsync(CurrentUserId())).ToActionResult(HttpContext);

    [HttpPost("mfa/enroll/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmMfaEnrollment([FromBody] MfaEnrollConfirmRequest request) =>
        (await authService.ConfirmMfaEnrollmentAsync(CurrentUserId(), request.TotpCode)).ToActionResult(HttpContext);

    [HttpPost("mfa/recovery-codes/regenerate")]
    [Authorize]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] RecoveryCodesRegenerateRequest request) =>
        (await authService.RegenerateRecoveryCodesAsync(CurrentUserId(), request.CurrentPassword)).ToActionResult(HttpContext);

    [HttpPost("password/forgot")]
    [EnableRateLimiting(AuthModuleExtensions.AuthSensitiveRateLimitPolicy)]
    public async Task<IActionResult> ForgotPassword([FromBody] PasswordForgotRequest request)
    {
        // Existence-non-disclosure (security_guide.md): always 200, identical body, regardless
        // of whether the account exists — AuthService.RequestPasswordResetAsync itself never
        // returns a distinguishable outcome for that reason.
        await authService.RequestPasswordResetAsync(request.Username, HttpContext.Connection.RemoteIpAddress?.ToString());
        return ServiceResult<object>.Success(new { }).ToActionResult(HttpContext);
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetRequest request) =>
        (await authService.ResetPasswordAsync(request.Token, request.NewPassword)).ToActionResult(HttpContext);

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await sessionService.RevokeAsync(CurrentSessionId());
        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    [HttpPost("logout/all-other-sessions")]
    [Authorize]
    public async Task<IActionResult> LogoutAllOtherSessions()
    {
        await sessionService.RevokeAllExceptAsync(CurrentUserId(), CurrentSessionId());
        return NoContent();
    }

    private void SetRefreshCookie(string refreshToken) =>
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth/refresh",
        });

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue("sub")!);

    private Guid CurrentSessionId() => Guid.Parse(User.FindFirstValue("session_id")!);
}
