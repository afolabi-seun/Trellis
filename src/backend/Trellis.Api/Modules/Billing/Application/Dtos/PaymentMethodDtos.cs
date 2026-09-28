namespace Trellis.Api.Modules.Billing.Application.Dtos;

public sealed record PaymentMethodResponse(
    Guid PaymentMethodId, string Provider, string? CardLast4, string? CardBrand, bool IsDefault, DateTimeOffset CreatedAt);

public sealed record InitializeCardCaptureResponse(string AuthorizationUrl, string AccessCode, string Reference);

public sealed record ConfirmCardCaptureRequest(string Reference);
