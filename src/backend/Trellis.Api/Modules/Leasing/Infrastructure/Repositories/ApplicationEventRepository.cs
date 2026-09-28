using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class ApplicationEventRepository(LeasingDbContext dbContext) : TenantAwareRepository<ApplicationEvent>, IApplicationEventRepository
{
    public async Task AddAsync(ApplicationEvent applicationEvent) => await dbContext.ApplicationEvents.AddAsync(applicationEvent);

    public async Task<IReadOnlyList<ApplicationEvent>> ListForApplicationAsync(Guid applicationId) =>
        await Scoped(dbContext.ApplicationEvents)
            .Where(e => e.ApplicationId == applicationId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
