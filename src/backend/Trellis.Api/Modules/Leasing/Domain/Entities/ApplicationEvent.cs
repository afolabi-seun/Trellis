using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum ApplicationEventActorType
{
    System,
    HumanUser,
}

/// <summary>
/// BDR-LSE-007's audit-trail registry — the spec's own leasing_design.md names this table once
/// with zero column definitions; the shape here follows Ai's own OverrideLogEntry precedent, whose
/// doc comment states trellis_audit.audit_record has "zero existing structured-query consumers,"
/// so a genuine multi-event, per-Application table is the right mechanism, not a reuse of
/// audit_record. Every ApplicationService/LeasingWebhookService method that changes Application's
/// state writes one of these, dual-written alongside a normal audit_record entry via the
/// relocated IAuditService — the same dual-write shape ExtractionService already uses for
/// AI_SUGGESTION_OVERRIDDEN.
/// </summary>
public static class ApplicationEventTypes
{
    /// <summary>Written at submit time, not at issuance — no Application row exists yet when the link is issued, and every event needs an ApplicationId. Its detail carries the link's own created-at/expires-at.</summary>
    public const string LinkIssued = "LINK_ISSUED";
    public const string Submitted = "SUBMITTED";
    public const string ScreeningRequested = "SCREENING_REQUESTED";
    public const string ScreeningReceived = "SCREENING_RECEIVED";
    public const string Approved = "APPROVED";
    public const string Declined = "DECLINED";
    public const string SentForSignature = "SENT_FOR_SIGNATURE";
    public const string SignatureCompleted = "SIGNATURE_COMPLETED";
    public const string Leased = "LEASED";
}

public sealed class ApplicationEvent : ITenantOwned
{
    public Guid ApplicationEventId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string EventType { get; private set; } = null!;
    public ApplicationEventActorType ActorType { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? DetailJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ApplicationEvent() { }

    public static ApplicationEvent Create(
        Guid tenantId, Guid applicationId, string eventType, ApplicationEventActorType actorType,
        Guid? actorId, string? detailJson) => new()
        {
            ApplicationEventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            EventType = eventType,
            ActorType = actorType,
            ActorId = actorId,
            DetailJson = detailJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
