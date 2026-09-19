namespace Trellis.Api.Core.Middleware;

/// <summary>
/// Standard security response headers on every API response (security_guide.md, BDR-NFR-010).
/// The SPA's own served assets get their equivalent headers from the static-hosting layer's
/// own config, not from here.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'";
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        await next(context);
    }
}
