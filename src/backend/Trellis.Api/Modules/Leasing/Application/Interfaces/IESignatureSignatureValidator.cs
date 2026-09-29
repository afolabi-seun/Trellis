namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>The e-signature webhook's HMAC check, mirrors IPaystackSignatureValidator. HmacESignatureSignatureValidator is the only implementation — real cryptography against a dev-only secret, even though no real e-signature vendor account is wired up yet.</summary>
public interface IESignatureSignatureValidator
{
    Task<bool> IsValidAsync(string body, string signatureHeader);
}
