namespace Trellis.Api.Core.Secrets;

/// <summary>
/// One implementation for every environment (security_guide.md's Phase 1 revision) — the same
/// environment-variable mechanism whether it was set by <c>DotNetEnv</c> from a local
/// <c>.env</c> file or injected directly by the hosting platform's dashboard.
/// </summary>
public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public Task<string> GetSecretAsync(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required secret '{key}' is not set.");
        }

        return Task.FromResult(value);
    }
}
