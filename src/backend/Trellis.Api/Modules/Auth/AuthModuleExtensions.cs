using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Application.Services;
using Trellis.Api.Modules.Auth.Configuration;
using Trellis.Api.Modules.Auth.Infrastructure;
using Trellis.Api.Modules.Auth.Infrastructure.Data;
using Trellis.Api.Modules.Auth.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Auth;

public static class AuthModuleExtensions
{
    public const string AuthSensitiveRateLimitPolicy = "auth-sensitive";

    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AuthOptions>()
            .Bind(config.GetSection("Modules:Auth"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AuthDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_auth"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();

        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<ITotpService, TotpService>();
        // JwtTokenService imports the RS256 key in its constructor (see that class's own
        // comment on why) — singleton, so that import happens once per process, not per request.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthService, AuthService>();

        AddJwtBearerAuthentication(services);
        AddAuthSensitiveRateLimiting(services, config);

        return services;
    }

    private static void AddJwtBearerAuthentication(IServiceCollection services)
    {
        // The RS256 public key for token *validation* is derived independently from the same
        // secret the signing side reads — ISecretProvider isn't available yet this early in
        // Program.cs's builder pipeline (it's registered by AddTrellisCore, called in the same
        // chain), so this reads the environment variable directly rather than resolving a
        // scoped/singleton service from an incomplete container. The one place in this module
        // that doesn't go through ISecretProvider, and it's read-only, public-key material.
        var privateKeyBase64 = Environment.GetEnvironmentVariable("JwtSigningKeyBase64")
            ?? throw new InvalidOperationException("JwtSigningKeyBase64 is not set.");
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        var publicKey = RSA.Create();
        publicKey.ImportParameters(rsa.ExportParameters(false));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "Trellis",
                    ValidAudience = "trellis-platform",
                    IssuerSigningKey = new RsaSecurityKey(publicKey),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization();
    }

    private static void AddAuthSensitiveRateLimiting(IServiceCollection services, IConfiguration config)
    {
        // security_guide.md's exact policy shape: fixed window, keyed on source IP. Values read
        // directly from config here (not IOptions<AuthOptions>) since AddRateLimiter's callback
        // runs at service-registration time, before the DI container exists to resolve IOptions from.
        var permitLimit = config.GetValue("Modules:Auth:RateLimitPermitLimit", 10);
        var windowMinutes = config.GetValue("Modules:Auth:RateLimitWindowMinutes", 5);

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter(AuthSensitiveRateLimitPolicy, opt =>
            {
                opt.PermitLimit = permitLimit;
                opt.Window = TimeSpan.FromMinutes(windowMinutes);
                opt.QueueLimit = 0;
            });

            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = (windowMinutes * 60).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
        });
    }
}
