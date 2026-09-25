import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteDocumentDataForTenant,
  deleteTenantByName,
  deleteVendor,
  deleteVendorDataForTenant,
  deleteWorkflowDataForTenant,
  seedTenant,
  seedTenantAdmin,
  seedTenantVendorRelationship,
  seedVendor,
  seedVendorAssignment,
  seedVendorUser,
  seedWorkflowInstance,
  SEED_PASSWORD,
} from "./support/dbSeed";

// Phase 4's exit criterion, verbatim: "A single Vendor login sees and closes assignments across
// two different tenants, and cannot see anything else." Alpha and Beta are two unrelated PM
// tenants that independently engage the same vendor; Gamma engages nobody and stands in for "a
// third, unrelated tenant" the vendor must never see.
const TENANT_ALPHA_ID = "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1";
const TENANT_BETA_ID = "b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2";
const TENANT_GAMMA_ID = "c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3";
const TENANT_ALPHA_NAME = `E2E Vendor Alpha ${Date.now()}`;
const TENANT_BETA_NAME = `E2E Vendor Beta ${Date.now()}`;
const TENANT_GAMMA_NAME = `E2E Vendor Gamma ${Date.now()}`;

const VENDOR_ID = "d4d4d4d4-d4d4-d4d4-d4d4-d4d4d4d4d4d4";
const WORK_ITEM_ALPHA_ID = "e5e5e5e5-e5e5-e5e5-e5e5-e5e5e5e5e5e5";
const WORK_ITEM_BETA_ID = "f6f6f6f6-f6f6-f6f6-f6f6-f6f6f6f6f6f6";
const WORK_ITEM_GAMMA_ID = "07070707-0707-0707-0707-070707070707";

