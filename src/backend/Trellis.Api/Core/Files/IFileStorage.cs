namespace Trellis.Api.Core.Files;

/// <summary>
/// Where an uploaded file's bytes actually live — a provider-agnostic seam (security_guide.md's
/// "same provider-agnostic seam as IPaymentGateway, IScreeningProvider" reasoning applied to
/// storage too, which the Handbook doesn't fix a backend for). <see cref="LocalDiskFileStorage"/>
/// is the real, working Phase 3 implementation; swapping to S3/Blob later is one new
/// implementation and one DI registration change, same as every other external seam in this
/// codebase.
/// </summary>
public interface IFileStorage
{
    /// <summary>Persists <paramref name="content"/> under a storage key derived from <paramref name="suggestedKey"/> and returns the key actually used — opaque to the caller, only ever round-tripped back into <see cref="OpenReadAsync"/>.</summary>
    Task<string> SaveAsync(Stream content, string suggestedKey);

    Task<Stream> OpenReadAsync(string storageKey);
}
