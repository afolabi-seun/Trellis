namespace Trellis.Api.Core.Secrets;

/// <summary>
/// security_guide.md's Secrets Management — the seam that makes swapping in Vault later a
/// provider change, not a rewrite. Every secret (JWT signing key, third-party API keys) is read
/// through this, never a raw <c>Environment.GetEnvironmentVariable</c> call at the point of use.
/// </summary>
public interface ISecretProvider
{
    Task<string> GetSecretAsync(string key);
}
