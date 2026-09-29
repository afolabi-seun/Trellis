namespace Trellis.Api.Core.Privacy;

/// <summary>How many of this module's records a POPIA erasure request touched — DataSubjectService's own audit_record detail_json carries these counts, never the subject's identity.</summary>
public sealed record ErasureSummary(string ModuleName, int RecordsAffected);

/// <summary>
/// security_guide.md's "erasure is one in-process operation spanning a few module services, not
/// a SQL DELETE" — each module that holds personal data implements this once, registered into DI
/// alongside its other services (multiple registrations of this same interface are expected and
/// resolved as <c>IEnumerable&lt;IPersonalDataHandler&gt;</c> by Tenant's own orchestrator,
/// DataSubjectService). Both methods run under the caller's own ambient TenantContext.Current —
/// this is always a same-tenant, TENANT_ADMIN-authenticated action, never a cross-tenant system
/// job, so no SystemContext/explicit-tenantId plumbing is needed here (unlike the anonymous
/// share-link/webhook paths elsewhere in this codebase). subjectEmail is the only stable
/// data-subject identifier the schema has across every module (an applicant's, an owner's, or a
/// staff member's own login email/username) — see each implementation's own doc comment for
/// exactly which column it matches against. EraseAsync must be idempotent: erasing an
/// already-erased subject is a no-op (RecordsAffected == 0), since the redacted PII no longer
/// matches subjectEmail.
/// </summary>
public interface IPersonalDataHandler
{
    string ModuleName { get; }

    /// <summary>Null when this module holds nothing for this subject. Whatever is returned is serialized as-is into the export bundle — never log it, never let it appear anywhere but the one export response.</summary>
    Task<object?> ExportAsync(string subjectEmail);

    Task<ErasureSummary> EraseAsync(string subjectEmail);
}
