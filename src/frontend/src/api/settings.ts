import { apiFetch } from "./client";
import type { components } from "./schema";

type TenantDomainResponse = components["schemas"]["TenantDomainResponse"];
type TenantSettingsResponse = components["schemas"]["TenantSettingsResponse"];
type NotificationSettingsDto = components["schemas"]["NotificationSettingsDto"];

/** The caller's own tenant's settings. Any staff role may read; only a TENANT_ADMIN may change who is notified. */
export const tenantSettingsApi = {
  get: () => apiFetch<TenantSettingsResponse>("/api/v1/settings"),

  updateNotifications: (settings: NotificationSettingsDto) =>
    apiFetch<TenantSettingsResponse>("/api/v1/settings/notifications", {
      method: "PUT",
      body: settings,
    }),

  /** BDR-LSE-010 — the reasons offered when a renewal is declined. "Other" is always offered too and isn't part of the list. */
  updateRenewalDeclineReasons: (reasons: string[]) =>
    apiFetch<TenantSettingsResponse>("/api/v1/settings/renewal-decline-reasons", {
      method: "PUT",
      body: { reasons },
    }),
};

/** A tenant's own custom domains (ENTERPRISE tier; TENANT_ADMIN only). A domain is unusable until DNS proves control. */
export const tenantDomainsApi = {
  list: () => apiFetch<TenantDomainResponse[]>("/api/v1/settings/domains"),

  add: (domain: string) =>
    apiFetch<TenantDomainResponse>("/api/v1/settings/domains", {
      method: "POST",
      body: { domain },
    }),

  verify: (id: string) =>
    apiFetch<TenantDomainResponse>(`/api/v1/settings/domains/${id}/verify`, { method: "POST" }),

  remove: (id: string) => apiFetch<boolean>(`/api/v1/settings/domains/${id}`, { method: "DELETE" }),
};
