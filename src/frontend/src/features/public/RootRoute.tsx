import { Navigate } from "react-router-dom";
import { isPlatformHost } from "../../utils/platformHosts";
import { PublicVacancyListingsPage } from "./leasing/PublicVacancyListingsPage";

/**
 * On the platform host `/` is the staff app; on a tenant's own custom domain it is that tenant's
 * public listings (the slug is resolved from the host, see useTenantSlug).
 */
export function RootRoute() {
  return isPlatformHost() ? <Navigate to="/admin" replace /> : <PublicVacancyListingsPage />;
}
