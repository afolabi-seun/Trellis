import type { components } from "./schema";

type ApiError = components["schemas"]["ApiError"];

/**
 * api_standards.md's Response Envelope error shape, thrown so TanStack Query's `error` and
 * react-hook-form's submit handlers can both branch on `.code`/`.details` without re-parsing
 * the envelope at every call site.
 */
export class TrellisApiError extends Error {
  readonly code: string;
  readonly details: ApiError["details"];

  constructor(error: ApiError) {
    super(error.message ?? "Request failed.");
    this.name = "TrellisApiError";
    this.code = error.code ?? "TRL-E-PLT-0000";
    this.details = error.details;
  }
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

/** In-memory only (frontend_architecture.md's Authentication Flow) — never localStorage. */
let accessToken: string | null = null;
let refreshInFlight: Promise<boolean> | null = null;
const listeners = new Set<() => void>();

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
  listeners.forEach((listener) => listener());
}

/** For `useSyncExternalStore` — components that need to re-render when auth state changes. */
export function subscribeToAccessToken(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  query?: Record<string, string | number | undefined>;
  /** Skip the Authorization header and the 401-triggered refresh retry — the four anonymous auth endpoints. */
  anonymous?: boolean;
}

/**
 * The one place every request goes through: attaches the bearer token, unwraps
 * `ApiResponse<T>`, and — on a 401 from an authenticated call — attempts exactly one silent
 * refresh before retrying, per frontend_architecture.md's Authentication Flow step 3. Concurrent
 * 401s share a single in-flight refresh instead of each firing their own.
 */
export async function apiFetch<TData>(path: string, options: RequestOptions = {}): Promise<TData> {
  const response = await doFetch(path, options);

  if (response.status === 401 && !options.anonymous) {
    const refreshed = await refreshOnce();
    if (refreshed) {
      const retried = await doFetch(path, options);
      return unwrap<TData>(retried);
    }
    setAccessToken(null);
  }

  return unwrap<TData>(response);
}

async function doFetch(path: string, options: RequestOptions): Promise<Response> {
  const url = new URL(path, API_BASE_URL);
  if (options.query) {
    for (const [key, value] of Object.entries(options.query)) {
      if (value !== undefined) {
        url.searchParams.set(key, String(value));
      }
    }
  }

  const headers: Record<string, string> = {};
  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (!options.anonymous && accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  return fetch(url, {
    method: options.method ?? "GET",
    headers,
    // The refresh_token cookie is httpOnly and scoped to /api/v1/auth/refresh — the browser
    // attaches it automatically; the app never reads or sets it directly (security_guide.md).
    credentials: "include",
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });
}

/** Coalesces concurrent 401s onto a single /refresh call rather than one per failed request. */
function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const response = await doFetch("/api/v1/auth/refresh", { method: "POST", anonymous: true });
      if (!response.ok) {
        return false;
      }
      const body = (await response.json()) as components["schemas"]["TokenResponseApiResponse"];
      if (body.status === "success" && body.data?.accessToken) {
        setAccessToken(body.data.accessToken);
        return true;
      }
      return false;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

async function unwrap<TData>(response: Response): Promise<TData> {
  if (response.status === 204) {
    return undefined as TData;
  }

  const body = (await response.json()) as { status: string; data?: TData; error?: ApiError };
  if (body.status !== "success") {
    throw new TrellisApiError(body.error ?? { code: "TRL-E-PLT-0000", message: "Request failed." });
  }

  return body.data as TData;
}
