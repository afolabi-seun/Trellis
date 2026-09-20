using Polly;

namespace Trellis.Api.Core.Resilience;

/// <summary>
/// error_handling.md's Resilience for External Dependencies table: 3 attempts, 2s/8s/20s
/// backoff, no circuit breaker yet ("with moderate request volume, a circuit breaker's main
/// benefit isn't yet a real risk"). Named generically rather than the doc's original
/// <c>AiProviderPolicy</c> — Notification is the first module to actually use it, ahead of the
/// Ai module itself, and the doc's own text already calls this "the same policy" shared across
/// external-dependency calls, not something AI-specific.
/// </summary>
public static class ResiliencePolicies
{
    public static readonly IAsyncPolicy ExternalProviderPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(4, attempt) / 2));
}
