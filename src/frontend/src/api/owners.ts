import { apiFetch } from "./client";
import type { components } from "./schema";

type OwnerCreateRequest = components["schemas"]["OwnerCreateRequest"];
type OwnerUpdateRequest = components["schemas"]["OwnerUpdateRequest"];
type OwnerResponse = components["schemas"]["OwnerResponse"];
type OwnerResponsePagedResponse = components["schemas"]["OwnerResponsePagedResponse"];

export const ownersApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<OwnerResponsePagedResponse>("/api/v1/owners", { query: { cursor, limit } }),

  create: (request: OwnerCreateRequest) =>
    apiFetch<OwnerResponse>("/api/v1/owners", { method: "POST", body: request }),

  getById: (ownerId: string) => apiFetch<OwnerResponse>(`/api/v1/owners/${ownerId}`),

  update: (ownerId: string, request: OwnerUpdateRequest) =>
    apiFetch<OwnerResponse>(`/api/v1/owners/${ownerId}`, { method: "PATCH", body: request }),

  archive: (ownerId: string) =>
    apiFetch<OwnerResponse>(`/api/v1/owners/${ownerId}`, { method: "DELETE" }),
};
