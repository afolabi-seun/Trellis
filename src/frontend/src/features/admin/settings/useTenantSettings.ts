import { useQuery } from "@tanstack/react-query";
import { tenantSettingsApi } from "../../../api/settings";

/** The caller's tenant settings, cached app-wide. Forms use `defaultCurrency` instead of guessing "USD". */
export function useTenantSettings() {
  return useQuery({
    queryKey: ["settings", "tenant"],
    queryFn: tenantSettingsApi.get,
    staleTime: 5 * 60 * 1000,
  });
}
