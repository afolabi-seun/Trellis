import { apiFetch } from "./client";
import type { components } from "./schema";

type ExtractionJobResponse = components["schemas"]["ExtractionJobResponse"];
type ExtractionJobResponsePagedResponse =
  components["schemas"]["ExtractionJobResponsePagedResponse"];
type ExtractionConfirmRequest = components["schemas"]["ExtractionConfirmRequest"];

export const aiExtractionApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<ExtractionJobResponsePagedResponse>("/api/v1/ai/extraction-jobs", {
      query: { cursor, limit },
    }),

  getById: (id: string) => apiFetch<ExtractionJobResponse>(`/api/v1/ai/extraction-jobs/${id}`),

  confirm: (id: string, request: ExtractionConfirmRequest) =>
    apiFetch<ExtractionJobResponse>(`/api/v1/ai/extraction-jobs/${id}/confirm`, {
      method: "PATCH",
      body: request,
    }),
};
