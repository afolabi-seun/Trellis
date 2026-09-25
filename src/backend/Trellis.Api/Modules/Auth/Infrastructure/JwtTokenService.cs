using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Configuration;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure;

/// <summary>security_guide.md's JWT Structure — RS256, the exact Tenant-side/Vendor claim shapes.</summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private const string JwtSigningKeySecretName = "JwtSigningKeyBase64";
    private const string Issuer = "Trellis";
    private const string Audience = "trellis-platform";

    private readonly RSA _rsa;
    private readonly AuthOptions _options;

    public JwtTokenService(ISecretProvider secretProvider, IOptions<AuthOptions> options)
    {
        _options = options.Value;
        _rsa = RSA.Create();

        // GetAwaiter().GetResult() here is the one exception coding_standards.md's Async
        // Conventions section itself names — a composition-root shim. A constructor can't be
        // async, so importing the signing key at construction (rather than lazily on first use)
        // has no alternative. Safe in practice: EnvironmentSecretProvider never actually awaits
        // I/O, only a future Vault-backed ISecretProvider would, and this runs once per process
        // lifetime, not per request.
        var pkcs8 = Convert.FromBase64String(
            secretProvider.GetSecretAsync(JwtSigningKeySecretName).GetAwaiter().GetResult());
        _rsa.ImportPkcs8PrivateKey(pkcs8, out _);
    }

    public string IssueAccessToken(AppUser user, Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new("sub", user.UserId.ToString()),
            new("role", user.Role.ToString().ToUpperInvariant() switch
            {
                "SUPERADMIN" => "SUPER_ADMIN",
                "TENANTADMIN" => "TENANT_ADMIN",
                "DEPARTMENTSTAFF" => "DEPARTMENT_STAFF",
                var other => other,
            }),
            new("email", user.Username),
            new("session_id", sessionId.ToString()),
        };

        if (user.TenantId is { } tenantId)
        {
            claims.Add(new Claim("tenant_id", tenantId.ToString()));
        }

        // TenantAdminOrDepartmentLead (Workflow's approval gate) reads these straight off the
        // token — security_guide.md's no-per-request-revalidation-against-Auth principle — so a
        // DEPARTMENT_STAFF user's memberships must ride the access token like tenant_id/role do.
        if (user.DepartmentIds is { Length: > 0 } departmentIds)
        {
            claims.AddRange(departmentIds.Select(id => new Claim("department_id", id.ToString())));
        }

        return WriteToken(claims, TimeSpan.FromMinutes(_options.AccessTokenExpiryMinutes));
    }

    public string IssueVendorAccessToken(Guid vendorUserId, Guid vendorId, Guid sessionId)
    {
        List<Claim> claims =
        [
            new Claim("sub", vendorUserId.ToString()),
            new Claim("vendor_id", vendorId.ToString()),
            new Claim("role", "VENDOR_USER"),
            new Claim("session_id", sessionId.ToString()),
        ];

        return WriteToken(claims, TimeSpan.FromMinutes(_options.AccessTokenExpiryMinutes));
    }

    public string IssuePartialToken(Guid userId)
    {
        List<Claim> claims = [new Claim("sub", userId.ToString()), new Claim("partial", "true")];
        return WriteToken(claims, TimeSpan.FromMinutes(_options.MfaPartialTokenExpiryMinutes));
    }

    public Guid? ValidatePartialToken(string partialToken)
    {
        // A JwtSecurityTokenHandler instance defaults MapInboundClaims to true independently of
        // AuthModuleExtensions' JwtBearerOptions setting — same remapping gotcha documented
        // there (found via this exact method: "sub" silently came back null, so every MFA
        // verification failed regardless of TOTP/recovery-code correctness). Off here too, so
        // FindFirst("sub") below actually finds the claim as issued.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = ValidationParameters();

        try
        {
            var principal = handler.ValidateToken(partialToken, parameters, out _);
            if (principal.FindFirst("partial")?.Value != "true")
            {
                return null;
            }

            var sub = principal.FindFirst("sub")?.Value;
            return sub is not null ? Guid.Parse(sub) : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public string GetJwksJson()
    {
        var parameters = _rsa.ExportParameters(false);
        var key = new RsaSecurityKey(parameters) { KeyId = "trellis-1" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;

        return System.Text.Json.JsonSerializer.Serialize(new { keys = new[] { jwk } });
    }

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan expiry)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            Issuer, Audience, claims, expires: DateTime.UtcNow.Add(expiry), signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new RsaSecurityKey(_rsa),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
}
