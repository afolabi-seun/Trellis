import { apiFetch } from "./client";
import type { components } from "./schema";

type DataSubjectExportRequest = components["schemas"]["DataSubjectExportRequest"];
type DataSubjectExportResponse = components["schemas"]["DataSubjectExportResponse"];
type EraseDataSubjectRequest = components["schemas"]["EraseDataSubjectRequest"];
type DataSubjectErasureResponse = components["schemas"]["DataSubjectErasureResponse"];

/** security_guide.md's POPIA data-subject export/erasure — TENANT_ADMIN only, the caller's own tenant. Erasure is irreversible. */
export const dataSubjectsApi = {
  export: (request: DataSubjectExportRequest) =>
    apiFetch<DataSubjectExportResponse>("/api/v1/data-subjects/export", {
      method: "POST",
      body: request,
    }),

  erase: (request: EraseDataSubjectRequest) =>
    apiFetch<DataSubjectErasureResponse>("/api/v1/data-subjects/erase", {
      method: "POST",
      body: request,
    }),
};
