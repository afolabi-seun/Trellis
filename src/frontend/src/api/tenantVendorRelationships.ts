import { apiFetch } from "./client";
import type { components } from "./schema";

type TenantVendorRelationshipResponse = components["schemas"]["TenantVendorRelationshipResponse"];
type TenantVendorRelationshipResponsePagedResponse =
  components["schemas"]["TenantVendorRelationshipResponsePagedResponse"];

export const tenantVendorRelationshipsApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<TenantVendorRelationshipResponsePagedResponse>("/api/v1/tenant-vendor-relationships", {
      query: { cursor, limit },
    }),

  create: (vendorId: string) =>
    apiFetch<TenantVendorRelationshipResponse>("/api/v1/tenant-vendor-relationships", {
      method: "POST",
      query: { vendorId },
    }),

  terminate: (id: string) =>
    apiFetch<TenantVendorRelationshipResponse>(
      `/api/v1/tenant-vendor-relationships/${id}/terminate`,
      {
        method: "POST",
      },
    ),
};
