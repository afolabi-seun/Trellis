import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    react(),
    // frontend_architecture.md's "Offline Inspection Capture" — a Service Worker scoped to the
    // /vendor/* and inspection routes, caching just the app shell (navigateFallback) so it loads
    // with no connectivity. Everything else (IndexedDB via Dexie, the sync queue) lives in
    // src/offline/, independent of this plugin.
    VitePWA({
      registerType: "autoUpdate",
      injectRegister: "auto",
      manifest: {
        name: "Trellis",
        short_name: "Trellis",
        start_url: "/vendor",
        display: "standalone",
        theme_color: "#1971c2",
        background_color: "#ffffff",
      },
      workbox: {
        // Only the vendor portal and staff inspection routes need offline app-shell access —
        // the Tenant Admin/Super Admin pages have no offline requirement and shouldn't silently
        // start caching stale data.
        navigateFallback: "/index.html",
        navigateFallbackAllowlist: [/^\/vendor(\/.*)?$/, /^\/admin\/inspections(\/.*)?$/],
        runtimeCaching: [],
      },
      devOptions: {
        enabled: false,
      },
    }),
  ],
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/unit/setup.ts"],
    include: ["tests/unit/**/*.test.{ts,tsx}"],
  },
});
