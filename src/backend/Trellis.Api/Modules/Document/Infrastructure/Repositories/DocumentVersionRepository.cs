using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Domain.Entities;
using Trellis.Api.Modules.Document.Infrastructure.Data;

namespace Trellis.Api.Modules.Document.Infrastructure.Repositories;

public sealed class DocumentVersionRepository(DocumentDbContext dbContext) : TenantAwareRepository<DocumentVersion>, IDocumentVersionRepository
{
    public async Task AddAsync(DocumentVersion version) => await dbContext.DocumentVersions.AddAsync(version);

    // Deliberately not Scoped() — called from both a tenant-context AND a vendor-context caller
    // (DocumentService.ToResponseAsync/DownloadAsync, used for both regular and vendor-evidence
    // uploads/downloads); Scoped() reads TenantContext.Current!, which throws for a vendor
    // caller (only VendorContext.Current is set). Every call site already resolves
    // documentVersionId from an already-RLS-filtered Document row's own CurrentVersionId, so the
    // caller's entitlement was already established before reaching here — this lookup relies on
    // document_version's own tenant_or_vendor_isolation policy (layer 1) rather than a redundant
    // app-layer filter this method can't correctly express for both caller shapes at once.
    public Task<DocumentVersion?> GetByIdAsync(Guid documentVersionId) =>
        dbContext.DocumentVersions.FirstOrDefaultAsync(v => v.DocumentVersionId == documentVersionId);

    public Task<int> CountForDocumentAsync(Guid documentId) =>
        Scoped(dbContext.DocumentVersions).CountAsync(v => v.DocumentId == documentId);

    public Task<Guid?> GetUploadedByAsync(Guid documentVersionId) =>
        dbContext.DocumentVersions
            .Where(v => v.DocumentVersionId == documentVersionId)
            .Select(v => (Guid?)v.UploadedBy)
            .FirstOrDefaultAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
