using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Domain.Exceptions;

namespace Trellis.Api.Modules.Leasing.Domain.Entities;

public enum ApplicationLinkTokenStatus
{
    Active,
    Expired,
    Consumed,
}

/// <summary>
/// BDR-LSE-002/008 — the secure application-intake link mechanism. Mirrors ReportShareLink's own
/// token mechanics with a genuinely different lifecycle: this token exists BEFORE any
/// <see cref="Application"/> row does, and "single-use" (the spec's own word) means consumed at
/// SUBMIT time, not at mere resolve/GET — an applicant reloading the form mid-completion before
/// final submit must still work. Resolving this token IS BDR-LSE-008's email-control verification;
/// no separate step exists.
/// </summary>
public sealed class ApplicationLinkToken : ITenantOwned
{
    public Guid ApplicationLinkTokenId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VacancyListingId { get; private set; }
    public string ApplicantEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public ApplicationLinkTokenStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? ConsumedApplicationId { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ApplicationLinkToken() { }

    public static ApplicationLinkToken Create(
        Guid tenantId, Guid vacancyListingId, string applicantEmail, string tokenHash, DateTimeOffset expiresAt) => new()
        {
            ApplicationLinkTokenId = Guid.NewGuid(),
            TenantId = tenantId,
            VacancyListingId = vacancyListingId,
            ApplicantEmail = applicantEmail,
            TokenHash = tokenHash,
            Status = ApplicationLinkTokenStatus.Active,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public void Consume(Guid applicationId)
    {
        if (!IsUsable())
        {
            throw new InvalidStatusTransitionException(nameof(ApplicationLinkToken), Status.ToString(), ApplicationLinkTokenStatus.Consumed.ToString());
        }

        Status = ApplicationLinkTokenStatus.Consumed;
        ConsumedApplicationId = applicationId;
        ConsumedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Time-based expiry is inferred here, exactly like ReportShareLink.IsUsable() — Status never flips to Expired by any code path this phase; that enum value exists for schema completeness / a future housekeeping job only.</summary>
    public bool IsUsable() => Status == ApplicationLinkTokenStatus.Active && ExpiresAt > DateTimeOffset.UtcNow;
}
