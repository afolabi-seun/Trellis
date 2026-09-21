import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteTenantByName,
  seedSuperAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// Requires the real local backend (dotnet run) and Postgres (docker-compose) up at
// VITE_API_BASE_URL — this is a genuine end-to-end test, not a mocked one; login, session
// establishment, and RLS all have to actually work, same principle as testing_strategy.md's
// rule against mocking the backend's own DbContext.
const SUPER_ADMIN_USERNAME = `e2e-super-admin-${Date.now()}@trellis-platform.test`;
const TENANT_NAME = `E2E Test Tenant ${Date.now()}`;

test.beforeAll(async () => {
  await seedSuperAdmin(SUPER_ADMIN_USERNAME);
});

test.afterAll(async () => {
  await deleteTenantByName(TENANT_NAME);
  await deleteAppUsers([SUPER_ADMIN_USERNAME, "admin@e2e-test-tenant.test"]);
});

async function login(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(SUPER_ADMIN_USERNAME);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/platform$/);
}

test("BDR-TEN-001/004: a Super Admin can provision a tenant and see it in the list", async ({
  page,
}) => {
  await login(page);

  await page.getByRole("button", { name: "Provision tenant" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.waitFor({ state: "visible" });

  await page.getByLabel("Tenant name").fill(TENANT_NAME);
  await page.getByLabel("Country code").fill("US");
  await page.getByLabel("Admin email").fill("admin@e2e-test-tenant.test");
  await dialog.getByRole("button", { name: "Provision", exact: true }).click();

  await dialog.waitFor({ state: "hidden" });
  await expect(page.getByText(TENANT_NAME)).toBeVisible();
  await expect(page.getByText("ONBOARDING")).toBeVisible();
});

test("BDR-SEC-006: signing out clears the session and returns to /login", async ({ page }) => {
  await login(page);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  // The access token only ever lived in memory — a fresh navigation with no new login has no
  // session to resume, so this must redirect again rather than silently re-authenticating.
  await page.goto("/platform");
  await expect(page).toHaveURL(/\/login$/);
});

test("BDR-NFR-007: the tenants list has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page);
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});
