import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { publicTenantApi } from "../../api/leasing";
import { isPlatformHost } from "../../utils/platformHosts";

export interface PublicTenantContext {
  /** The tenant slug, or null while it is still being resolved (or if it never resolves). */
  slug: string | null;
  tenantName: string | null;
  isResolving: boolean;
  isNotFound: boolean;
  /** Path to the tenant's listings page — `/vacancies/:slug` on the platform host, `/` on the tenant's own domain. */
  listingsPath: string;
  listingPath: (listingId: string) => string;
}

/**
 * The public pages serve two shapes of URL: `/vacancies/:tenantSlug/...` on the platform host, and on
 * a tenant's own verified custom domain plain `/` and `/listings/:id` — there the slug is resolved
 * from the host instead. Either way the pages get the same slug, and never a tenant id.
 */
export function useTenantSlug(): PublicTenantContext {
  const { tenantSlug: routeSlug } = useParams<{ tenantSlug: string }>();
  const onPlatformHost = isPlatformHost();

  const profileQuery = useQuery({
    queryKey: ["public-tenant", routeSlug ?? window.location.hostname],
    queryFn: () =>
      routeSlug
        ? publicTenantApi.getProfile(routeSlug)
        : publicTenantApi.resolveHost(window.location.hostname),
    enabled: Boolean(routeSlug) || !onPlatformHost,
    retry: false,
  });

  const slug = routeSlug ?? profileQuery.data?.slug ?? null;
  const basePath = routeSlug ? `/vacancies/${routeSlug}` : "";

  return {
    slug,
    tenantName: profileQuery.data?.name ?? null,
    isResolving: profileQuery.isPending && profileQuery.fetchStatus !== "idle",
    isNotFound: profileQuery.isError,
    listingsPath: routeSlug ? basePath : "/",
    listingPath: (listingId) => (routeSlug ? `${basePath}/${listingId}` : `/listings/${listingId}`),
  };
}
