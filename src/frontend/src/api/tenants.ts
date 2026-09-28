import { apiFetch } from "./client";
import type { components } from "./schema";

type TenantProvisionRequest = components["schemas"]["TenantProvisionRequest"];
type TenantResponse = components["schemas"]["TenantResponse"];
type TenantResponsePagedResponse = components["schemas"]["TenantResponsePagedResponse"];

/** api_standards.md's "Tenant Management Endpoints (Platform)" — SUPER_ADMIN only. */
export const tenantsApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<TenantResponsePagedResponse>("/platform/tenants", { query: { cursor, limit } }),

  provision: (request: TenantProvisionRequest) =>
    apiFetch<TenantResponse>("/platform/tenants", { method: "POST", body: request }),

  getById: (tenantId: string) => apiFetch<TenantResponse>(`/platform/tenants/${tenantId}`),

  /** Renaming breaks every public link already shared with the old slug. */
  changeSlug: (tenantId: string, slug: string) =>
    apiFetch<TenantResponse>(`/platform/tenants/${tenantId}/slug`, {
      method: "PATCH",
      body: { slug },
    }),

  offboard: (tenantId: string) =>
    apiFetch<TenantResponse>(`/platform/tenants/${tenantId}/offboard`, { method: "POST" }),
};
