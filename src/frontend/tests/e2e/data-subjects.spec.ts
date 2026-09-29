import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deletePropertyAssetDataForTenant,
  deleteTenantByName,
  seedDepartmentStaff,
  seedOwner,
  seedTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// security_guide.md's POPIA data-subject export/erasure — TENANT_ADMIN only. A single seeded
// Owner (PropertyAsset) is enough to prove the page is wired to real data end to end; the full
// cross-module redaction behavior (Leasing, Reporting, Notification, Auth) is covered by the
// backend's own unit/isolation tests and a live smoke script, not duplicated here.
// UUIDs are distinct from every other spec's (parallel workers share one Postgres).
const TENANT_ID = "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1";
const TENANT_NAME = `E2E Data Subjects Tenant ${Date.now()}`;
const OWNER_ID = "d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2";
const TENANT_ADMIN_USERNAME = `e2e-datasubjects-admin-${Date.now()}@trellis-platform.test`;
const DEPARTMENT_STAFF_USERNAME = `e2e-datasubjects-staff-${Date.now()}@trellis-platform.test`;
const OWNER_EMAIL = `e2e-datasubjects-owner-${Date.now()}@example.com`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartmentStaff(DEPARTMENT_STAFF_USERNAME, TENANT_ID, []);
  await seedOwner(OWNER_ID, TENANT_ID, "Erasable Owner", OWNER_EMAIL);
});

test.afterAll(async () => {
  await deletePropertyAssetDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME, DEPARTMENT_STAFF_USERNAME]);
  await deleteTenantByName(TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("a Department Staff login cannot reach the data subjects page", async ({ page }) => {
  await login(page, DEPARTMENT_STAFF_USERNAME);

  await page.goto("/admin/data-subjects");

  await expect(page).toHaveURL(/\/admin\/properties$/);
});

test("a Tenant Admin exports and then erases a data subject, and the redaction is real", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Data subjects", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/data-subjects$/);
  await expect(page.getByRole("textbox", { name: "Subject email" })).toBeVisible();
  const axeResults = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(axeResults.violations).toEqual([]);

  await page.getByRole("textbox", { name: "Subject email" }).fill(OWNER_EMAIL);

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export data" }).click();
  const download = await downloadPromise;
  const path = await download.path();
  const fs = await import("node:fs/promises");
  const exported = JSON.parse(path ? await fs.readFile(path, "utf-8") : "{}");
  expect(exported.modules.PropertyAsset).toBeTruthy();

  await page.getByRole("button", { name: "Erase data" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This is irreversible.")).toBeVisible();
  const eraseButton = dialog.getByRole("button", { name: "Erase permanently" });
  await expect(eraseButton).toBeDisabled();

  await dialog.getByRole("textbox", { name: "Retype subject email" }).fill("wrong@example.com");
  await expect(eraseButton).toBeDisabled();
  await dialog.getByRole("textbox", { name: "Retype subject email" }).fill(OWNER_EMAIL);
  await dialog.getByLabel("Your password").fill(SEED_PASSWORD);
  await expect(eraseButton).toBeEnabled();
  const dialogAxeResults = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(dialogAxeResults.violations).toEqual([]);

  await eraseButton.click();
  await expect(dialog.getByText("Erasure complete")).toBeVisible();
  await expect(dialog.getByRole("cell", { name: "PropertyAsset" })).toBeVisible();
  await expect(
    dialog
      .getByRole("row", { name: /PropertyAsset/ })
      .getByRole("cell")
      .last(),
  ).toHaveText("1");

  await dialog.getByRole("button", { name: "Done" }).click();
  await expect(dialog).toBeHidden();
});
