import { apiFetch } from "./client";
import type { components } from "./schema";

type WorkflowTemplateCreateRequest = components["schemas"]["WorkflowTemplateCreateRequest"];
type WorkflowTemplateUpdateRequest = components["schemas"]["WorkflowTemplateUpdateRequest"];
type WorkflowTemplateResponse = components["schemas"]["WorkflowTemplateResponse"];
type WorkflowTemplateResponsePagedResponse =
  components["schemas"]["WorkflowTemplateResponsePagedResponse"];

export const workflowTemplatesApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<WorkflowTemplateResponsePagedResponse>("/api/v1/workflow-templates", {
      query: { cursor, limit },
    }),

  create: (request: WorkflowTemplateCreateRequest) =>
    apiFetch<WorkflowTemplateResponse>("/api/v1/workflow-templates", {
      method: "POST",
      body: request,
    }),

  getById: (workflowTemplateId: string) =>
    apiFetch<WorkflowTemplateResponse>(`/api/v1/workflow-templates/${workflowTemplateId}`),

  update: (workflowTemplateId: string, request: WorkflowTemplateUpdateRequest) =>
    apiFetch<WorkflowTemplateResponse>(`/api/v1/workflow-templates/${workflowTemplateId}`, {
      method: "PATCH",
      body: request,
    }),

  archive: (workflowTemplateId: string) =>
    apiFetch<WorkflowTemplateResponse>(`/api/v1/workflow-templates/${workflowTemplateId}`, {
      method: "DELETE",
    }),
};
