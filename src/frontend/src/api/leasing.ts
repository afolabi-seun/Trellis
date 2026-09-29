import { apiFetch, getAccessToken } from "./client";
import type { components } from "./schema";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

type VacancyListingCreateRequest = components["schemas"]["VacancyListingCreateRequest"];
type VacancyListingResponse = components["schemas"]["VacancyListingResponse"];
type VacancyListingResponsePagedResponse =
  components["schemas"]["VacancyListingResponsePagedResponse"];
type VacancyListingPhotoResponse = components["schemas"]["VacancyListingPhotoResponse"];
type ApplicationResponse = components["schemas"]["ApplicationResponse"];
type ApplicationResponsePagedResponse = components["schemas"]["ApplicationResponsePagedResponse"];
type ApplicationEventResponse = components["schemas"]["ApplicationEventResponse"];
type PublicVacancyListingResponsePagedResponse =
  components["schemas"]["PublicVacancyListingResponsePagedResponse"];
type PublicVacancyListingDetailResponse =
  components["schemas"]["PublicVacancyListingDetailResponse"];
type ApplicationLinkContextResponse = components["schemas"]["ApplicationLinkContextResponse"];
type ApplicationSubmitRequest = components["schemas"]["ApplicationSubmitRequest"];
type ApplicationSubmitResponse = components["schemas"]["ApplicationSubmitResponse"];
type EndLeaseRequest = components["schemas"]["EndLeaseRequest"];
type ViewScreeningReportRequest = components["schemas"]["ViewScreeningReportRequest"];
type ScreeningReportResponse = components["schemas"]["ScreeningReportResponse"];

/** BDR-LSE-001 — staff listing management. TENANT_ADMIN + DEPARTMENT_STAFF, gated by the LEASING module entitlement. */
export const vacancyListingsApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<VacancyListingResponsePagedResponse>("/api/v1/vacancy-listings", {
      query: { cursor, limit },
    }),

  create: (request: VacancyListingCreateRequest) =>
    apiFetch<VacancyListingResponse>("/api/v1/vacancy-listings", {
      method: "POST",
      body: request,
    }),

  getById: (id: string) => apiFetch<VacancyListingResponse>(`/api/v1/vacancy-listings/${id}`),

  withdraw: (id: string) =>
    apiFetch<VacancyListingResponse>(`/api/v1/vacancy-listings/${id}/withdraw`, {
      method: "POST",
    }),

  uploadPhoto: (id: string, file: File) => {
    const form = new FormData();
    form.set("file", file);
    return apiFetch<VacancyListingPhotoResponse>(`/api/v1/vacancy-listings/${id}/photos`, {
      method: "POST",
      body: form,
    });
  },

  /** Binary download — bypasses apiFetch's JSON-envelope unwrap, same as documentsApi.download. */
  async downloadPhoto(id: string, photoId: string): Promise<Blob> {
    const response = await fetch(
      new URL(`/api/v1/vacancy-listings/${id}/photos/${photoId}/download`, API_BASE_URL),
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

/** BDR-LSE-004 — staff review. Only the coarse screening outcome summary ever appears; the full report is never returned by any endpoint. */
export const applicationsApi = {
  listByListing: (vacancyListingId: string, cursor?: string, limit = 25) =>
    apiFetch<ApplicationResponsePagedResponse>("/api/v1/applications", {
      query: { vacancyListingId, cursor, limit },
    }),

  getEvents: (id: string) =>
    apiFetch<ApplicationEventResponse[]>(`/api/v1/applications/${id}/events`),

  approve: (id: string) =>
    apiFetch<ApplicationResponse>(`/api/v1/applications/${id}/approve`, { method: "POST" }),

  decline: (id: string) =>
    apiFetch<ApplicationResponse>(`/api/v1/applications/${id}/decline`, { method: "POST" }),

  sendForSignature: (id: string, draftDocumentId: string) =>
    apiFetch<ApplicationResponse>(`/api/v1/applications/${id}/send-for-signature`, {
      method: "POST",
      body: { draftDocumentId },
    }),

  /** A staff-initiated move-out on a LEASED application — ends its rent schedule (if any) and frees the unit to be re-listed. */
  endLease: (id: string, request: EndLeaseRequest) =>
    apiFetch<ApplicationResponse>(`/api/v1/applications/${id}/end-lease`, {
      method: "POST",
      body: request,
    }),

  /** BDR-LSE-004's one exception — TENANT_ADMIN only, password re-entry required, every view server-audited. */
  viewScreeningReport: (id: string, request: ViewScreeningReportRequest) =>
    apiFetch<ScreeningReportResponse>(`/api/v1/applications/${id}/screening-report`, {
      method: "POST",
      body: request,
    }),
};

/** BDR-LSE-002 — the anonymous surface. Never sends an Authorization header and never triggers the 401-refresh retry. */
export const publicLeasingApi = {
  browse: (tenantSlug: string, cursor?: string, limit = 25) =>
    apiFetch<PublicVacancyListingResponsePagedResponse>(
      `/api/v1/public/tenants/${tenantSlug}/vacancy-listings`,
      { anonymous: true, query: { cursor, limit } },
    ),

  getListing: (tenantSlug: string, listingId: string) =>
    apiFetch<PublicVacancyListingDetailResponse>(
      `/api/v1/public/tenants/${tenantSlug}/vacancy-listings/${listingId}`,
      { anonymous: true },
    ),

  photoUrl: (tenantSlug: string, listingId: string, photoId: string) =>
    new URL(
      `/api/v1/public/tenants/${tenantSlug}/vacancy-listings/${listingId}/photos/${photoId}`,
      API_BASE_URL,
    ).toString(),

  /** The response is deliberately a bare success — the link itself goes only to the applicant's own inbox (BDR-LSE-008). */
  requestApplicationLink: (tenantSlug: string, listingId: string, applicantEmail: string) =>
    apiFetch<boolean>(
      `/api/v1/public/tenants/${tenantSlug}/vacancy-listings/${listingId}/application-link`,
      { anonymous: true, method: "POST", body: { applicantEmail } },
    ),

  resolveLink: (token: string) =>
    apiFetch<ApplicationLinkContextResponse>(`/api/v1/public/application-links/${token}`, {
      anonymous: true,
    }),

  submitApplication: (token: string, request: ApplicationSubmitRequest) =>
    apiFetch<ApplicationSubmitResponse>(`/api/v1/public/application-links/${token}/applications`, {
      anonymous: true,
      method: "POST",
      body: request,
    }),
};

type PublicTenantResponse = components["schemas"]["PublicTenantResponse"];

/** Who is this page for? A tenant's display name, by slug or — on a tenant's own domain — by host. Never returns an id. */
export const publicTenantApi = {
  getProfile: (tenantSlug: string) =>
    apiFetch<PublicTenantResponse>(`/api/v1/public/tenants/${tenantSlug}`, { anonymous: true }),

  resolveHost: (host: string) =>
    apiFetch<PublicTenantResponse>("/api/v1/public/domain-resolution", {
      anonymous: true,
      query: { host },
    }),
};
