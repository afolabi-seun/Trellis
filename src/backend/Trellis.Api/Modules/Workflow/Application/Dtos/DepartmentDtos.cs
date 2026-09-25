namespace Trellis.Api.Modules.Workflow.Application.Dtos;

public sealed record DepartmentCreateRequest(string Name);

public sealed record DepartmentUpdateRequest(string Name, string RowVersion);

public sealed record DepartmentResponse(Guid DepartmentId, string Name, string Status, string RowVersion, DateTimeOffset CreatedAt);
