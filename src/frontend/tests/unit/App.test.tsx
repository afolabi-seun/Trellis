import { render, screen } from "@testing-library/react";
import { MantineProvider } from "@mantine/core";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "../../src/App";
import { AuthProvider } from "../../src/auth/AuthContext";
import { theme } from "../../src/theme/theme";

function renderApp(initialEntry: string) {
  const queryClient = new QueryClient();
  return render(
    <MantineProvider theme={theme}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[initialEntry]}>
          <AuthProvider>
            <App />
          </AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>
    </MantineProvider>,
  );
}

describe("App", () => {
  it("redirects an unauthenticated visit to the root route to the login page", async () => {
    renderApp("/");

    // RequireRole renders nothing while the mount-time silent-refresh is in flight, then
    // redirects once it resolves (rejected, since fetch is stubbed to fail in unit tests) —
    // matching frontend_architecture.md's "UI-layer scoping is UX, not security" for an
    // unauthenticated visitor.
    expect(await screen.findByRole("heading", { name: "Sign in" })).toBeInTheDocument();
  });
});
