using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Trellis.Api.Core.Cors;

/// <summary>
/// Static <c>AllowedOrigins</c> (a deployment concern) plus every VERIFIED tenant custom domain
/// (data, so it can't be an env var). Per request: an origin that isn't in the static list is
/// looked up once via <see cref="ICustomDomainOriginProvider"/> (memory-cached there), and only an
/// origin that resolves is echoed back — an unregistered origin never gets credentialed CORS.
/// </summary>
public sealed class TrellisCorsPolicyProvider(IReadOnlyList<string> staticOrigins) : ICorsPolicyProvider
{
    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var builder = new CorsPolicyBuilder()
            .WithOrigins([.. staticOrigins])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0
            && !staticOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            && await context.RequestServices.GetRequiredService<ICustomDomainOriginProvider>().IsAllowedOriginAsync(origin))
        {
            builder.WithOrigins([.. staticOrigins, origin]);
        }

        return builder.Build();
    }
}
