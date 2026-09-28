import "@mantine/core/styles.css";
import "@mantine/dropzone/styles.css";

import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { MantineProvider } from "@mantine/core";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { App } from "./App";
import { TrellisApiError } from "./api/client";
import { AuthProvider } from "./auth/AuthContext";
import { initSyncCoordinator } from "./offline/syncCoordinator";
import { theme } from "./theme/theme";

// A 4xx (bad request, forbidden, not found, ...) is never going to succeed on retry — only a
// network blip or a genuine 5xx might. Without this, every isError-driven UI branch across the
// app (an upsell state, a "could not load" alert) sits in a false not-yet-failed state for
// TanStack Query's default 3-retry backoff (~7s) before the real error ever surfaces.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        if (error instanceof TrellisApiError && error.status >= 400 && error.status < 500) {
          return false;
        }
        return failureCount < 3;
      },
    },
  },
});

// frontend_architecture.md's Offline Inspection Capture — starts the connectivity-listener/
// Background-Sync coordinator once, at app boot, independent of which route is active.
initSyncCoordinator();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <MantineProvider theme={theme}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthProvider>
            <App />
          </AuthProvider>
        </BrowserRouter>
      </QueryClientProvider>
    </MantineProvider>
  </React.StrictMode>,
);
