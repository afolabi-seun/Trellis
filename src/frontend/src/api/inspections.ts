import { apiFetch, getAccessToken } from "./client";
import type { components } from "./schema";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type InspectionCreateRequest = components["schemas"]["InspectionCreateRequest"];
type InspectionItemAddRequest = components["schemas"]["InspectionItemAddRequest"];
type InspectionItemUpdateRequest = components["schemas"]["InspectionItemUpdateRequest"];
type InspectionItemResponse = components["schemas"]["InspectionItemResponse"];
type InspectionPhotoResponse = components["schemas"]["InspectionPhotoResponse"];
type InspectionReportResponse = components["schemas"]["InspectionReportResponse"];
type InspectionReportResponsePagedResponse =
  components["schemas"]["InspectionReportResponsePagedResponse"];
type VendorInspectionView = components["schemas"]["VendorInspectionView"];

/** Downloads always bypass apiFetch's JSON-envelope unwrap — same pattern as documents.ts's own download(). */
async function downloadBinary(path: string): Promise<Blob> {
  const response = await fetch(new URL(path, API_BASE_URL), {
    headers: getAccessToken() ? { Authorization: `Bearer ${getAccessToken()}` } : {},
    credentials: "include",
  });
  if (!response.ok) {
    throw new Error("Download failed.");
  }
  return response.blob();
}

export interface InspectionPhotoUploadFields {
  file: File;
  inspectionItemId?: string;
  latitude?: number;
  longitude?: number;
  capturedAt?: string;
}

function toPhotoFormData(fields: InspectionPhotoUploadFields): FormData {
  const form = new FormData();
  form.set("file", fields.file);
  if (fields.inspectionItemId) form.set("inspectionItemId", fields.inspectionItemId);
  if (fields.latitude !== undefined) form.set("latitude", String(fields.latitude));
  if (fields.longitude !== undefined) form.set("longitude", String(fields.longitude));
  if (fields.capturedAt) form.set("capturedAt", fields.capturedAt);
  return form;
}

/** One checklist item's final state, and one uploaded photo's metadata — decision 12's JSON-blob shape, matching InspectionCompleteForm.cs exactly. */
export interface InspectionCompleteItemPayload {
  inspectionItemId: string;
  status: string;
  notes?: string | null;
}

export interface InspectionCompletePhotoPayload {
  file: File;
  fileName: string;
  inspectionItemId?: string;
  latitude?: number;
  longitude?: number;
  capturedAt: string;
}

export interface InspectionCompletePayload {
  items: InspectionCompleteItemPayload[];
  latitude?: number;
  longitude?: number;
  photos: InspectionCompletePhotoPayload[];
}

function toCompleteFormData(payload: InspectionCompletePayload): FormData {
  const form = new FormData();
  form.set(
    "ItemsJson",
    JSON.stringify(
      payload.items.map((i) => ({
        InspectionItemId: i.inspectionItemId,
        Status: i.status,
        Notes: i.notes ?? null,
      })),
    ),
  );
  if (payload.latitude !== undefined && payload.longitude !== undefined) {
    form.set(
      "LocationJson",
      JSON.stringify({ Latitude: payload.latitude, Longitude: payload.longitude }),
    );
  }
  form.set(
    "PhotoMetaJson",
    JSON.stringify(
      payload.photos.map((p) => ({
        FileName: p.fileName,
        InspectionItemId: p.inspectionItemId ?? null,
        Latitude: p.latitude ?? null,
        Longitude: p.longitude ?? null,
        CapturedAt: p.capturedAt,
      })),
    ),
  );
  for (const photo of payload.photos) {
    form.append("Photos", photo.file, photo.fileName);
  }
  return form;
}

export const inspectionsApi = {
  // Staff-side (TENANT_ADMIN/DEPARTMENT_STAFF).
  list: (cursor?: string, limit = 25) =>
    apiFetch<InspectionReportResponsePagedResponse>("/api/v1/inspections", {
      query: { cursor, limit },
    }),

  create: (request: InspectionCreateRequest) =>
    apiFetch<InspectionReportResponse>("/api/v1/inspections", { method: "POST", body: request }),

  getById: (id: string) => apiFetch<InspectionReportResponse>(`/api/v1/inspections/${id}`),

  addItem: (id: string, request: InspectionItemAddRequest) =>
    apiFetch<InspectionItemResponse>(`/api/v1/inspections/${id}/items`, {
      method: "POST",
      body: request,
    }),

  updateItem: (id: string, itemId: string, request: InspectionItemUpdateRequest) =>
    apiFetch<InspectionItemResponse>(`/api/v1/inspections/${id}/items/${itemId}`, {
      method: "PATCH",
      body: request,
    }),

  removeItem: (id: string, itemId: string) =>
    apiFetch<boolean>(`/api/v1/inspections/${id}/items/${itemId}`, { method: "DELETE" }),

  uploadPhoto: (id: string, fields: InspectionPhotoUploadFields) =>
    apiFetch<InspectionPhotoResponse>(`/api/v1/inspections/${id}/photos`, {
      method: "POST",
      body: toPhotoFormData(fields),
    }),

  downloadPhoto: (id: string, photoId: string) =>
    downloadBinary(`/api/v1/inspections/${id}/photos/${photoId}/download`),

  start: (id: string) =>
    apiFetch<InspectionReportResponse>(`/api/v1/inspections/${id}/start`, { method: "POST" }),

  complete: (id: string, latitude?: number, longitude?: number) =>
    apiFetch<InspectionReportResponse>(`/api/v1/inspections/${id}/complete`, {
      method: "POST",
      body: { latitude: latitude ?? null, longitude: longitude ?? null },
    }),

  // Vendor-side (VENDOR_USER) — keyed by workItemId, matching VendorAssignedToWorkItem's route param.
  getForVendor: (workItemId: string) =>
    apiFetch<VendorInspectionView>(`/api/v1/vendor-portal/inspections/${workItemId}`),

  startForVendor: (workItemId: string) =>
    apiFetch<VendorInspectionView>(`/api/v1/vendor-portal/inspections/${workItemId}/start`, {
      method: "POST",
    }),

  downloadPhotoForVendor: (workItemId: string, photoId: string) =>
    downloadBinary(`/api/v1/vendor-portal/inspections/${workItemId}/photos/${photoId}/download`),

  /** The one idempotency-protected route in the whole phase — a retry with the same idempotencyKey returns the cached first result, never a duplicate. */
  completeForVendor: (
    workItemId: string,
    payload: InspectionCompletePayload,
    idempotencyKey: string,
  ) =>
    apiFetch<VendorInspectionView>(`/api/v1/vendor-portal/inspections/${workItemId}/complete`, {
      method: "POST",
      body: toCompleteFormData(payload),
      headers: { "X-Idempotency-Key": idempotencyKey },
    }),
};
