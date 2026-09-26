import "@mantine/core/styles.css";
import "@mantine/dropzone/styles.css";

import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { MantineProvider } from "@mantine/core";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { App } from "./App";
import { AuthProvider } from "./auth/AuthContext";
import { initSyncCoordinator } from "./offline/syncCoordinator";
import { theme } from "./theme/theme";

const queryClient = new QueryClient();

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
