import { apiFetch, getAccessToken } from "./client";
import type { components } from "./schema";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type DashboardSummaryResponse = components["schemas"]["DashboardSummaryResponse"];
type OwnerReportCreateRequest = components["schemas"]["OwnerReportCreateRequest"];
type OwnerReportResponse = components["schemas"]["OwnerReportResponse"];
type OwnerReportResponsePagedResponse = components["schemas"]["OwnerReportResponsePagedResponse"];
type ReportShareLinkResponse = components["schemas"]["ReportShareLinkResponse"];

async function downloadBlob(path: string): Promise<Blob> {
  const response = await fetch(new URL(path, API_BASE_URL), {
    headers: getAccessToken() ? { Authorization: `Bearer ${getAccessToken()}` } : {},
    credentials: "include",
  });
  if (!response.ok) {
    throw new Error("Download failed.");
  }
  return response.blob();
}

/** BDR-RPT-001 — a tenant-wide operational summary, no dollar figures. Reporting is Core tier, never gated by module entitlement. */
export const reportingApi = {
  getDashboardSummary: () =>
    apiFetch<DashboardSummaryResponse>("/api/v1/reporting/dashboard"),

  downloadDashboardCsv: () => downloadBlob("/api/v1/reporting/dashboard/export.csv"),
};

/** BDR-OWN-002/003/004 — Owner report generation, export, delivery, and share links. TENANT_ADMIN only. */
export const ownerReportsApi = {
  generate: (request: OwnerReportCreateRequest) =>
    apiFetch<OwnerReportResponse>("/api/v1/reporting/owner-reports", {
      method: "POST",
      body: request,
    }),

  list: (ownerId?: string, cursor?: string, limit = 25) =>
    apiFetch<OwnerReportResponsePagedResponse>("/api/v1/reporting/owner-reports", {
      query: { ownerId, cursor, limit },
    }),

  downloadPdf: (ownerReportId: string) =>
    downloadBlob(`/api/v1/reporting/owner-reports/${ownerReportId}/download.pdf`),

  downloadCsv: (ownerReportId: string) =>
    downloadBlob(`/api/v1/reporting/owner-reports/${ownerReportId}/download.csv`),

  deliver: (ownerReportId: string) =>
    apiFetch<boolean>(`/api/v1/reporting/owner-reports/${ownerReportId}/deliver`, {
      method: "POST",
    }),

  /** Returns the raw share token exactly once — never retrievable again after this call. */
  createShareLink: (ownerReportId: string) =>
    apiFetch<ReportShareLinkResponse>(
      `/api/v1/reporting/owner-reports/${ownerReportId}/share-links`,
      { method: "POST" },
    ),

  revokeShareLink: (reportShareLinkId: string) =>
    apiFetch<boolean>(`/api/v1/reporting/share-links/${reportShareLinkId}`, {
      method: "DELETE",
    }),
};
