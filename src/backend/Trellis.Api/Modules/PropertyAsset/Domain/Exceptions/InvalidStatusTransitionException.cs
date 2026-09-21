namespace Trellis.Api.Modules.PropertyAsset.Domain.Exceptions;

/// <summary>
/// Thrown when code tries to move an Owner/Property/UnitAsset into a status its current status
/// can't reach directly — shared across all three entities (unlike Tenant's own
/// InvalidTenantStatusTransitionException) since the transition rule is identical for each:
/// Active -> Archived only, never Archived -> Active in this phase.
/// </summary>
public sealed class InvalidStatusTransitionException(string entityName, string from, string to)
    : Exception($"Cannot transition {entityName} from '{from}' to '{to}'.");
