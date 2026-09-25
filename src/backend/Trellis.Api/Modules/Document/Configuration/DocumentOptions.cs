using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Document.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__Document__*</c> — see AuthOptions's identical doc comment.</summary>
public class DocumentOptions
{
    /// <summary>BDR-DOC-003's lookahead window — a document is alerted once its expires_at falls within this many days.</summary>
    [Range(1, 365)]
    public int ExpiryAlertLookaheadDays { get; init; } = 30;
}
