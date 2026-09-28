import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteTenantByName,
  seedDepartmentStaff,
  seedTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// A tenant chooses who receives which alert (BDR-RENT-004's "configured staff"). The settings screen
// is TENANT_ADMIN only; a Department Staff login can't reach it. UUIDs are distinct from every other
// spec's (parallel workers share one Postgres).
const TENANT_ID = "f3f3f3f3-f3f3-f3f3-f3f3-f3f3f3f3f3f3";
const TENANT_NAME = `E2E Settings Tenant ${Date.now()}`;
const ADMIN = `e2e-settings-admin-${Date.now()}@trellis-platform.test`;
const STAFF = `e2e-settings-staff-${Date.now()}@trellis-platform.test`;

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(ADMIN, TENANT_ID);
  await seedDepartmentStaff(STAFF, TENANT_ID, []);
});

test.afterAll(async () => {
  await deleteAppUsers([ADMIN, STAFF]);
  await deleteTenantByName(TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("a Tenant Admin sees the defaults, changes who is notified about rent arrears, and it persists", async ({
  page,
}) => {
  await login(page, ADMIN);
  await page.getByRole("link", { name: "Settings", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/settings$/);

  const arrears = page.getByRole("group", { name: "Rent arrears" });
  await expect(arrears.getByRole("checkbox", { name: "Tenant admins" })).toBeChecked();
  await expect(arrears.getByRole("checkbox", { name: "Department staff" })).toBeChecked();

  await arrears.getByRole("checkbox", { name: "Department staff" }).uncheck();
  await page.getByRole("button", { name: "Save notification settings" }).click();
  await expect(page.getByText("Notification settings saved")).toBeVisible();

  await page.reload();
  const reloaded = page.getByRole("group", { name: "Rent arrears" });
  await expect(reloaded.getByRole("checkbox", { name: "Tenant admins" })).toBeChecked();
  await expect(reloaded.getByRole("checkbox", { name: "Department staff" })).not.toBeChecked();
  // The other alert was left alone.
  await expect(
    page
      .getByRole("group", { name: "New rental applications" })
      .getByRole("checkbox", { name: "Department staff" }),
  ).toBeChecked();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});

test("at least one role must stay selected for each alert", async ({ page }) => {
  await login(page, ADMIN);
  await page.goto("/admin/settings");

  const applications = page.getByRole("group", { name: "New rental applications" });
  await applications.getByRole("checkbox", { name: "Tenant admins" }).uncheck();
  await applications.getByRole("checkbox", { name: "Department staff" }).uncheck();

  await expect(page.getByRole("button", { name: "Save notification settings" })).toBeDisabled();
});

test("a Department Staff login cannot reach the settings screen", async ({ page }) => {
  await login(page, STAFF);

  await expect(page.getByRole("link", { name: "Settings", exact: true })).toHaveCount(0);
  await page.goto("/admin/settings");
  await expect(page).toHaveURL(/\/admin\/properties$/);
});
