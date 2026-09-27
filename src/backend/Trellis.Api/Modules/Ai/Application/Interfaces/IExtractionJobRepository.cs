using Trellis.Api.Modules.Ai.Domain.Entities;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IExtractionJobRepository
{
    Task AddAsync(ExtractionJob job);

    Task<ExtractionJob?> GetByIdAsync(Guid extractionJobId);

    Task<(IReadOnlyList<ExtractionJob> Items, bool HasMore, int TotalCount)> ListAsync(
        (DateTimeOffset CreatedAt, Guid Id)? cursor, int limit);

    string GetRowVersion(ExtractionJob job);

    void SetExpectedRowVersion(ExtractionJob job, string rowVersion);

    Task SaveChangesAsync();
}
