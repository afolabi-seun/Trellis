import { render, screen } from "@testing-library/react";
import { MantineProvider } from "@mantine/core";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "../../src/App";
import { theme } from "../../src/theme/theme";

describe("App", () => {
  it("redirects the root route to the Tenant Admin shell", () => {
    render(
      <MantineProvider theme={theme}>
        <MemoryRouter initialEntries={["/"]}>
          <App />
        </MemoryRouter>
      </MantineProvider>,
    );

    expect(screen.getByText("Tenant Admin")).toBeInTheDocument();
  });
});
