/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  /** Comma-separated hosts the platform itself is served from; any other host is treated as a tenant's custom domain. */
  readonly VITE_PLATFORM_HOSTS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
