using System.Security.Cryptography;
using System.Text;
using Trellis.Api.Core.Secrets;
using Trellis.Api.Modules.Leasing.Application.Interfaces;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Providers;

/// <summary>
/// Real HMAC-SHA256 check for the e-signature provider's completion webhook — hex digest of the
/// raw body, constant-time compare, exact PaystackSignatureValidator precedent. Replaces
/// NoOpESignatureSignatureValidator: no real e-signature vendor account exists yet, but the
/// cryptography itself needs no vendor credentials to be correct, so it's built for real this
/// phase. Fails closed: a missing secret means every signature is invalid, never that every
/// signature is valid, so a misconfigured deployment can't silently reopen the unauthenticated
/// hole this replaces — the opposite of what ISecretProvider's own "throw if unset" would do left
/// uncaught here.
/// </summary>
public sealed class HmacESignatureSignatureValidator(ISecretProvider secretProvider) : IESignatureSignatureValidator
{
    private const string SecretKeyName = "ESignatureWebhookSecret";

    public async Task<bool> IsValidAsync(string body, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        string secret;
        try
        {
            secret = await secretProvider.GetSecretAsync(SecretKeyName);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(signatureHeader));
    }
}
