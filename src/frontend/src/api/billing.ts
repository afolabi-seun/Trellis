import { apiFetch } from "./client";
import type { components } from "./schema";

type SubscriptionSummaryResponse = components["schemas"]["SubscriptionSummaryResponse"];
type SubscriptionResponsePagedResponse = components["schemas"]["SubscriptionResponsePagedResponse"];
type InvoiceResponse = components["schemas"]["InvoiceResponse"];
type InvoiceResponsePagedResponse = components["schemas"]["InvoiceResponsePagedResponse"];
type PaymentMethodResponse = components["schemas"]["PaymentMethodResponse"];
type InitializeCardCaptureResponse = components["schemas"]["InitializeCardCaptureResponse"];
type ConfirmCardCaptureRequest = components["schemas"]["ConfirmCardCaptureRequest"];
type ModuleEntitlementResponse = components["schemas"]["ModuleEntitlementResponse"];
type ModuleEntitlementSetRequest = components["schemas"]["ModuleEntitlementSetRequest"];
type PlatformRevenueSummaryResponse = components["schemas"]["PlatformRevenueSummaryResponse"];

/** BDR-BIL-006 — a Tenant Admin's own view of their tenant's billing state. */
export const billingApi = {
  getSubscription: () => apiFetch<SubscriptionSummaryResponse>("/api/v1/billing/subscription"),

  listInvoices: (cursor?: string, limit = 25) =>
    apiFetch<InvoiceResponsePagedResponse>("/api/v1/billing/invoices", {
      query: { cursor, limit },
    }),

  getInvoice: (invoiceId: string) =>
    apiFetch<InvoiceResponse>(`/api/v1/billing/invoices/${invoiceId}`),

  getPaymentMethod: () => apiFetch<PaymentMethodResponse>("/api/v1/billing/payment-method"),

  initializePaymentMethod: () =>
    apiFetch<InitializeCardCaptureResponse>("/api/v1/billing/payment-method/initialize", {
      method: "POST",
    }),

  confirmPaymentMethod: (request: ConfirmCardCaptureRequest) =>
    apiFetch<PaymentMethodResponse>("/api/v1/billing/payment-method/confirm", {
      method: "POST",
      body: request,
    }),

  listModuleEntitlements: () =>
    apiFetch<ModuleEntitlementResponse[]>("/api/v1/billing/module-entitlements"),
};

/** BDR-BIL-007/009 — a Super Admin's cross-tenant revenue view and entitlement management. */
export const platformBillingApi = {
  getSummary: () => apiFetch<PlatformRevenueSummaryResponse>("/platform/billing/summary"),

  listSubscriptions: (cursor?: string, limit = 25) =>
    apiFetch<SubscriptionResponsePagedResponse>("/platform/billing/subscriptions", {
      query: { cursor, limit },
    }),

  listInvoices: (tenantId?: string, cursor?: string, limit = 25) =>
    apiFetch<InvoiceResponsePagedResponse>("/platform/billing/invoices", {
      query: { tenantId, cursor, limit },
    }),

  listModuleEntitlements: (tenantId: string) =>
    apiFetch<ModuleEntitlementResponse[]>(`/platform/billing/module-entitlements/${tenantId}`),

  setModuleEntitlement: (
    tenantId: string,
    moduleKey: string,
    request: ModuleEntitlementSetRequest,
  ) =>
    apiFetch<ModuleEntitlementResponse>(
      `/platform/billing/module-entitlements/${tenantId}/${moduleKey}`,
      {
        method: "PUT",
        body: request,
      },
    ),
};
