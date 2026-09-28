using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trellis.Api.Core.Middleware;
using Xunit;

namespace Trellis.Tests.Isolation;

/// <summary>
/// Phase 7's own proof of RLS across trellis_billing — plain tenant_isolation on 7 of the 8
/// tables (no vendor branch: a Vendor has no billing relationship with any tenant), exact
/// AiIsolationTests.cs shape. webhook_event is the one exception, covered separately below — it
/// has no tenant_id at all (see WebhookEvent's own doc comment) and gets a stricter "system_only"
/// policy instead.
/// </summary>
[Collection(PostgresIsolationCollection.Name)]
public sealed class BillingIsolationTests(PostgresIsolationFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private Guid _subscriptionAId;
    private Guid _subscriptionBId;
    private Guid _paymentMethodAId;
    private Guid _paymentMethodBId;
    private Guid _usageSnapshotAId;
    private Guid _usageSnapshotBId;
    private Guid _invoiceAId;
    private Guid _invoiceBId;
    private Guid _lineItemAId;
    private Guid _lineItemBId;
    private Guid _transactionAId;
    private Guid _transactionBId;
    private Guid _entitlementAId;
    private Guid _entitlementBId;
    private Guid _webhookEventId;

    public async Task InitializeAsync()
    {
        _subscriptionAId = Guid.NewGuid();
        _subscriptionBId = Guid.NewGuid();
        _paymentMethodAId = Guid.NewGuid();
        _paymentMethodBId = Guid.NewGuid();
        _usageSnapshotAId = Guid.NewGuid();
        _usageSnapshotBId = Guid.NewGuid();
        _invoiceAId = Guid.NewGuid();
        _invoiceBId = Guid.NewGuid();
        _lineItemAId = Guid.NewGuid();
        _lineItemBId = Guid.NewGuid();
        _transactionAId = Guid.NewGuid();
        _transactionBId = Guid.NewGuid();
        _entitlementAId = Guid.NewGuid();
        _entitlementBId = Guid.NewGuid();
        _webhookEventId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            insert into trellis_billing.subscription (subscription_id, tenant_id, status, currency, billing_cycle_anchor_day, created_at)
            values
                (@subA, @tenantA, 'ACTIVE', 'USD', 1, now()),
                (@subB, @tenantB, 'ACTIVE', 'USD', 1, now());

            insert into trellis_billing.payment_method
                (payment_method_id, tenant_id, provider, provider_authorization_code, card_last4, card_brand, is_default, created_at)
            values
                (@pmA, @tenantA, 'PAYSTACK', 'auth-a', '4242', 'VISA', true, now()),
                (@pmB, @tenantB, 'PAYSTACK', 'auth-b', '4242', 'VISA', true, now());

            insert into trellis_billing.usage_snapshot
                (snapshot_id, tenant_id, billing_period_start, billing_period_end, active_unit_count, captured_at)
            values
                (@usA, @tenantA, '2026-01-01', '2026-01-31', 10, now()),
                (@usB, @tenantB, '2026-01-01', '2026-01-31', 20, now());

            insert into trellis_billing.invoice
                (invoice_id, tenant_id, subscription_id, billing_period_start, billing_period_end, currency,
                 subtotal_amount, tax_amount, total_amount, status, due_at, paid_at, created_at)
            values
                (@invA, @tenantA, @subA, '2026-01-01', '2026-01-31', 'USD', 50, 0, 50, 'OPEN', now(), null, now()),
                (@invB, @tenantB, @subB, '2026-01-01', '2026-01-31', 'USD', 100, 0, 100, 'OPEN', now(), null, now());

            insert into trellis_billing.invoice_line_item (line_item_id, tenant_id, invoice_id, description, quantity, unit_amount, amount)
            values
                (@liA, @tenantA, @invA, '10 active units', 10, 5, 50),
                (@liB, @tenantB, @invB, '20 active units', 20, 5, 100);

            insert into trellis_billing.payment_transaction
                (transaction_id, tenant_id, invoice_id, provider_reference, amount, status, created_at)
            values
                (@txA, @tenantA, @invA, 'ref-a', 50, 'SUCCESS', now()),
                (@txB, @tenantB, @invB, 'ref-b', 100, 'SUCCESS', now());

            insert into trellis_billing.module_entitlement (entitlement_id, tenant_id, module_key, status, monthly_price, enabled_at)
            values
                (@entA, @tenantA, 'INSPECTION', 'ACTIVE', 0, now()),
                (@entB, @tenantB, 'INSPECTION', 'ACTIVE', 0, now());

            insert into trellis_billing.webhook_event (webhook_event_id, provider_event_id, event_type, payload_json, received_at)
            values (@webhook, 'evt-isolation-test', 'charge.success', '{}', now());
            """;
        seed.Parameters.AddWithValue("subA", _subscriptionAId);
        seed.Parameters.AddWithValue("subB", _subscriptionBId);
        seed.Parameters.AddWithValue("pmA", _paymentMethodAId);
        seed.Parameters.AddWithValue("pmB", _paymentMethodBId);
        seed.Parameters.AddWithValue("usA", _usageSnapshotAId);
        seed.Parameters.AddWithValue("usB", _usageSnapshotBId);
        seed.Parameters.AddWithValue("invA", _invoiceAId);
        seed.Parameters.AddWithValue("invB", _invoiceBId);
        seed.Parameters.AddWithValue("liA", _lineItemAId);
        seed.Parameters.AddWithValue("liB", _lineItemBId);
        seed.Parameters.AddWithValue("txA", _transactionAId);
        seed.Parameters.AddWithValue("txB", _transactionBId);
        seed.Parameters.AddWithValue("entA", _entitlementAId);
        seed.Parameters.AddWithValue("entB", _entitlementBId);
        seed.Parameters.AddWithValue("webhook", _webhookEventId);
        seed.Parameters.AddWithValue("tenantA", TenantA);
        seed.Parameters.AddWithValue("tenantB", TenantB);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        TenantContext.Current = null;

        await using var connection = new NpgsqlConnection(fixture.MigrationsConnectionString);
        await connection.OpenAsync();
        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = """
            delete from trellis_billing.payment_transaction where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.invoice_line_item where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.invoice where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.usage_snapshot where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.module_entitlement where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.payment_method where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.subscription where tenant_id in (@tenantA, @tenantB);
            delete from trellis_billing.webhook_event where provider_event_id = 'evt-isolation-test';
            """;
        cleanup.Parameters.AddWithValue("tenantA", TenantA);
        cleanup.Parameters.AddWithValue("tenantB", TenantB);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task Subscription_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.Subscriptions.ToListAsync();

        results.Should().ContainSingle(s => s.SubscriptionId == _subscriptionAId);
    }

    [Fact]
    [Trait("BDR", "BDR-SEC-002")]
    public async Task PaymentMethod_TenantBContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.PaymentMethods.ToListAsync();

        results.Should().ContainSingle(p => p.PaymentMethodId == _paymentMethodBId);
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-001")]
    public async Task UsageSnapshot_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.UsageSnapshots.ToListAsync();

        results.Should().ContainSingle(u => u.SnapshotId == _usageSnapshotAId);
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-002")]
    public async Task Invoice_TenantBContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.Invoices.ToListAsync();

        results.Should().ContainSingle(i => i.InvoiceId == _invoiceBId);
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-002")]
    public async Task InvoiceLineItem_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.InvoiceLineItems.ToListAsync();

        results.Should().ContainSingle(l => l.LineItemId == _lineItemAId);
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-003")]
    public async Task PaymentTransaction_TenantBContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantB);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.PaymentTransactions.ToListAsync();

        results.Should().ContainSingle(t => t.TransactionId == _transactionBId);
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-009")]
    public async Task ModuleEntitlement_TenantAContext_SeesOnlyItsOwnRow()
    {
        TenantContext.Current = new TenantContext(TenantA);

        await using var db = fixture.CreateAppBillingContext();
        var results = await db.ModuleEntitlements.ToListAsync();

        results.Should().ContainSingle(e => e.EntitlementId == _entitlementAId);
    }

    [Fact]
    public async Task List_WithNoContextAtAll_ReturnsNoRowsAcrossEveryTenantOwnedTable()
    {
        await using var db = fixture.CreateAppBillingContext();

        (await db.Subscriptions.ToListAsync()).Should().BeEmpty();
        (await db.PaymentMethods.ToListAsync()).Should().BeEmpty();
        (await db.UsageSnapshots.ToListAsync()).Should().BeEmpty();
        (await db.Invoices.ToListAsync()).Should().BeEmpty();
        (await db.InvoiceLineItems.ToListAsync()).Should().BeEmpty();
        (await db.PaymentTransactions.ToListAsync()).Should().BeEmpty();
        (await db.ModuleEntitlements.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    [Trait("BDR", "BDR-BIL-008")]
    public async Task WebhookEvent_UnderAnyTenantContext_IsInvisible_OnlySystemContextSeesIt()
    {
        TenantContext.Current = new TenantContext(TenantA);
        await using (var tenantScoped = fixture.CreateAppBillingContext())
        {
            (await tenantScoped.WebhookEvents.ToListAsync()).Should().BeEmpty();
        }

        TenantContext.Current = null;
        using (SystemContext.Enter())
        {
            await using var systemScoped = fixture.CreateAppBillingContext();
            var results = await systemScoped.WebhookEvents.ToListAsync();

            results.Should().ContainSingle(w => w.WebhookEventId == _webhookEventId);
        }
    }
}
