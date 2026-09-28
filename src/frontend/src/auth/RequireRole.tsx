import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "./useAuth";
import type { TrellisTokenClaims } from "./tokenClaims";

const roleHome: Record<TrellisTokenClaims["role"], string> = {
  SUPER_ADMIN: "/platform/tenants",
  TENANT_ADMIN: "/admin",
  DEPARTMENT_STAFF: "/admin",
  VENDOR_USER: "/vendor",
};

/**
 * frontend_architecture.md: "UI-layer scoping is UX, not security" — this only decides what
 * renders. The API enforces access independently at every layer described in
 * multi_tenant_design.md; a Vendor manually navigating to an admin URL gets the same
 * 401/403/404 whether or not this guard exists.
 */
export function RequireRole({ allowedRoles }: { allowedRoles: TrellisTokenClaims["role"][] }) {
  const { isBootstrapping, isAuthenticated, claims } = useAuth();
  const location = useLocation();

  if (isBootstrapping) {
    return null;
  }

  if (!isAuthenticated || !claims) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (!allowedRoles.includes(claims.role)) {
    return <Navigate to={roleHome[claims.role]} replace />;
  }

  return <Outlet />;
}
