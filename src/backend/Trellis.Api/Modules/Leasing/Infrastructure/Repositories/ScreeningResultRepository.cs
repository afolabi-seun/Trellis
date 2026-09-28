using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class ScreeningResultRepository(LeasingDbContext dbContext) : TenantAwareRepository<ScreeningResult>, IScreeningResultRepository
{
    public async Task AddAsync(ScreeningResult result) => await dbContext.ScreeningResults.AddAsync(result);

    public Task<ScreeningResult?> GetLatestForApplicationAsync(Guid applicationId) =>
        Scoped(dbContext.ScreeningResults)
            .Where(r => r.ApplicationId == applicationId)
            .OrderByDescending(r => r.ReceivedAt)
            .FirstOrDefaultAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
