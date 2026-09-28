using Trellis.Api.Modules.Leasing.Domain.ValueObjects;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>
/// BDR-LSE-005 — leasing_design.md's own interface has only SendForSignatureAsync/GetStatusAsync;
/// DownloadSignedDocumentAsync is a necessary, minimal completion of that underspecified shape —
/// every real e-signature vendor (DocuSign, HelloSign/Dropbox Sign, etc.) exposes exactly this as
/// a "download the completed envelope" call, the only way the signed PDF bytes could ever reach
/// Trellis for BDR-DOC-002's filing step. No named provider — NoOpESignatureProvider is the only
/// implementation this phase.
/// </summary>
public interface IESignatureProvider
{
    Task<string> SendForSignatureAsync(Guid documentId, IEnumerable<Signer> signers);

    Task<SignatureStatus> GetStatusAsync(string envelopeId);

    Task<byte[]> DownloadSignedDocumentAsync(string envelopeId);
}
