namespace Trellis.Api.Core.Helpers;

/// <summary>
/// The single source of truth for every <c>TRL-E-{module}-{number}</c> error code
/// (api_standards.md, "Error Code Registry"). A Service never hardcodes an error message
/// directly — it resolves both code and message through here, so the same fault always
/// produces the same code.
///
/// Only the <see cref="Platform"/> bucket is populated in Phase 0 — codes aren't allocated
/// until a module actually needs one (api_standards.md's own stated principle), so the other
/// module buckets (Auth 1000–1999, PropertyAsset 3000–3999, ...) get added as each module is
/// built, not stubbed out empty ahead of time.
/// </summary>
public static class ErrorCodeRegistry
{
    public static class Platform
    {
        /// <summary>TRL-E-PLT-0001 — an unhandled exception caught by ExceptionHandlingMiddleware.</summary>
        public const string UnhandledFault = "TRL-E-PLT-0001";

        /// <summary>
        /// TRL-E-PLT-0404 — the module-agnostic "not found" code <see cref="Results.ServiceResultExtensions"/>
        /// uses for <see cref="Results.ServiceResultOutcome.NotFound"/>. Deliberately not module-specific:
        /// api_standards.md's own reasoning for returning 404 (not 403) out of tenant/vendor scope applies
        /// identically regardless of which module's resource was asked for.
        /// </summary>
        public const string NotFound = "TRL-E-PLT-0404";

        /// <summary>TRL-E-PLT-0400 — request-shape validation failure, before any module's business logic runs.</summary>
        public const string ValidationFailed = "TRL-E-PLT-0400";

        /// <summary>TRL-E-PLT-0409 — a stale RowVersion on update (coding_standards.md, "Optimistic Concurrency").</summary>
        public const string ConcurrencyConflict = "TRL-E-PLT-0409";
    }

    public static class Tenant
    {
        /// <summary>TRL-E-TEN-2000 — ProvisionTenantAsync's cross-module call into Auth failed; the tenant was rolled back, not left half-provisioned.</summary>
        public const string AdminCreationFailed = "TRL-E-TEN-2000";

        /// <summary>TRL-E-TEN-2001 — an offboard/status-transition request the tenant's current status doesn't allow.</summary>
        public const string InvalidStatusTransition = "TRL-E-TEN-2001";
    }
}
