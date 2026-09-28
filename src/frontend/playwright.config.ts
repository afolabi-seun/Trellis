import { defineConfig } from "@playwright/test";

// devops_environments.md / roadmap.md: axe-core wired into the Playwright pipeline from the
// start, so accessibility is a gate from the first page built, not bolted on later.
export default defineConfig({
  testDir: "./tests/e2e",
  // Login is an Argon2id verification, and Playwright runs 4 workers against one API+Postgres, so
  // the 5s/30s defaults intermittently timed out on unrelated specs' login and axe steps (seen
  // repeatedly from Phase 8 onward as the suite grew) — a load artifact, not a product bug.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  webServer: {
    command: "npm run preview",
    url: "http://localhost:4173",
    reuseExistingServer: !process.env.CI,
  },
  use: {
    baseURL: "http://localhost:4173",
    // Any *.test hostname resolves to this machine, so tenant-custom-domain rendering can be proven
    // end to end (tenant-domain.spec.ts) without editing /etc/hosts. Every other spec uses localhost.
    launchOptions: { args: ["--host-resolver-rules=MAP *.test 127.0.0.1"] },
  },
});
