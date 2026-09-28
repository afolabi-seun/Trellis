using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;

namespace Trellis.Api.Modules.Billing.Application.Services;

/// <summary>
/// billing_design.md's dunning state machine — the one place Billing reaches into Tenant, through
/// ITenantService.SuspendAsync/ReactivateAsync, never by writing to trellis_tenant.tenant
/// directly. A first charge attempt and a retry are structurally identical (charge, record the
/// PaymentTransaction, decide the next state), so AttemptChargeAsync is the single method both
/// UsageSnapshotBackgroundService's initial attempt and this service's own day-2/day-5 retries
/// go through.
/// </summary>
public sealed class DunningService(
    IInvoiceRepository invoiceRepository,
    ISubscriptionRepository subscriptionRepository,
    IPaymentTransactionRepository transactionRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IPaymentGateway paymentGateway,
    ITenantService tenantService,
    IAuditService auditService) : IDunningService
{
    public async Task<bool> AttemptChargeAsync(Invoice invoice)
    {
        var paymentMethod = await paymentMethodRepository.GetDefaultAsync(invoice.TenantId);
        var reference = $"{invoice.InvoiceId}-{Guid.NewGuid():N}";

        var chargeResult = paymentMethod is null
            ? new ChargeResult(false, reference, "{\"error\":\"no_default_payment_method\"}")
            : await paymentGateway.ChargeAsync(paymentMethod.ProviderAuthorizationCode, invoice.TotalAmount, invoice.Currency, reference);

        var transaction = PaymentTransaction.Create(
            invoice.TenantId, invoice.InvoiceId, chargeResult.ProviderReference, invoice.TotalAmount,
            chargeResult.Success ? PaymentTransactionStatus.Success : PaymentTransactionStatus.Failed, chargeResult.ResponseJson);
        await transactionRepository.AddAsync(transaction);
        await transactionRepository.SaveChangesAsync();

        var subscription = await subscriptionRepository.GetByIdAsync(invoice.SubscriptionId);
        if (subscription is null)
        {
            return chargeResult.Success;
        }

        if (chargeResult.Success)
        {
            invoice.MarkPaid(DateTimeOffset.UtcNow);
            await invoiceRepository.SaveChangesAsync();

            if (subscription.Status != SubscriptionStatus.Active)
            {
                subscription.Activate();
                await subscriptionRepository.SaveChangesAsync();
            }

            await auditService.RecordAsync(invoice.TenantId, invoice.TenantId, AuditActorType.System, AuditTypes.Billing.InvoicePaid);
            return true;
        }

        if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
        {
            // The initial failure — either the subscription's very first-ever charge attempt
            // (still Trialing) or a lapse from a previously-healthy Active state. Either way,
            // this is the first failed attempt against this invoice, so dunning starts now.
            subscription.MarkPastDue();
            await subscriptionRepository.SaveChangesAsync();
            await auditService.RecordAsync(invoice.TenantId, invoice.TenantId, AuditActorType.System, AuditTypes.Billing.SubscriptionPastDue);
        }
        else
        {
            // Not the initial failure — subscription is already PAST_DUE, this is a day-2/day-5
            // retry. Dunning exhausted on the 3rd total failed attempt against this invoice.
            var failedAttempts = (await transactionRepository.ListForInvoiceAsync(invoice.InvoiceId))
                .Count(t => t.Status == PaymentTransactionStatus.Failed);

            if (failedAttempts >= 3)
            {
                invoice.MarkFailed();
                await invoiceRepository.SaveChangesAsync();

                subscription.Suspend();
                await subscriptionRepository.SaveChangesAsync();

                await tenantService.SuspendAsync(invoice.TenantId, "non-payment");
                await auditService.RecordAsync(invoice.TenantId, invoice.TenantId, AuditActorType.System, AuditTypes.Billing.TenantSuspendedNonpayment);
            }
        }

        return false;
    }

    public async Task RetryDueSubscriptionsAsync()
    {
        var pastDue = await subscriptionRepository.ListPastDueAsync();
        var now = DateTimeOffset.UtcNow;

        foreach (var subscription in pastDue)
        {
            var invoice = await invoiceRepository.GetOpenForTenantAsync(subscription.TenantId);
            if (invoice?.DueAt is not { } dueAt)
            {
                continue;
            }

            var failedAttempts = (await transactionRepository.ListForInvoiceAsync(invoice.InvoiceId))
                .Count(t => t.Status == PaymentTransactionStatus.Failed);

            var shouldRetry = failedAttempts switch
            {
                1 => now >= dueAt.AddDays(2),
                2 => now >= dueAt.AddDays(5),
                _ => false,
            };

            if (shouldRetry)
            {
                await AttemptChargeAsync(invoice);
            }
        }
    }

    public async Task ReactivateOnSuccessAsync(Guid subscriptionId, string providerReference)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId);
        if (subscription is null)
        {
            return;
        }

        var invoice = await invoiceRepository.GetOpenForTenantAsync(subscription.TenantId);
        if (invoice is not null)
        {
            invoice.MarkPaid(DateTimeOffset.UtcNow);
            await invoiceRepository.SaveChangesAsync();

            var transaction = PaymentTransaction.Create(
                subscription.TenantId, invoice.InvoiceId, providerReference, invoice.TotalAmount, PaymentTransactionStatus.Success, null);
            await transactionRepository.AddAsync(transaction);
            await transactionRepository.SaveChangesAsync();
        }

        if (subscription.Status != SubscriptionStatus.Active)
        {
            subscription.Activate();
            await subscriptionRepository.SaveChangesAsync();
            await auditService.RecordAsync(subscription.TenantId, subscription.TenantId, AuditActorType.System, AuditTypes.Billing.SubscriptionReactivated);
        }

        // Reverses a non-payment suspension automatically — no manual intervention (billing_design.md).
        await tenantService.ReactivateAsync(subscription.TenantId);
    }
}
