namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

/// <summary>Separate from IApplicationService — its only caller is ESignatureWebhookController, running under SystemContext.Enter() with no ambient TenantContext at entry (see ProcessSignatureCompletedAsync's own implementation for the deliberate mid-flow TenantContext.Current set once the owning tenant is resolved).</summary>
public interface ILeasingWebhookService
{
    Task ProcessSignatureCompletedAsync(string envelopeId, string rawPayloadJson);
}
