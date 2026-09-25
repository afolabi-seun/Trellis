using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;

namespace Trellis.Api.Core.Audit;

public sealed class AuditService(CoreDbContext dbContext) : IAuditService
{
    public async Task RecordAsync(
        Guid? tenantId, Guid actorId, AuditActorType actorType, string auditType, string? detailJson = null)
    {
        // audit_record's own tenant_isolation policy checks the INSERT's tenant_id against
        // app.current_tenant_id — fine for every tenant-context caller (the common case), but a
        // Vendor-context caller (BDR-VEN-003's evidence upload, assignment start/complete) has no
        // app.current_tenant_id at all, so the write was rejected by RLS outright, found live via
        // Phase 4's own vendor-evidence smoke test. Recording that something happened is a
        // system-level fact, not one that should depend on the caller's ambient RLS context
        // shape — tenantId here is always an explicit, already-trusted parameter (never taken
        // from the caller's own claims), so bypassing RLS for this one write is the same
        // "narrow, server-only operation" SystemContext.Enter()'s own doc comment describes,
        // centralized here once rather than wrapped at every call site (present and future).
        using var systemScope = SystemContext.Enter();

        var record = AuditRecord.Create(tenantId, actorId, actorType, auditType, detailJson);
        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync();
    }
}
