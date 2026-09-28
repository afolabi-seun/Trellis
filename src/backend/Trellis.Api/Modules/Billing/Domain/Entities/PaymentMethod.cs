using Trellis.Api.Core.Data;

namespace Trellis.Api.Modules.Billing.Domain.Entities;

/// <summary>
/// A tokenised, gateway-issued reference (BDR-BIL-003/005) — ProviderAuthorizationCode is
/// Paystack's reusable authorization_code, never a card number. Full history is kept (a new
/// default doesn't overwrite the old row), matching "single reusable payment method" meaning one
/// active default at a time, not literally one row ever.
/// </summary>
public sealed class PaymentMethod : ITenantOwned
{
    public Guid PaymentMethodId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ProviderAuthorizationCode { get; private set; } = null!;
    public string? CardLast4 { get; private set; }
    public string? CardBrand { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PaymentMethod() { }

    public static PaymentMethod Create(Guid tenantId, string provider, string authorizationCode, string? cardLast4, string? cardBrand) => new()
    {
        PaymentMethodId = Guid.NewGuid(),
        TenantId = tenantId,
        Provider = provider,
        ProviderAuthorizationCode = authorizationCode,
        CardLast4 = cardLast4,
        CardBrand = cardBrand,
        IsDefault = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public void MarkNotDefault() => IsDefault = false;
}
