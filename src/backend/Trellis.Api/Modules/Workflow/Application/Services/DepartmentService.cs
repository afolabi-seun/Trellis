using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Domain.Entities;
using Trellis.Api.Modules.Workflow.Domain.Exceptions;

namespace Trellis.Api.Modules.Workflow.Application.Services;

public sealed class DepartmentService(IDepartmentRepository repository) : IDepartmentService
{
    public async Task<ServiceResult<DepartmentResponse>> CreateAsync(DepartmentCreateRequest request)
    {
        var department = Department.Create(TenantContext.Current!.TenantId, request.Name);
        await repository.AddAsync(department);
        await repository.SaveChangesAsync();

        return ServiceResult<DepartmentResponse>.Success(ToResponse(department));
    }

    public async Task<ServiceResult<DepartmentResponse>> GetByIdAsync(Guid departmentId)
    {
        var department = await repository.GetByIdAsync(departmentId);
        return department is null
            ? ServiceResult<DepartmentResponse>.NotFound()
            : ServiceResult<DepartmentResponse>.Success(ToResponse(department));
    }

    public async Task<ServiceResult<PagedResponse<DepartmentResponse>>> ListAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].CreatedAt, items[^1].DepartmentId)
            : null;

        return ServiceResult<PagedResponse<DepartmentResponse>>.Success(
            new PagedResponse<DepartmentResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<DepartmentResponse>> UpdateAsync(Guid departmentId, DepartmentUpdateRequest request)
    {
        var department = await repository.GetByIdAsync(departmentId);
        if (department is null)
        {
            return ServiceResult<DepartmentResponse>.NotFound();
        }

        department.Update(request.Name);

        try
        {
            repository.SetExpectedRowVersion(department, request.RowVersion);
            await repository.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or FormatException or OverflowException)
        {
            return ServiceResult<DepartmentResponse>.ConcurrencyConflict();
        }

        return ServiceResult<DepartmentResponse>.Success(ToResponse(department));
    }

    public async Task<ServiceResult<DepartmentResponse>> ArchiveAsync(Guid departmentId)
    {
        var department = await repository.GetByIdAsync(departmentId);
        if (department is null)
        {
            return ServiceResult<DepartmentResponse>.NotFound();
        }

        try
        {
            department.Archive();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<DepartmentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Workflow.InvalidStatusTransition);
        }

        await repository.SaveChangesAsync();

        return ServiceResult<DepartmentResponse>.Success(ToResponse(department));
    }

    private DepartmentResponse ToResponse(Department department) => new(
        department.DepartmentId,
        department.Name,
        department.Status.ToString().ToUpperInvariant(),
        repository.GetRowVersion(department),
        department.CreatedAt);
}
