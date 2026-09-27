using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;

namespace Trellis.Api.Modules.Ai.Application.Interfaces;

public interface IOverrideLogService
{
    Task<ServiceResult<PagedResponse<OverrideLogResponse>>> ListAsync(string? cursor, int limit);
}
