import { apiFetch } from "./client";
import type { components } from "./schema";

type VendorCreateRequest = components["schemas"]["VendorCreateRequest"];
type VendorInviteUserRequest = components["schemas"]["VendorInviteUserRequest"];
type VendorResponse = components["schemas"]["VendorResponse"];
type VendorResponsePagedResponse = components["schemas"]["VendorResponsePagedResponse"];

export const vendorsApi = {
  search: (search?: string, cursor?: string, limit = 25) =>
    apiFetch<VendorResponsePagedResponse>("/api/v1/vendors", { query: { search, cursor, limit } }),

  create: (request: VendorCreateRequest) =>
    apiFetch<VendorResponse>("/api/v1/vendors", { method: "POST", body: request }),

  getById: (vendorId: string) => apiFetch<VendorResponse>(`/api/v1/vendors/${vendorId}`),

  inviteUser: (vendorId: string, request: VendorInviteUserRequest) =>
    apiFetch<VendorResponse>(`/api/v1/vendors/${vendorId}/invite-user`, {
      method: "POST",
      body: request,
    }),
};
