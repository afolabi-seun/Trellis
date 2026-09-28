import { apiFetch } from "./client";
import type { components } from "./schema";

type RentScheduleCreateRequest = components["schemas"]["RentScheduleCreateRequest"];
type RentScheduleUpdateRequest = components["schemas"]["RentScheduleUpdateRequest"];
type RentScheduleResponse = components["schemas"]["RentScheduleResponse"];
type RentScheduleResponsePagedResponse = components["schemas"]["RentScheduleResponsePagedResponse"];
type RentPaymentRecordCreateRequest = components["schemas"]["RentPaymentRecordCreateRequest"];
type RentPaymentRecordResponse = components["schemas"]["RentPaymentRecordResponse"];

/**
 * BDR-RENT-001–003 — rent tracking, staff-facing only (TENANT_ADMIN + DEPARTMENT_STAFF), gated by the
 * RENT_COLLECTION module entitlement. Every payment call records a payment received OUTSIDE the
 * platform; Trellis processes and holds no funds (BDR-RENT-006), which is why nothing here is named
 * or worded as taking a payment.
 */
export const rentSchedulesApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<RentScheduleResponsePagedResponse>("/api/v1/rent-schedules", {
      query: { cursor, limit },
    }),

  listArrears: () => apiFetch<RentScheduleResponse[]>("/api/v1/rent-schedules/arrears"),

  getById: (id: string) => apiFetch<RentScheduleResponse>(`/api/v1/rent-schedules/${id}`),

  create: (request: RentScheduleCreateRequest) =>
    apiFetch<RentScheduleResponse>("/api/v1/rent-schedules", { method: "POST", body: request }),

  update: (id: string, request: RentScheduleUpdateRequest) =>
    apiFetch<RentScheduleResponse>(`/api/v1/rent-schedules/${id}`, {
      method: "PATCH",
      body: request,
    }),

  end: (id: string) =>
    apiFetch<RentScheduleResponse>(`/api/v1/rent-schedules/${id}/end`, { method: "POST" }),

  listPaymentRecords: (id: string) =>
    apiFetch<RentPaymentRecordResponse[]>(`/api/v1/rent-schedules/${id}/payment-records`),

  recordPaymentReceived: (id: string, request: RentPaymentRecordCreateRequest) =>
    apiFetch<RentPaymentRecordResponse>(`/api/v1/rent-schedules/${id}/payment-records`, {
      method: "POST",
      body: request,
    }),

  voidPaymentRecord: (id: string, recordId: string, reason: string) =>
    apiFetch<RentPaymentRecordResponse>(
      `/api/v1/rent-schedules/${id}/payment-records/${recordId}/void`,
      { method: "POST", body: { reason } },
    ),
};