const ALPHA_ADMIN_USERNAME = `e2e-ven-alpha-admin-${Date.now()}@trellis-platform.test`;
const BETA_ADMIN_USERNAME = `e2e-ven-beta-admin-${Date.now()}@trellis-platform.test`;
const VENDOR_USERNAME = `e2e-ven-vendor-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ALPHA_ID, TENANT_ALPHA_NAME);
  await seedTenant(TENANT_BETA_ID, TENANT_BETA_NAME);
  await seedTenant(TENANT_GAMMA_ID, TENANT_GAMMA_NAME);
  await seedTenantAdmin(ALPHA_ADMIN_USERNAME, TENANT_ALPHA_ID);
  await seedTenantAdmin(BETA_ADMIN_USERNAME, TENANT_BETA_ID);

  await seedVendor(VENDOR_ID, "Acme Facilities Services");
  await seedTenantVendorRelationship(TENANT_ALPHA_ID, VENDOR_ID);
  // Beta's relationship is deliberately left unseeded — the "search and engage" flow below
  // establishes it live through the UI. vendor_assignment's own RLS is a plain tenant/vendor OR
  // with no dependency on tenant_vendor_relationship, so the Beta assignment seeded below is
  // already visible to the vendor regardless of when (or whether, in test order) that relationship
  // gets created — the two are independent by design (BDR-VEN-006).
  await seedVendorUser(VENDOR_USERNAME, VENDOR_ID);

  await seedWorkflowInstance(WORK_ITEM_ALPHA_ID, TENANT_ALPHA_ID);
  await seedWorkflowInstance(WORK_ITEM_BETA_ID, TENANT_BETA_ID);
  // Gamma's own work item is never assigned to the vendor — the negative case.
  await seedWorkflowInstance(WORK_ITEM_GAMMA_ID, TENANT_GAMMA_ID);

  await seedVendorAssignment(VENDOR_ID, TENANT_ALPHA_ID, WORK_ITEM_ALPHA_ID);
  await seedVendorAssignment(VENDOR_ID, TENANT_BETA_ID, WORK_ITEM_BETA_ID);
});

test.afterAll(async () => {
  await deleteVendorDataForTenant(TENANT_ALPHA_ID);
  await deleteVendorDataForTenant(TENANT_BETA_ID);
  await deleteVendor(VENDOR_ID);
  await deleteDocumentDataForTenant(TENANT_ALPHA_ID);
  await deleteDocumentDataForTenant(TENANT_BETA_ID);
  await deleteWorkflowDataForTenant(TENANT_ALPHA_ID);
  await deleteWorkflowDataForTenant(TENANT_BETA_ID);
  await deleteWorkflowDataForTenant(TENANT_GAMMA_ID);
  await deleteAppUsers([ALPHA_ADMIN_USERNAME, BETA_ADMIN_USERNAME, VENDOR_USERNAME]);
  await deleteTenantByName(TENANT_ALPHA_NAME);
  await deleteTenantByName(TENANT_BETA_NAME);
  await deleteTenantByName(TENANT_GAMMA_NAME);
});

async function login(page: import("@playwright/test").Page, username: string, expectedUrl: RegExp) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(expectedUrl);
}

test("BDR-VEN-004/006: one Vendor login sees and closes assignments across two tenants, and nothing from a third", async ({
  page,
}) => {
  await login(page, VENDOR_USERNAME, /\/vendor$/);

  // Both engaged tenants' assignments are visible under a single login — no tenant switcher.
  await expect(page.getByText(TENANT_ALPHA_NAME)).toBeVisible();
  await expect(page.getByText(TENANT_BETA_NAME)).toBeVisible();
  // Gamma never engaged this vendor — its work item must never surface.
  await expect(page.getByText(TENANT_GAMMA_NAME)).toHaveCount(0);

  // Open Alpha's assignment, start it, upload evidence, complete it.
  await page.getByText(TENANT_ALPHA_NAME).click();
  await expect(page).toHaveURL(new RegExp(`/vendor/assignments/${WORK_ITEM_ALPHA_ID}$`));
  // BDR-VEN-002: the vendor-safe work item view never carries cost/financial fields.
  await expect(page.getByText("500")).toHaveCount(0);

  await page.getByRole("button", { name: "Start work" }).click();
  // The assignment status badge and the underlying work item's own (independent) status can both
  // read IN_PROGRESS at once — same duplicate-text reasoning as workflow.spec.ts's own assertions.
  await expect(page.getByText("IN_PROGRESS").first()).toBeVisible();

  // Not inside a dialog here (unlike DocumentsLibraryPage's modal upload flow), so Mantine's
  // always-mounted (but hidden) options listbox also carries the same aria-labelledby text —
  // scoping to the textbox role, not getByLabel, avoids matching both.
  await page.getByRole("textbox", { name: "Document type" }).click();
  await page.getByRole("option", { name: "Inspection report" }).click();
  await page.locator('input[type="file"]').setInputFiles({
    name: "inspection.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4 e2e vendor evidence"),
  });
  await page.getByRole("button", { name: "Upload" }).click();
  await expect(page.getByText("inspection.pdf")).toBeVisible();

  await page.getByRole("button", { name: "Complete" }).click();
  await expect(page.getByText("CLOSED")).toBeVisible();

  // Beta's assignment is independent — still visible, untouched, closeable on its own.
  await page.getByRole("button", { name: "← Assignments" }).click();
  await page.getByText(TENANT_BETA_NAME).click();
  await expect(page).toHaveURL(new RegExp(`/vendor/assignments/${WORK_ITEM_BETA_ID}$`));
  await page.getByRole("button", { name: "Start work" }).click();
  await page.getByRole("button", { name: "Complete" }).click();
  await expect(page.getByText("CLOSED")).toBeVisible();
});

test("BDR-VEN-001: a second, unrelated Tenant Admin can find an already-onboarded vendor via search and engage it without duplicating it", async ({
  page,
}) => {
  await login(page, BETA_ADMIN_USERNAME, /\/admin\/properties$/);

  await page.getByRole("link", { name: "Vendors", exact: true }).click();
  await page.getByLabel("Search vendors").fill("Acme");
  await expect(page.getByText("Acme Facilities Services")).toBeVisible();

  await page.getByRole("button", { name: "Engage this vendor" }).click();
  await expect(page.getByText("ACTIVE")).toBeVisible();
});

test("BDR-VEN-004: Alpha's independently-seeded relationship with the same vendor already reads ACTIVE", async ({
  page,
}) => {
  await login(page, ALPHA_ADMIN_USERNAME, /\/admin\/properties$/);

  await page.getByRole("link", { name: "Vendors", exact: true }).click();
  await expect(page.getByText("Acme Facilities Services")).toBeVisible();
  await expect(page.getByText("ACTIVE")).toBeVisible();
});

for (const path of ["/admin/vendors"]) {
  test(`BDR-NFR-007: ${path} has no automatically detectable accessibility violations`, async ({
    page,
  }) => {
    await login(page, ALPHA_ADMIN_USERNAME, /\/admin\/properties$/);
    await page.goto(path);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);
  });
}

test("BDR-NFR-007: the vendor portal has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, VENDOR_USERNAME, /\/vendor$/);
  const listResults = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(listResults.violations).toEqual([]);

  await page.goto(`/vendor/assignments/${WORK_ITEM_ALPHA_ID}`);
  const detailResults = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(detailResults.violations).toEqual([]);
});
