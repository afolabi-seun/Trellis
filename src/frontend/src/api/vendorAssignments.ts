import { apiFetch } from "./client";
import type { components } from "./schema";

type VendorAssignmentCreateRequest = components["schemas"]["VendorAssignmentCreateRequest"];
type VendorAssignmentResponse = components["schemas"]["VendorAssignmentResponse"];
type VendorAssignmentResponsePagedResponse =
  components["schemas"]["VendorAssignmentResponsePagedResponse"];
type VendorAssignmentVendorViewPagedResponse =
  components["schemas"]["VendorAssignmentVendorViewPagedResponse"];
type VendorAssignmentDetailView = components["schemas"]["VendorAssignmentDetailView"];

export const vendorAssignmentsApi = {
  // Tenant-side (TENANT_ADMIN/DEPARTMENT_STAFF) — this tenant's own assignments.
  listForTenant: (cursor?: string, limit = 25) =>
    apiFetch<VendorAssignmentResponsePagedResponse>("/api/v1/vendor-assignments", {
      query: { cursor, limit },
    }),

  create: (request: VendorAssignmentCreateRequest) =>
    apiFetch<VendorAssignmentResponse>("/api/v1/vendor-assignments", {
      method: "POST",
      body: request,
    }),

  // Vendor-side (VENDOR_USER) — assignments across every tenant that has engaged this vendor,
  // keyed by work item id (not the assignment's own PK) to match VendorAssignedToWorkItem's route param.
  listMine: (cursor?: string, limit = 25) =>
    apiFetch<VendorAssignmentVendorViewPagedResponse>("/api/v1/vendor-portal/assignments", {
      query: { cursor, limit },
    }),

  getForVendor: (workItemId: string) =>
    apiFetch<VendorAssignmentDetailView>(`/api/v1/vendor-portal/assignments/${workItemId}`),

  start: (workItemId: string) =>
    apiFetch<VendorAssignmentResponse>(`/api/v1/vendor-portal/assignments/${workItemId}/start`, {
      method: "POST",
    }),

  complete: (workItemId: string) =>
    apiFetch<VendorAssignmentResponse>(`/api/v1/vendor-portal/assignments/${workItemId}/complete`, {
      method: "POST",
    }),
};
