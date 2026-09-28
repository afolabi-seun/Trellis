namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>The e-signature webhook's HMAC check, mirrors IPaystackSignatureValidator. NoOpESignatureSignatureValidator (always valid) is the only implementation this phase — no real vendor credentials wired up.</summary>
public interface IESignatureSignatureValidator
{
    Task<bool> IsValidAsync(string body, string signatureHeader);
}
