using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum ApplicationStatus
{
    Submitted,
    Screening,
    Approved,
    Declined,
    Leased,
    LeaseEnded,
}

/// <summary>
/// BDR-LSE-002 — one prospective tenant's submission against a <see cref="VacancyListing"/>. No
/// `app_user` row exists for the applicant — identity is <see cref="ApplicantEmail"/> plus the
/// <see cref="ApplicationLinkToken"/> that produced it. <see cref="TenantId"/> is denormalized
/// (the spec's own schema omits it) so RLS can filter this table directly, the same convention
/// every other tenant-owned table in this codebase already follows.
/// </summary>
public sealed class Application : ITenantOwned
{
    public Guid ApplicationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VacancyListingId { get; private set; }

    /// <summary>Copied from the resolved ApplicationLinkContext only — never accepted from the submit request body (BDR-LSE-008).</summary>
    public string ApplicantEmail { get; private set; } = null!;
    public string ApplicantFullName { get; private set; } = null!;
    public string ApplicantPhone { get; private set; } = null!;
    public DateOnly ApplicantDateOfBirth { get; private set; }
    public string ApplicantCurrentAddress { get; private set; } = null!;
    public string? ApplicantEmployerName { get; private set; }
    public decimal? ApplicantMonthlyIncome { get; private set; }

    /// <summary>Legally required for a credit/background check — enforced true at the request validator, not merely optional here.</summary>
    public bool ConsentToScreening { get; private set; }

    /// <summary>Set by MarkSentForSignature — the e-signature provider's own envelope id, used by the completion webhook to look this Application back up (the provider's payload carries an envelope id, never a Trellis ApplicationId).</summary>
    public string? EnvelopeId { get; private set; }

    /// <summary>The staff member who triggered send-for-signature — the auto-filed signed-lease Document's own UploadedBy needs a real human actor, never an invented "system user" id.</summary>
    public Guid? SentForSignatureBy { get; private set; }

    /// <summary>The unsigned lease Document staff picked when sending for signature. The executed lease is filed as the next VERSION of this document (BDR-LSE-006: "a versioned document"), so a draft and its signed copy stay one document. Null only for applications sent before this was tracked — the webhook then falls back to filing a new document.</summary>
    public Guid? DraftDocumentId { get; private set; }

    /// <summary>BDR-LSE-009 — the lease term staff set when sending for signature, carried to the lease record once signed. Both null only for applications sent before the term was captured (the lease is then NOT_RECORDED); a start date with no end date means month-to-month.</summary>
    public DateOnly? LeaseStartDate { get; private set; }
    public DateOnly? LeaseEndDate { get; private set; }

    /// <summary>Set the moment the executed lease is filed, and saved before the rest of the completion runs — so a webhook retry after a later failure never files it a second time.</summary>
    public Guid? SignedLeaseVersionId { get; private set; }

    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Set only by MarkLeaseEnded — when a lease that was executed here was later ended. Why it ended (BDR-LSE-011) lives on the lease record in PropertyAsset, not here.</summary>
    public DateOnly? LeaseEndedDate { get; private set; }

    private Application() { }

    public static Application Create(
        Guid tenantId, Guid vacancyListingId, string applicantEmail, string fullName, string phone,
        DateOnly dateOfBirth, string currentAddress, string? employerName, decimal? monthlyIncome, bool consentToScreening) => new()
        {
            ApplicationId = Guid.NewGuid(),
            TenantId = tenantId,
            VacancyListingId = vacancyListingId,
            ApplicantEmail = applicantEmail,
            ApplicantFullName = fullName,
            ApplicantPhone = phone,
            ApplicantDateOfBirth = dateOfBirth,
            ApplicantCurrentAddress = currentAddress,
            ApplicantEmployerName = employerName,
            ApplicantMonthlyIncome = monthlyIncome,
            ConsentToScreening = consentToScreening,
            Status = ApplicationStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Fired inline within ApplicationService.SubmitAsync the moment IScreeningProvider.RequestScreeningAsync is called — BDR-LSE-003's own flow ("triggered from a submitted application"), not a separate staff action.</summary>
    public void BeginScreening()
    {
        if (Status != ApplicationStatus.Submitted)
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), ApplicationStatus.Screening.ToString());
        }

        Status = ApplicationStatus.Screening;
    }

    /// <summary>Screening (Should, BDR-LSE-003) is not a hard prerequisite — staff may approve directly from Submitted if screening was never triggered.</summary>
    public void Approve()
    {
        if (Status is not (ApplicationStatus.Submitted or ApplicationStatus.Screening))
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), ApplicationStatus.Approved.ToString());
        }

        Status = ApplicationStatus.Approved;
    }

    public void Decline()
    {
        if (Status is not (ApplicationStatus.Submitted or ApplicationStatus.Screening or ApplicationStatus.Approved))
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), ApplicationStatus.Declined.ToString());
        }

        Status = ApplicationStatus.Declined;
    }

    /// <summary>Approved only. No ApplicationStatus value changes — the spec's own enum has none for "sent for signature," only the two new fields are set.</summary>
    public void MarkSentForSignature(
        string envelopeId, Guid staffUserId, Guid? draftDocumentId = null, DateOnly? leaseStartDate = null, DateOnly? leaseEndDate = null)
    {
        if (Status != ApplicationStatus.Approved)
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), Status.ToString());
        }

        if (leaseEndDate is not null && (leaseStartDate is null || leaseEndDate <= leaseStartDate))
        {
            throw new ArgumentException("A lease end date needs a start date before it.", nameof(leaseEndDate));
        }

        EnvelopeId = envelopeId;
        SentForSignatureBy = staffUserId;
        DraftDocumentId = draftDocumentId;
        LeaseStartDate = leaseStartDate;
        LeaseEndDate = leaseEndDate;
    }

    public void RecordSignedLeaseFiled(Guid signedLeaseVersionId) => SignedLeaseVersionId = signedLeaseVersionId;

    /// <summary>BDR-LSE-006's completion side effect on the Application record itself — the spec never states this explicitly, but the enum has the value and it's the only sensible terminal state once a lease is executed against this specific application.</summary>
    public void MarkLeased()
    {
        if (Status != ApplicationStatus.Approved)
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), ApplicationStatus.Leased.ToString());
        }

        Status = ApplicationStatus.Leased;
    }

    /// <summary>Closes out the application once its lease (a PropertyAsset record) has ended. Leased only.</summary>
    public void MarkLeaseEnded(DateOnly effectiveDate)
    {
        if (Status != ApplicationStatus.Leased)
        {
            throw new InvalidStatusTransitionException(nameof(Application), Status.ToString(), ApplicationStatus.LeaseEnded.ToString());
        }

        Status = ApplicationStatus.LeaseEnded;
        LeaseEndedDate = effectiveDate;
    }

    /// <summary>
    /// POPIA erasure — several applicant columns are NOT NULL, so tombstoning rather than
    /// nulling; ApplicantEmail's tombstone is keyed off ApplicationId to stay unique the same way
    /// AppUser.RedactForErasure's username tombstone is. application_event and audit_record are
    /// deliberately untouched — both are IDs-only by design (security_guide.md).
    /// </summary>
    public void RedactApplicantPii()
    {
        ApplicantEmail = $"erased-{ApplicationId:N}@erased.invalid";
        ApplicantFullName = "Erased applicant";
        ApplicantPhone = "[erased]";
        ApplicantDateOfBirth = DateOnly.MinValue;
        ApplicantCurrentAddress = "[erased]";
        ApplicantEmployerName = null;
        ApplicantMonthlyIncome = null;
    }
}
