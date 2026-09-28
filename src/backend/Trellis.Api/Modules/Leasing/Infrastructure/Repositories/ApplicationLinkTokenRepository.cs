using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class ApplicationLinkTokenRepository(LeasingDbContext dbContext) : TenantAwareRepository<ApplicationLinkToken>, IApplicationLinkTokenRepository
{
    public async Task AddAsync(ApplicationLinkToken token) => await dbContext.ApplicationLinkTokens.AddAsync(token);

    public Task<ApplicationLinkToken?> ResolveByTokenHashAsync(string tokenHash) =>
        dbContext.ApplicationLinkTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
