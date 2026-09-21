// api_standards.md's JWT Structure — Tenant-side and Vendor payload shapes. Decoded client-side
// for display/routing only, never verified here: the signature check happens on every request,
// server-side (security_guide.md); a tampered claim just gets the caller a 401/403 from the API.
export interface TrellisTokenClaims {
  sub: string;
  role: "SUPER_ADMIN" | "TENANT_ADMIN" | "DEPARTMENT_STAFF" | "VENDOR_USER";
  email: string;
  sessionId: string;
  tenantId?: string;
  vendorId?: string;
  exp: number;
}

export function decodeAccessToken(token: string): TrellisTokenClaims | null {
  try {
    const [, payload] = token.split(".");
    const json = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
    return {
      sub: json.sub,
      role: json.role,
      email: json.email,
      sessionId: json.session_id,
      tenantId: json.tenant_id,
      vendorId: json.vendor_id,
      exp: json.exp,
    };
  } catch {
    return null;
  }
}
