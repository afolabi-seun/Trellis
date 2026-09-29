using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;

namespace Trellis.Api.Modules.Billing.Application.Services;

public sealed class PaymentMethodService(
    IPaymentMethodRepository repository, IPaymentGateway paymentGateway, IAuditService auditService) : IPaymentMethodService
{
    public async Task<ServiceResult<PaymentMethodResponse>> GetDefaultAsync(Guid tenantId)
    {
        var method = await repository.GetDefaultAsync(tenantId);
        return method is null
            ? ServiceResult<PaymentMethodResponse>.NotFound()
            : ServiceResult<PaymentMethodResponse>.Success(ToResponse(method));
    }

    public async Task<PaymentMethod?> GetDefaultEntityAsync(Guid tenantId) => await repository.GetDefaultAsync(tenantId);

    public async Task<ServiceResult<InitializeCardCaptureResponse>> InitializeCardCaptureAsync(Guid tenantId, string email)
    {
        var result = await paymentGateway.InitializeCardCaptureAsync(tenantId, email);
        return ServiceResult<InitializeCardCaptureResponse>.Success(
            new InitializeCardCaptureResponse(result.AuthorizationUrl, result.AccessCode, result.Reference));
    }

    public async Task<ServiceResult<PaymentMethodResponse>> ConfirmCardCaptureAsync(Guid tenantId, ConfirmCardCaptureRequest request)
    {
        // Paystack's real flow verifies the transaction (GET /transaction/verify/{reference}) to
        // retrieve the resulting authorization_code — NoOpPaymentGateway's own VerifyTransactionAsync
        // mirrors its prior hardcoded "4242"/"VISA" stand-in, so this call is a no-op behavior
        // change for every environment still running NoOp.
        var verification = await paymentGateway.VerifyTransactionAsync(request.Reference);
        if (!verification.Success)
        {
            return ServiceResult<PaymentMethodResponse>.BusinessRuleViolation(ErrorCodeRegistry.Billing.CardVerificationFailed);
        }

        var existing = await repository.ListForTenantAsync(tenantId);
        foreach (var old in existing.Where(m => m.IsDefault))
        {
            old.MarkNotDefault();
        }

        var method = PaymentMethod.Create(tenantId, "PAYSTACK", verification.AuthorizationCode, verification.CardLast4, verification.CardBrand);
        await repository.AddAsync(method);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(tenantId, tenantId, AuditActorType.System, AuditTypes.Billing.PaymentMethodAdded);

        return ServiceResult<PaymentMethodResponse>.Success(ToResponse(method));
    }

    private static PaymentMethodResponse ToResponse(PaymentMethod method) => new(
        method.PaymentMethodId, method.Provider, method.CardLast4, method.CardBrand, method.IsDefault, method.CreatedAt);
}
