namespace Trellis.Api.Core.Data;

/// <summary>
/// Marks a Domain entity as belonging to exactly one tenant. The one property every
/// <see cref="TenantAwareRepository{T}"/> subclass filters on (multi_tenant_design.md,
/// "Application-layer scoping" — layer 2 of the 3-layer isolation model).
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
