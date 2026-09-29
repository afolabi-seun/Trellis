using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Billing.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__Billing__*</c> — see AuthOptions's identical doc comment.</summary>
public class BillingOptions
{
    /// <summary>billing_design.md's Phase 1 usage-based rate — the "R{rate}" in "142 active units × R{rate}", per active unit/asset per billing period.</summary>
    [Range(0, 100000)]
    public decimal PerUnitRate { get; init; } = 5.00m;

    /// <summary>
    /// Selects IPaymentGateway's registration (BillingModuleExtensions). "NoOp" (default) — no
    /// real Paystack test-mode credentials exist yet, so this stays the default in every
    /// environment until a real Paystack account exists. "Paystack" switches to the real HTTP
    /// integration, unverified against a live sandbox.
    /// </summary>
    [RegularExpression("^(NoOp|Paystack)$")]
    public string PaymentGateway { get; init; } = "NoOp";
}
