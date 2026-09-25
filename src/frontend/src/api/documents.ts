import { apiFetch, getAccessToken } from "./client";
import type { components } from "./schema";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type DocumentUpdateRequest = components["schemas"]["DocumentUpdateRequest"];
type DocumentResponse = components["schemas"]["DocumentResponse"];
type DocumentResponsePagedResponse = components["schemas"]["DocumentResponsePagedResponse"];

export interface DocumentUploadFields {
  documentType: string;
  expiresAt?: string;
  propertyId?: string;
  unitAssetId?: string;
  ownerId?: string;
  workflowInstanceId?: string;
  file: File;
}

function toFormData(fields: DocumentUploadFields): FormData {
  const form = new FormData();
  form.set("documentType", fields.documentType);
  if (fields.expiresAt) form.set("expiresAt", fields.expiresAt);
  if (fields.propertyId) form.set("propertyId", fields.propertyId);
  if (fields.unitAssetId) form.set("unitAssetId", fields.unitAssetId);
  if (fields.ownerId) form.set("ownerId", fields.ownerId);
  if (fields.workflowInstanceId) form.set("workflowInstanceId", fields.workflowInstanceId);
  form.set("file", fields.file);
  return form;
}

export const documentsApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<DocumentResponsePagedResponse>("/api/v1/documents", { query: { cursor, limit } }),

  listByProperty: (propertyId: string, cursor?: string, limit = 25) =>
    apiFetch<DocumentResponsePagedResponse>(`/api/v1/properties/${propertyId}/documents`, {
      query: { cursor, limit },
    }),

  listByUnit: (unitAssetId: string, cursor?: string, limit = 25) =>
    apiFetch<DocumentResponsePagedResponse>(`/api/v1/units/${unitAssetId}/documents`, {
      query: { cursor, limit },
    }),

  listByOwner: (ownerId: string, cursor?: string, limit = 25) =>
    apiFetch<DocumentResponsePagedResponse>(`/api/v1/owners/${ownerId}/documents`, {
      query: { cursor, limit },
    }),

  /** Also the route a Vendor's evidence upload reaches — VENDOR_USER, TENANT_ADMIN, and DEPARTMENT_STAFF are all allowed here (WorkflowInstanceDocumentsController). */
  listByWorkflowInstance: (workflowInstanceId: string, cursor?: string, limit = 25) =>
    apiFetch<DocumentResponsePagedResponse>(
      `/api/v1/workflow-instances/${workflowInstanceId}/documents`,
      {
        query: { cursor, limit },
      },
    ),

  create: (fields: DocumentUploadFields) =>
    apiFetch<DocumentResponse>("/api/v1/documents", { method: "POST", body: toFormData(fields) }),

  createForWorkflowInstance: (workflowInstanceId: string, fields: DocumentUploadFields) =>
    apiFetch<DocumentResponse>(`/api/v1/workflow-instances/${workflowInstanceId}/documents`, {
      method: "POST",
      body: toFormData(fields),
    }),

  getById: (documentId: string) => apiFetch<DocumentResponse>(`/api/v1/documents/${documentId}`),

  update: (documentId: string, request: DocumentUpdateRequest) =>
    apiFetch<DocumentResponse>(`/api/v1/documents/${documentId}`, {
      method: "PATCH",
      body: request,
    }),

  archive: (documentId: string) =>
    apiFetch<DocumentResponse>(`/api/v1/documents/${documentId}`, { method: "DELETE" }),

  /** Binary download — bypasses apiFetch's JSON-envelope unwrap; the page turns the returned blob into a save-as click. */
  async download(documentId: string): Promise<Blob> {
    const response = await fetch(
      new URL(`/api/v1/documents/${documentId}/download`, API_BASE_URL),
      {
        headers: getAccessToken() ? { Authorization: `Bearer ${getAccessToken()}` } : {},
        credentials: "include",
      },
    );
    if (!response.ok) {
      throw new Error("Download failed.");
    }
    return response.blob();
  },
};
