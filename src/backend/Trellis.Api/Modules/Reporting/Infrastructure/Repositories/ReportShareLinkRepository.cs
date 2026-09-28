using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Reporting.Infrastructure.Data;

namespace Trellis.Api.Modules.Reporting.Infrastructure.Repositories;

public sealed class ReportShareLinkRepository(ReportingDbContext dbContext) : TenantAwareRepository<ReportShareLink>, IReportShareLinkRepository
{
    public async Task AddAsync(ReportShareLink link) => await dbContext.ReportShareLinks.AddAsync(link);

    public Task<ReportShareLink?> GetByIdAsync(Guid reportShareLinkId) =>
        Scoped(dbContext.ReportShareLinks).FirstOrDefaultAsync(l => l.ReportShareLinkId == reportShareLinkId);

    public Task<ReportShareLink?> ResolveByTokenHashAsync(string tokenHash) =>
        dbContext.ReportShareLinks.FirstOrDefaultAsync(l => l.TokenHash == tokenHash);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
