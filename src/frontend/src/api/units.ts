import { apiFetch } from "./client";
import type { components } from "./schema";

type UnitAssetCreateRequest = components["schemas"]["UnitAssetCreateRequest"];
type UnitAssetUpdateRequest = components["schemas"]["UnitAssetUpdateRequest"];
type UnitAssetResponse = components["schemas"]["UnitAssetResponse"];
type UnitAssetResponsePagedResponse = components["schemas"]["UnitAssetResponsePagedResponse"];

export const unitsApi = {
  listByProperty: (propertyId: string, cursor?: string, limit = 25) =>
    apiFetch<UnitAssetResponsePagedResponse>(`/api/v1/properties/${propertyId}/units`, {
      query: { cursor, limit },
    }),

  create: (propertyId: string, request: UnitAssetCreateRequest) =>
    apiFetch<UnitAssetResponse>(`/api/v1/properties/${propertyId}/units`, {
      method: "POST",
      body: request,
    }),

  getById: (unitAssetId: string) => apiFetch<UnitAssetResponse>(`/api/v1/units/${unitAssetId}`),

  update: (unitAssetId: string, request: UnitAssetUpdateRequest) =>
    apiFetch<UnitAssetResponse>(`/api/v1/units/${unitAssetId}`, { method: "PATCH", body: request }),

  archive: (unitAssetId: string) =>
    apiFetch<UnitAssetResponse>(`/api/v1/units/${unitAssetId}`, { method: "DELETE" }),
};
