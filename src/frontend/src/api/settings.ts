import { apiFetch } from "./client";
import type { components } from "./schema";

type TenantDomainResponse = components["schemas"]["TenantDomainResponse"];

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
