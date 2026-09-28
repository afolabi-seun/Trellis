namespace Trellis.Api.Modules.Leasing.Domain.ValueObjects;

/// <summary>IApplicationLinkService.ResolveAsync's return shape — everything the public application form needs to render, plus the server-trusted ApplicantEmail that SubmitAsync copies onto the new Application (BDR-LSE-008: never re-accepted from the request body).</summary>
public sealed record ApplicationLinkContext(
    Guid TenantId, Guid VacancyListingId, string ApplicantEmail, string UnitLabel, decimal RentAmount, string Currency);
