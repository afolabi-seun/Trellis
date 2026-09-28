using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IScreeningResultRepository
{
    Task AddAsync(ScreeningResult result);

    Task<ScreeningResult?> GetLatestForApplicationAsync(Guid applicationId);

    Task SaveChangesAsync();
}
