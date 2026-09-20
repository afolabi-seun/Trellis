namespace Trellis.Api.Modules.Auth.Application.Dtos;

/// <summary>The minimal shape another module needs back after creating an app_user — never the full entity.</summary>
public sealed record AppUserSummary(Guid UserId, string Email, Guid? TenantId);
