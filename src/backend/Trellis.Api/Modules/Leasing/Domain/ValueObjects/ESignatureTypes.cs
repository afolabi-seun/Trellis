namespace Trellis.Api.Modules.Leasing.Domain.ValueObjects;

/// <summary>Role: "TENANT" | "LANDLORD_REP" — the applicant and the staff member who triggered send-for-signature, respectively.</summary>
public sealed record Signer(string Name, string Email, string Role);

public enum SignatureStatusState
{
    Sent,
    Completed,
    Declined,
    Voided,
}

public sealed record SignatureStatus(string EnvelopeId, SignatureStatusState State, DateTimeOffset? CompletedAt);
