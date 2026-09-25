namespace Trellis.Api.Core.Files;

public enum ScanOutcome
{
    Clean,
    Infected,
}

/// <summary>error_handling.md's fail-closed contract: a caller only ever sees Clean or Infected here — a scanner that can't complete (timeout/outage) throws instead, so it's never confused with an actual clean result. See <see cref="IFileScanner"/>'s own doc comment.</summary>
public sealed record ScanResult(ScanOutcome Outcome, string? Detail);

/// <summary>security_guide.md's exact File Upload Security signature — same provider-agnostic seam as IPaymentGateway/IScreeningProvider.</summary>
public interface IFileScanner
{
    Task<ScanResult> ScanAsync(Stream content, string fileName, string declaredContentType);
}
