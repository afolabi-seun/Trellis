using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Providers;

/// <summary>Always valid — no real vendor credentials are wired up this phase, so there is no shared secret to verify an HMAC against. A real validator (PaystackSignatureValidator's own shape) resolves its secret via ISecretProvider per call and compares in constant time. Registered in place of the real one at exactly one DI line.</summary>
public sealed class NoOpESignatureSignatureValidator : IESignatureSignatureValidator
{
    public Task<bool> IsValidAsync(string body, string signatureHeader) => Task.FromResult(true);
}
