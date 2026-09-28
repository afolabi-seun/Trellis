using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;
using LeasingApplication = Trellis.Api.Modules.Leasing.Domain.Entities.Application;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class ApplicationRepository(LeasingDbContext dbContext) : TenantAwareRepository<LeasingApplication>, IApplicationRepository
{
    public async Task AddAsync(LeasingApplication application) => await dbContext.Applications.AddAsync(application);

    public Task<LeasingApplication?> GetByIdAsync(Guid applicationId) =>
        Scoped(dbContext.Applications).FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

    public async Task<(IReadOnlyList<LeasingApplication> Items, bool HasMore, int TotalCount)> ListByListingAsync(
        Guid vacancyListingId, (DateTimeOffset SubmittedAt, Guid Id)? cursor, int limit)
    {
        var baseQuery = Scoped(dbContext.Applications).Where(a => a.VacancyListingId == vacancyListingId);
        var totalCount = await baseQuery.CountAsync();

        var query = baseQuery;
        if (cursor is { } c)
        {
            query = query.Where(a => a.SubmittedAt > c.SubmittedAt || (a.SubmittedAt == c.SubmittedAt && a.ApplicationId > c.Id));
        }

        var page = await query.OrderBy(a => a.SubmittedAt).ThenBy(a => a.ApplicationId).Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;

        return (page.Take(limit).ToList(), hasMore, totalCount);
    }

    public Task<LeasingApplication?> GetByIdAcrossTenantsAsync(Guid applicationId) =>
        dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

    public Task<LeasingApplication?> GetByEnvelopeIdAcrossTenantsAsync(string envelopeId) =>
        dbContext.Applications.FirstOrDefaultAsync(a => a.EnvelopeId == envelopeId);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
