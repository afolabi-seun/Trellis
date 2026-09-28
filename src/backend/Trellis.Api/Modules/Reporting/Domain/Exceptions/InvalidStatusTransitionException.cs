namespace Trellis.Api.Modules.Reporting.Domain.Exceptions;

public sealed class InvalidStatusTransitionException(string entityName, string from, string to)
    : Exception($"Cannot transition {entityName} from '{from}' to '{to}'.");
