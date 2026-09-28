using Trellis.Api.Modules.Leasing.Domain.Entities;

namespace Trellis.Api.Modules.Leasing.Application.Interfaces;

public interface IApplicationEventRepository
{
    Task AddAsync(ApplicationEvent applicationEvent);

    Task<IReadOnlyList<ApplicationEvent>> ListForApplicationAsync(Guid applicationId);

    Task SaveChangesAsync();
}
