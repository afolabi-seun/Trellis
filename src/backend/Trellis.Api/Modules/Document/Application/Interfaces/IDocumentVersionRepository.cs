using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Application.Interfaces;

public interface IDocumentVersionRepository
{
    Task AddAsync(DocumentVersion version);

    Task<DocumentVersion?> GetByIdAsync(Guid documentVersionId);

    Task<int> CountForDocumentAsync(Guid documentId);

    /// <summary>Runs under SystemContext.Enter() (DocumentExpiryAlertBackgroundService) — queries unscoped, not through TenantAwareRepository.Scoped(), which would throw with no TenantContext.Current set. Same reasoning as IDocumentRepository.GetDueForExpiryAlertAsync.</summary>
    Task<Guid?> GetUploadedByAsync(Guid documentVersionId);

    Task SaveChangesAsync();
}
