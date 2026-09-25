using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Domain.Entities;
using Trellis.Api.Modules.Document.Infrastructure.Data;

namespace Trellis.Api.Modules.Document.Infrastructure.Repositories;

public sealed class DocumentVersionRepository(DocumentDbContext dbContext) : TenantAwareRepository<DocumentVersion>, IDocumentVersionRepository
{
    public async Task AddAsync(DocumentVersion version) => await dbContext.DocumentVersions.AddAsync(version);

    public Task<DocumentVersion?> GetByIdAsync(Guid documentVersionId) =>
        Scoped(dbContext.DocumentVersions).FirstOrDefaultAsync(v => v.DocumentVersionId == documentVersionId);

    public Task<int> CountForDocumentAsync(Guid documentId) =>
        Scoped(dbContext.DocumentVersions).CountAsync(v => v.DocumentId == documentId);

    public Task<Guid?> GetUploadedByAsync(Guid documentVersionId) =>
        dbContext.DocumentVersions
            .Where(v => v.DocumentVersionId == documentVersionId)
            .Select(v => (Guid?)v.UploadedBy)
            .FirstOrDefaultAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
