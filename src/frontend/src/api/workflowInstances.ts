import { apiFetch } from "./client";
import type { components } from "./schema";

type WorkflowInstanceCreateRequest = components["schemas"]["WorkflowInstanceCreateRequest"];
type WorkflowActionRequest = components["schemas"]["WorkflowActionRequest"];
type WorkflowInstanceResponse = components["schemas"]["WorkflowInstanceResponse"];
type WorkflowInstanceResponsePagedResponse =
  components["schemas"]["WorkflowInstanceResponsePagedResponse"];

export const workflowInstancesApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<WorkflowInstanceResponsePagedResponse>("/api/v1/workflow-instances", {
      query: { cursor, limit },
    }),

  create: (request: WorkflowInstanceCreateRequest) =>
    apiFetch<WorkflowInstanceResponse>("/api/v1/workflow-instances", {
      method: "POST",
      body: request,
    }),

  getById: (workflowInstanceId: string) =>
    apiFetch<WorkflowInstanceResponse>(`/api/v1/workflow-instances/${workflowInstanceId}`),

  approve: (workflowInstanceId: string, request: WorkflowActionRequest) =>
    apiFetch<WorkflowInstanceResponse>(`/api/v1/workflow-instances/${workflowInstanceId}/approve`, {
      method: "POST",
      body: request,
    }),

  reject: (workflowInstanceId: string, request: WorkflowActionRequest) =>
    apiFetch<WorkflowInstanceResponse>(`/api/v1/workflow-instances/${workflowInstanceId}/reject`, {
      method: "POST",
      body: request,
    }),
};
