using System.Text;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Providers;

/// <summary>
/// No real e-signature vendor (DocuSign, Adobe Sign, a regional alternative) is fixed for this
/// phase — same "ship one stub, defer the real one" convention as NoOpPaymentGateway. The
/// completion flow is exercised by POSTing a synthetic envelope.completed payload to the real
/// webhook endpoint; a real implementation resolves its credentials via ISecretProvider per call.
/// </summary>
public sealed class NoOpESignatureProvider : IESignatureProvider
{
    public Task<string> SendForSignatureAsync(Guid documentId, IEnumerable<Signer> signers) =>
        Task.FromResult($"noop-envelope-{Guid.NewGuid():N}");

    public Task<SignatureStatus> GetStatusAsync(string envelopeId) =>
        Task.FromResult(new SignatureStatus(envelopeId, SignatureStatusState.Completed, DateTimeOffset.UtcNow));

    public Task<byte[]> DownloadSignedDocumentAsync(string envelopeId) =>
        Task.FromResult(Encoding.UTF8.GetBytes($"%PDF-1.4 NOOP SIGNED LEASE — envelope {envelopeId}"));
}
