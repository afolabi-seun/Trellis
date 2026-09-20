using Trellis.Api.Core.Data;

namespace Trellis.Api.Core.Audit;

public sealed class AuditService(CoreDbContext dbContext) : IAuditService
{
    public async Task RecordAsync(
        Guid? tenantId, Guid actorId, AuditActorType actorType, string auditType, string? detailJson = null)
    {
        var record = AuditRecord.Create(tenantId, actorId, actorType, auditType, detailJson);
        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync();
    }
}
