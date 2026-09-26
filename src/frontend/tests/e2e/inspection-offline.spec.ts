import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteInspectionDataForTenant,
  deleteTenantByName,
  deleteVendor,
  deleteVendorDataForTenant,
  seedInspection,
  seedTenant,
  seedTenantAdmin,
  seedTenantVendorRelationship,
  seedVendor,
  seedVendorAssignment,
  seedVendorUser,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 5 exit criterion, verbatim: "A Vendor completes an assigned inspection with
// photos while offline, and it syncs automatically the moment connectivity returns." This spec is
// that criterion, executable — the inspection is seeded directly (mirroring seedWorkflowInstance's
// own precedent) with the same 5 baseline checklist items InspectionChecklistDefaults.BaselineLabels
// auto-populates in the real API.
const TENANT_ID = "99999999-9999-9999-9999-999999999999";
const TENANT_NAME = `E2E Inspection Tenant ${Date.now()}`;
const VENDOR_ID = "a9a9a9a9-a9a9-a9a9-a9a9-a9a9a9a9a9a9";
const PROPERTY_ID = "b8b8b8b8-b8b8-b8b8-b8b8-b8b8b8b8b8b8";
const INSPECTION_ID = "c7c7c7c7-c7c7-c7c7-c7c7-c7c7c7c7c7c7";

const TENANT_ADMIN_USERNAME = `e2e-insp-admin-${Date.now()}@trellis-platform.test`;
const VENDOR_USERNAME = `e2e-insp-vendor-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME);
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedVendor(VENDOR_ID, "Acme Inspection Services");
  await seedTenantVendorRelationship(TENANT_ID, VENDOR_ID);
  await seedVendorUser(VENDOR_USERNAME, VENDOR_ID);
  await seedInspection(INSPECTION_ID, TENANT_ID, PROPERTY_ID);
  await seedVendorAssignment(VENDOR_ID, TENANT_ID, INSPECTION_ID, "INSPECTION");
});

test.afterAll(async () => {
  await deleteVendorDataForTenant(TENANT_ID);
  await deleteVendor(VENDOR_ID);
  await deleteInspectionDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME, VENDOR_USERNAME]);
  await deleteTenantByName(TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string, expectedUrl: RegExp) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(expectedUrl);
}

test("BDR-INSP-003/BDR-NFR-001: a Vendor completes an inspection with a photo while offline, and it syncs the moment connectivity returns", async ({
  page,
  context,
}) => {
  await context.grantPermissions(["geolocation"]);
  await context.setGeolocation({ latitude: 37.7749, longitude: -122.4194 });

  await login(page, VENDOR_USERNAME, /\/vendor$/);

  await page.goto(`/vendor/inspections/${INSPECTION_ID}`);
  await expect(page.getByText("Smoke Detectors")).toBeVisible();
  await expect(page.getByText("Plumbing")).toBeVisible();

  // Start happens online, before the vendor goes into the field and loses connectivity.
  await page.getByRole("button", { name: "Start inspection" }).click();
  await expect(page.getByRole("button", { name: "Start inspection" })).toHaveCount(0);

  // The moment connectivity drops, everything below happens against the local Dexie draft only.
  await context.setOffline(true);

  await page.getByLabel("Notes for Smoke Detectors").fill("Checked, working");
  // Mantine's always-mounted (but hidden) options listbox also carries the same aria-label —
  // scoping to the textbox role avoids matching both (same fix vendor.spec.ts already needed).
  await page.getByRole("textbox", { name: "Status for Smoke Detectors" }).click();
  await page.getByRole("option", { name: "Pass" }).click();

  await page.locator('input[type="file"]').setInputFiles({
    name: "hallway.jpg",
    mimeType: "image/jpeg",
    buffer: Buffer.from([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0xff, 0xd9]),
  });
  await expect(page.getByText("hallway.jpg")).toBeVisible();

  await page.getByRole("button", { name: "Finish inspection" }).click();
  // Offline: the completion attempt fails against the network and is queued, not lost.
  await expect(page.getByText(/Queued.*sync when back online/)).toBeVisible();

  await context.setOffline(false);

  // syncCoordinator's `online` listener fires automatically — no user action needed here.
  await expect(page.getByText("Inspection synced")).toBeVisible({ timeout: 15000 });

  // Re-opening the (now-closed) inspection re-fetches from the server fresh — confirms the sync
  // actually reached the server exactly once, with exactly the one photo captured offline.
  await page.goto(`/vendor/inspections/${INSPECTION_ID}`);
  await expect(page.getByText("hallway.jpg")).toBeVisible();
  await expect(page.locator("table").last().locator("tbody tr")).toHaveCount(1);
});

test("BDR-NFR-007: the inspection capture page has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, VENDOR_USERNAME, /\/vendor$/);
  await page.goto(`/vendor/inspections/${INSPECTION_ID}`);
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});
