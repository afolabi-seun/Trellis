import { apiFetch } from "./client";
import type { components } from "./schema";

type LeaseResponse = components["schemas"]["LeaseResponse"];
type LeaseTermRequest = components["schemas"]["LeaseTermRequest"];

/**
 * BDR-LSE-009 — lease records. Core, not an add-on: a tenant with only Leasing or only Rent
 * Collection still has leases. There is no create or end call here on purpose — a lease starts
 * when Leasing executes one or Rent Collection starts a schedule, and ends through those same
 * screens.
 */
export const leasesApi = {
  /** Active fixed-term leases ending within `withinDays`, plus any already past their end date, soonest first. */
  listExpiring: (withinDays = 90) =>
    apiFetch<LeaseResponse[]>("/api/v1/leases/expiring", { query: { withinDays } }),

  /** Active leases that predate term capture — staff work through these to record each real term. */
  listTermNotRecorded: () => apiFetch<LeaseResponse[]>("/api/v1/leases/term-not-recorded"),

  getById: (id: string) => apiFetch<LeaseResponse>(`/api/v1/leases/${id}`),

  recordTerm: (id: string, request: LeaseTermRequest) =>
    apiFetch<LeaseResponse>(`/api/v1/leases/${id}/term`, { method: "PUT", body: request }),
};
