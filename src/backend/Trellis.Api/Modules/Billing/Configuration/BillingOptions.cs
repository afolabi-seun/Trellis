using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Billing.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__Billing__*</c> — see AuthOptions's identical doc comment.</summary>
public class BillingOptions
{
    /// <summary>billing_design.md's Phase 1 usage-based rate — the "R{rate}" in "142 active units × R{rate}", per active unit/asset per billing period.</summary>
    [Range(0, 100000)]
    public decimal PerUnitRate { get; init; } = 5.00m;
}
