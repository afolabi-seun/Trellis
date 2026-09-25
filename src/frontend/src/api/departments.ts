import { apiFetch } from "./client";
import type { components } from "./schema";

type DepartmentCreateRequest = components["schemas"]["DepartmentCreateRequest"];
type DepartmentUpdateRequest = components["schemas"]["DepartmentUpdateRequest"];
type DepartmentResponse = components["schemas"]["DepartmentResponse"];
type DepartmentResponsePagedResponse = components["schemas"]["DepartmentResponsePagedResponse"];

export const departmentsApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<DepartmentResponsePagedResponse>("/api/v1/departments", { query: { cursor, limit } }),

  create: (request: DepartmentCreateRequest) =>
    apiFetch<DepartmentResponse>("/api/v1/departments", { method: "POST", body: request }),

  getById: (departmentId: string) =>
    apiFetch<DepartmentResponse>(`/api/v1/departments/${departmentId}`),

  update: (departmentId: string, request: DepartmentUpdateRequest) =>
    apiFetch<DepartmentResponse>(`/api/v1/departments/${departmentId}`, {
      method: "PATCH",
      body: request,
    }),

  archive: (departmentId: string) =>
    apiFetch<DepartmentResponse>(`/api/v1/departments/${departmentId}`, { method: "DELETE" }),
};
