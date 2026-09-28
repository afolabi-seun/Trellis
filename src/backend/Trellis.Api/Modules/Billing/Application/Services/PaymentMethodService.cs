using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;

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
        // retrieve the resulting authorization_code — a call IPaymentGateway doesn't expose yet
        // since PaystackGateway's real HTTP implementation is deferred this phase (see its own
        // doc comment). The NoOp stub's own reference doubles as the authorization code here;
        // this line is the one spot that changes once a real verify call exists.
        var existing = await repository.ListForTenantAsync(tenantId);
        foreach (var old in existing.Where(m => m.IsDefault))
        {
            old.MarkNotDefault();
        }

        var method = PaymentMethod.Create(tenantId, "PAYSTACK", request.Reference, "4242", "VISA");
        await repository.AddAsync(method);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(tenantId, tenantId, AuditActorType.System, AuditTypes.Billing.PaymentMethodAdded);

        return ServiceResult<PaymentMethodResponse>.Success(ToResponse(method));
    }

    private static PaymentMethodResponse ToResponse(PaymentMethod method) => new(
        method.PaymentMethodId, method.Provider, method.CardLast4, method.CardBrand, method.IsDefault, method.CreatedAt);
}
