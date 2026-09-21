import { apiFetch, setAccessToken } from "./client";
import type { components } from "./schema";

type LoginRequest = components["schemas"]["LoginRequest"];
type LoginResponse = components["schemas"]["LoginResponse"];
type MfaVerifyRequest = components["schemas"]["MfaVerifyRequest"];
type TokenResponse = components["schemas"]["TokenResponse"];
type PasswordForgotRequest = components["schemas"]["PasswordForgotRequest"];
type PasswordResetRequest = components["schemas"]["PasswordResetRequest"];

/** api_standards.md's Authentication endpoints — one thin wrapper per route, typed from the generated schema. */
export const authApi = {
  login: (request: LoginRequest) =>
    apiFetch<LoginResponse>("/api/v1/auth/login", {
      method: "POST",
      body: request,
      anonymous: true,
    }),

  verifyMfa: (request: MfaVerifyRequest) =>
    apiFetch<TokenResponse>("/api/v1/auth/mfa/verify", {
      method: "POST",
      body: request,
      anonymous: true,
    }),

  logout: () => apiFetch<void>("/api/v1/auth/logout", { method: "POST" }),

  logoutAllOtherSessions: () =>
    apiFetch<void>("/api/v1/auth/logout/all-other-sessions", { method: "POST" }),

  forgotPassword: (request: PasswordForgotRequest) =>
    apiFetch<unknown>("/api/v1/auth/password/forgot", {
      method: "POST",
      body: request,
      anonymous: true,
    }),

  resetPassword: (request: PasswordResetRequest) =>
    apiFetch<unknown>("/api/v1/auth/password/reset", {
      method: "POST",
      body: request,
      anonymous: true,
    }),
};

/**
 * Called once on app load (`AuthProvider`'s mount effect) — the access token lives only in
 * memory, so a page reload has none, but the httpOnly refresh cookie may still be valid.
 * A failure here just means "not logged in," not an error to surface.
 */
export async function bootstrapSession(): Promise<boolean> {
  try {
    const response = await apiFetch<TokenResponse>("/api/v1/auth/refresh", {
      method: "POST",
      anonymous: true,
    });
    if (response.accessToken) {
      setAccessToken(response.accessToken);
      return true;
    }
    return false;
  } catch {
    return false;
  }
}
