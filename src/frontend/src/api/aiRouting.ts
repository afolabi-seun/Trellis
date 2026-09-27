import { apiFetch } from "./client";
import type { components } from "./schema";

type RoutingSuggestionCreateRequest = components["schemas"]["RoutingSuggestionCreateRequest"];
type RoutingSuggestionLinkRequest = components["schemas"]["RoutingSuggestionLinkRequest"];
type RoutingSuggestionResponse = components["schemas"]["RoutingSuggestionResponse"];
type RoutingSuggestionResponsePagedResponse =
  components["schemas"]["RoutingSuggestionResponsePagedResponse"];
type OverrideLogResponsePagedResponse = components["schemas"]["OverrideLogResponsePagedResponse"];

export const aiRoutingApi = {
  create: (request: RoutingSuggestionCreateRequest) =>
    apiFetch<RoutingSuggestionResponse>("/api/v1/ai/routing-suggestions", {
      method: "POST",
      body: request,
    }),

  list: (cursor?: string, limit = 25) =>
    apiFetch<RoutingSuggestionResponsePagedResponse>("/api/v1/ai/routing-suggestions", {
      query: { cursor, limit },
    }),

  getById: (id: string) =>
    apiFetch<RoutingSuggestionResponse>(`/api/v1/ai/routing-suggestions/${id}`),

  link: (id: string, request: RoutingSuggestionLinkRequest) =>
    apiFetch<RoutingSuggestionResponse>(`/api/v1/ai/routing-suggestions/${id}/link`, {
      method: "PATCH",
      body: request,
    }),
};

export const aiOverrideLogApi = {
  list: (cursor?: string, limit = 25) =>
    apiFetch<OverrideLogResponsePagedResponse>("/api/v1/ai/override-log", {
      query: { cursor, limit },
    }),
};
