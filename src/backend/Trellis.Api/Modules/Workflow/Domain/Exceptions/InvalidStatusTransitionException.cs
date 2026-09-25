namespace Trellis.Api.Modules.Workflow.Domain.Exceptions;

/// <summary>Shared across every Workflow-module entity with a status transition — same shape as PropertyAsset's own InvalidStatusTransitionException.</summary>
public sealed class InvalidStatusTransitionException(string entityName, string from, string to)
    : Exception($"Cannot transition {entityName} from '{from}' to '{to}'.");
