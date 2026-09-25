using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;

namespace Trellis.Api.Modules.Workflow.Application.Interfaces;

public interface IDepartmentService
{
    Task<ServiceResult<DepartmentResponse>> CreateAsync(DepartmentCreateRequest request);

    Task<ServiceResult<DepartmentResponse>> GetByIdAsync(Guid departmentId);

    Task<ServiceResult<PagedResponse<DepartmentResponse>>> ListAsync(string? cursor, int limit);

    Task<ServiceResult<DepartmentResponse>> UpdateAsync(Guid departmentId, DepartmentUpdateRequest request);

    Task<ServiceResult<DepartmentResponse>> ArchiveAsync(Guid departmentId);
}
