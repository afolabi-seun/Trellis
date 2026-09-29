import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deletePropertyAssetDataForTenant,
  deleteRentLedgerDataForTenant,
  deleteReportingDataForTenant,
  deleteTenantByName,
  deleteWorkflowDataForTenant,
  seedDepartmentStaff,
  seedModuleEntitlement,
  seedOwner,
  seedProperty,
  seedRentSchedule,
  seedTenant,
  seedTenantAdmin,
  seedUnitAsset,
  seedWorkflowInstance,
  SEED_PASSWORD,
} from "./support/dbSeed";

// Phase 8's exit criterion: "An Owner report reflects real portfolio data end-to-end, generated
// without a Trellis engineer hand-assembling it." A full scenario (a real property, a real open
// work order, a real Owner) can't be driven through the UI alone — Owner/Property creation is its
// own already-tested flow (propertyasset.spec.ts) — so the underlying data is seeded directly,
// mirroring billing.spec.ts's own precedent; report generation, download, delivery, and the
// share-link flow are all driven through the real UI and a real unauthenticated fetch.
// Distinct from every other spec's own fixed UUIDs — vendor.spec.ts's own TENANT_ALPHA_ID already
// occupies "a1a1...", the same pattern this file originally (and wrongly) reused, causing a real
// PK_tenant collision under Playwright's parallel-worker execution (ai.spec.ts hit and fixed this
// identical class of bug once before, for the same reason).
const TENANT_ID = "9a9a9a9a-9a9a-9a9a-9a9a-9a9a9a9a9a9a";
const TENANT_NAME = `E2E Reporting Tenant ${Date.now()}`;
const OWNER_ID = "9b9b9b9b-9b9b-9b9b-9b9b-9b9b9b9b9b9b";
const PROPERTY_ID = "9c9c9c9c-9c9c-9c9c-9c9c-9c9c9c9c9c9c";
const WORKFLOW_INSTANCE_ID = "9d9d9d9d-9d9d-9d9d-9d9d-9d9d9d9d9d9d";
const DEPARTMENT_ID = "9e9e9e9e-9e9e-9e9e-9e9e-9e9e9e9e9e9e";
const UNIT_ID = "9f9f9f9f-8a8a-8a8a-8a8a-9f9f9f9f9f9f";
const RENT_SCHEDULE_ID = "8a8a8a8a-9c9c-9c9c-9c9c-8a8a8a8a8a8a";
const TENANT_ADMIN_USERNAME = `e2e-reporting-admin-${Date.now()}@trellis-platform.test`;
const DEPARTMENT_STAFF_USERNAME = `e2e-reporting-staff-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartmentStaff(DEPARTMENT_STAFF_USERNAME, TENANT_ID, [DEPARTMENT_ID]);
  await seedOwner(OWNER_ID, TENANT_ID, "Jane Owner", "jane.owner@example.com");
  await seedProperty(PROPERTY_ID, TENANT_ID, OWNER_ID, "123 Main St");
  await seedWorkflowInstance(WORKFLOW_INSTANCE_ID, TENANT_ID);
  // BDR-RPT-001's fifth tile: the dashboard's arrears count is only shown when the tenant is
  // entitled to RENT_COLLECTION — a long-overdue schedule with no payments proves it's real, not
  // a stub zero.
  await seedUnitAsset(UNIT_ID, TENANT_ID, PROPERTY_ID, "Unit R1");
  await seedModuleEntitlement(TENANT_ID, "RENT_COLLECTION", "ACTIVE");
  await seedRentSchedule(RENT_SCHEDULE_ID, TENANT_ID, UNIT_ID, 900);
});

test.afterAll(async () => {
  await deleteRentLedgerDataForTenant(TENANT_ID);
  await deleteReportingDataForTenant(TENANT_ID);
  await deleteWorkflowDataForTenant(TENANT_ID);
  await deletePropertyAssetDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME, DEPARTMENT_STAFF_USERNAME]);
  await deleteTenantByName(TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string, landingUrl: RegExp) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(landingUrl);
}

test("BDR-RPT-001: a Tenant Admin views the dashboard and sees counts reflecting real seeded data", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);

  await page.getByRole("link", { name: "Reporting", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/reporting$/);

  await expect(page.getByText("Open work orders")).toBeVisible();
  const openWorkOrdersTile = page.getByText("Open work orders").locator("..");
  await expect(openWorkOrdersTile.getByText("1", { exact: true })).toBeVisible();

  await expect(page.getByText("Rent schedules in arrears")).toBeVisible();
  const arrearsTile = page.getByText("Rent schedules in arrears").locator("..");
  await expect(arrearsTile.getByText("1", { exact: true })).toBeVisible();
});

test("BDR-OWN-002/BDR-SEC-003: a Tenant Admin generates an Owner report, downloads its CSV, and the share link resolves anonymously then 404s once revoked", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);

  await page.getByRole("link", { name: "Owner reports", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/owner-reports$/);

  await page.getByRole("textbox", { name: "Owner" }).click();
  await page.getByRole("option", { name: "Jane Owner" }).click();
  await page.getByLabel("Start").fill("2026-01-01");
  await page.getByLabel("End").fill("2026-01-31");
  await page.getByRole("button", { name: "Generate report" }).click();

  await expect(page.getByText("READY")).toBeVisible({ timeout: 10000 });

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "CSV" }).click();
  const download = await downloadPromise;
  const csvPath = await download.path();
  const fs = await import("node:fs/promises");
  const csvContent = csvPath ? await fs.readFile(csvPath, "utf-8") : "";
  expect(csvContent).toContain("Metric,Value");
  expect(csvContent).toContain("Owner,Jane Owner");

  await page.getByRole("button", { name: "Share link" }).click();
  await expect(page.getByText("Share link created")).toBeVisible();
  const shareUrl = await page.getByRole("textbox").last().inputValue();

  const resolveResponse = await page.request.get(shareUrl);
  expect(resolveResponse.status()).toBe(200);
  expect(resolveResponse.headers()["content-type"]).toBe("application/pdf");

  await page.getByRole("button", { name: "Revoke this link" }).click();
  await expect(page.getByText("Share link created")).toHaveCount(0);

  const revokedResponse = await page.request.get(shareUrl);
  expect(revokedResponse.status()).toBe(404);
});

test("BDR-RPT-001: a DEPARTMENT_STAFF login can reach the dashboard but not Owner reports", async ({
  page,
}) => {
  await login(page, DEPARTMENT_STAFF_USERNAME, /\/admin\/properties$/);

  await page.goto("/admin/reporting");
  await expect(page).toHaveURL(/\/admin\/reporting$/);
  await expect(page.getByText("Open work orders")).toBeVisible();

  await page.goto("/admin/owner-reports");
  await expect(page).toHaveURL(/\/admin\/properties$/);
});

test("BDR-NFR-007: /admin/reporting has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);
  await page.goto("/admin/reporting");
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});

test("BDR-NFR-007: /admin/owner-reports has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);
  await page.goto("/admin/owner-reports");
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});
