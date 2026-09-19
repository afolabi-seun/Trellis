import { defineConfig } from "@playwright/test";

// devops_environments.md / roadmap.md: axe-core wired into the Playwright pipeline from the
// start, so accessibility is a gate from the first page built, not bolted on later.
export default defineConfig({
  testDir: "./tests/e2e",
  webServer: {
    command: "npm run preview",
    url: "http://localhost:4173",
    reuseExistingServer: !process.env.CI,
  },
  use: {
    baseURL: "http://localhost:4173",
  },
});
