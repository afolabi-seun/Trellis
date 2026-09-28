const configured = import.meta.env.VITE_PLATFORM_HOSTS ?? "localhost,127.0.0.1";

const platformHosts = configured
  .split(",")
  .map((h) => h.trim().toLowerCase())
  .filter((h) => h.length > 0);

/** True when the app is being served from the platform's own host(s). Any other host is a tenant's custom domain. */
export function isPlatformHost(host: string = window.location.hostname): boolean {
  const normalized = host.toLowerCase();
  return platformHosts.some((p) => normalized === p);
}
