namespace Trellis.Api.Core.Files;

/// <summary>
/// No real cloud scanning API is fixed for Phase 3 (a vendor choice, out of scope here) — always
/// returns Clean, same "interface first, provider later" precedent as Notification's own
/// LoggingNotificationProvider. A real <see cref="IFileScanner"/> is a pre-Production
/// requirement, swapping it in is one new implementation and one DI registration change, nothing
/// else.
/// </summary>
public sealed class NoOpFileScanner : IFileScanner
{
    public Task<ScanResult> ScanAsync(Stream content, string fileName, string declaredContentType) =>
        Task.FromResult(new ScanResult(ScanOutcome.Clean, null));
}
