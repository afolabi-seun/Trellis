namespace Trellis.Api.Core.Audit;

/// <summary>
/// Every module writes audit records through this, in-process, never by inserting into
/// <c>trellis_audit.audit_record</c> directly — the same "own your schema, expose a service"
/// rule every module follows for every other module's data (project_architecture.md). Lives in
/// <c>Core/</c> rather than a Reporting module because Reporting isn't built until much later,
/// but every module needs to write an audit record starting Phase 1 (data_model.md's "Row-Level
/// Security" section) — the same "shared capability now, moved into its own module's phase
/// later without callers changing" pattern the extraction seam already uses elsewhere.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(
        Guid? tenantId, Guid actorId, AuditActorType actorType, string auditType, string? detailJson = null);
}
