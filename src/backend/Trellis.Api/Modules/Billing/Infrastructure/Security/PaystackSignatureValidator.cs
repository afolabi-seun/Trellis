using System.Security.Cryptography;
using System.Text;
using Trellis.Api.Core.Secrets;

namespace Trellis.Api.Modules.Billing.Infrastructure.Security;

/// <summary>
/// billing_design.md: "the signature check IS its authentication" — HMAC-SHA512 with the Paystack
/// secret key, verified before anything else runs so an unverified payload never reaches business
/// logic. Built for real this phase (unlike PaystackGateway's real HTTP calls) since it's pure
/// cryptography, fully testable against a synthetic payload and a known test secret with no live
/// vendor call involved. Resolves its key per call, not at construction, matching every other
/// per-request secret use in this codebase (security_guide.md).
/// </summary>
public sealed class PaystackSignatureValidator(ISecretProvider secretProvider) : IPaystackSignatureValidator
{
    private const string SecretKeyName = "PaystackSecretKey";

    public async Task<bool> IsValidAsync(string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var secret = await secretProvider.GetSecretAsync(SecretKeyName);
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(signature));
    }
}
