namespace Trellis.Api.Modules.Tenant.Domain.Exceptions;

/// <summary>Thrown when code tries to move a Tenant into a status its current status can't reach directly.</summary>
public sealed class InvalidTenantStatusTransitionException(string from, string to)
    : Exception($"Cannot transition tenant from '{from}' to '{to}'.");
