namespace Trellis.Api.Core.Audit;

public enum AuditActorType
{
    System,
    HumanUser,
    VendorUser,
}

/// <summary>
/// One append-only row in <c>trellis_audit.audit_record</c> (data_model.md). Immutable after
/// insert — a database trigger rejects UPDATE/DELETE (added in the owning migration).
/// <see cref="TenantId"/> is nullable: NULL only for platform-level (Super Admin) actions, and
/// the standard <c>tenant_isolation</c> RLS policy already handles that correctly (data_model.md,
/// "Row-Level Security" — a NULL-tenant row is invisible to every tenant, visible only under
/// <c>app.is_system</c>).
/// </summary>
public sealed class AuditRecord
{
    public Guid AuditId { get; private init; }
    public Guid? TenantId { get; private init; }
    public Guid ActorId { get; private init; }
    public AuditActorType ActorType { get; private init; }
    public string AuditType { get; private init; } = null!;
    public string? DetailJson { get; private init; }
    public DateTimeOffset RecordedAt { get; private init; }

    private AuditRecord() { }

    public static AuditRecord Create(
        Guid? tenantId, Guid actorId, AuditActorType actorType, string auditType, string? detailJson = null) =>
        new()
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            ActorId = actorId,
            ActorType = actorType,
            AuditType = auditType,
            DetailJson = detailJson,
            RecordedAt = DateTimeOffset.UtcNow,
        };
}
