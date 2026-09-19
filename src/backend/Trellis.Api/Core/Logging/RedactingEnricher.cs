using Serilog.Core;
using Serilog.Events;

namespace Trellis.Api.Core.Logging;

/// <summary>
/// A blanket, mechanically enforced redaction rule (security_guide.md, "Log PII Redaction",
/// BDR-NFR-011) rather than case-by-case discipline — a developer who logs a request DTO that
/// happens to contain a <c>password</c> field is protected by default. Applied globally in
/// Program.cs, not opt-in per call site.
/// </summary>
public sealed class RedactingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password_hash", "token", "refresh_token", "totp_code", "recovery_code",
        "authorization", "card_number", "national_id",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in RedactedProperties)
        {
            if (logEvent.Properties.ContainsKey(name))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, "***REDACTED***"));
            }
        }
    }
}
