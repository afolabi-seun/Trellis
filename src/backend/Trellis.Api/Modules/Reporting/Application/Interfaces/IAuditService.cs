using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Reporting.Application.Interfaces;

/// <summary>
/// Every module writes audit records through this, in-process, never by inserting into
/// <c>trellis_audit.audit_record</c> directly — the same "own your schema, expose a service"
/// rule every module follows for every other module's data (project_architecture.md). Originally
/// lived in <c>Core/</c> since Reporting wasn't built until much later but every module needed to
/// write an audit record starting Phase 1 — moved here once Reporting (Phase 8) shipped, the same
/// "shared capability now, moved into its own module's phase later without callers changing"
/// pattern the extraction seam already used. Every existing caller's <c>using</c> updated to this
/// namespace; the interface itself, and every call site's usage, is otherwise unchanged.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(
        Guid? tenantId, Guid actorId, AuditActorType actorType, string auditType, string? detailJson = null);
}
