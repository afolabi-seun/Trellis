namespace Trellis.Api.Modules.Leasing.Domain.ValueObjects;

/// <summary>IScreeningProvider.RequestScreeningAsync's input — built from a submitted Application's own fields, CountryCode from the owning Tenant (screening-provider routing context, e.g. a South African tenant routes to a TransUnion-shaped implementation, a Kenyan tenant to a Metropol-shaped one, per leasing_design.md — never built this phase, NoOp only).</summary>
public sealed record ApplicantDetails(
    string FullName, string Email, string Phone, DateOnly DateOfBirth, string CurrentAddress,
    string? EmployerName, decimal? MonthlyIncome, string CountryCode);
