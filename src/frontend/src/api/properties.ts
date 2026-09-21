import { apiFetch } from "./client";
import type { components } from "./schema";

type PropertyCreateRequest = components["schemas"]["PropertyCreateRequest"];
type PropertyUpdateRequest = components["schemas"]["PropertyUpdateRequest"];
type PropertyResponse = components["schemas"]["PropertyResponse"];
type PropertyResponsePagedResponse = components["schemas"]["PropertyResponsePagedResponse"];

export const propertiesApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<PropertyResponsePagedResponse>("/api/v1/properties", { query: { cursor, limit } }),

  create: (request: PropertyCreateRequest) =>
    apiFetch<PropertyResponse>("/api/v1/properties", { method: "POST", body: request }),

  getById: (propertyId: string) => apiFetch<PropertyResponse>(`/api/v1/properties/${propertyId}`),

  update: (propertyId: string, request: PropertyUpdateRequest) =>
    apiFetch<PropertyResponse>(`/api/v1/properties/${propertyId}`, {
      method: "PATCH",
      body: request,
    }),

  archive: (propertyId: string) =>
    apiFetch<PropertyResponse>(`/api/v1/properties/${propertyId}`, { method: "DELETE" }),
};
