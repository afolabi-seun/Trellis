import { randomUUID } from "node:crypto";
import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteBillingDataForTenant,
  deleteDocumentDataForTenant,
  deleteLeasingDataForTenant,
  deleteRentLedgerDataForTenant,
  deletePropertyAssetDataForTenant,
  deleteTenantByName,
  getDocumentsForUnit,
  getEnvelopeIdForTenant,
  getRentSchedulesForUnit,
  getUnitOccupancyStatus,
  seedApplicationLinkToken,
  seedDepartmentStaff,
  seedLeaseDraftDocument,
  seedModuleEntitlement,
  seedOwner,
  seedProperty,
  seedRentSchedule,
  seedTenant,
  seedTenantAdmin,
  seedUnitAsset,
  SEED_PASSWORD,
  tenantSlugFor,
} from "./support/dbSeed";

// Phase 9's exit criterion: a prospective tenant applies via a secure link with no platform
// account, staff review it, and a completed e-signature flips the unit to occupied and files the
// lease. The emailed link can't be read by a test, so a token with a known raw value is seeded
// (only its hash is stored, as in the app); everything else — listing creation, photo upload,
// public browsing, the application itself, approval, send-for-signature — goes through the real
// UI, and the e-signature provider's completion callback is posted to the real webhook endpoint.
// UUIDs are distinct from every other spec's (parallel workers share one Postgres).
const TENANT_ID = "9f9f9f9f-9f9f-9f9f-9f9f-9f9f9f9f9f9f";
const TENANT_NAME = `E2E Leasing Tenant ${Date.now()}`;
const OWNER_ID = "a0a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a0";
const PROPERTY_ID = "a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2";
const UNIT_ID = "a4a4a4a4-a4a4-a4a4-a4a4-a4a4a4a4a4a4";
const DRAFT_DOCUMENT_ID = "a6a6a6a6-a6a6-a6a6-a6a6-a6a6a6a6a6a6";
const UNENTITLED_TENANT_ID = "b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0";
const UNENTITLED_TENANT_NAME = `E2E Leasing Unentitled Tenant ${Date.now()}`;
const TENANT_ADMIN_USERNAME = `e2e-leasing-admin-${Date.now()}@trellis-platform.test`;
const DEPARTMENT_STAFF_USERNAME = `e2e-leasing-staff-${Date.now()}@trellis-platform.test`;
const UNENTITLED_ADMIN_USERNAME = `e2e-leasing-unentitled-${Date.now()}@trellis-platform.test`;
const APPLICANT_EMAIL = "applicant@example.com";
const RAW_TOKEN = `e2e-leasing-token-${Date.now()}`;
const PROPERTY_ADDRESS = "42 Leasing Lane";
const UNIT_LABEL = "Unit 7A";
const LISTING_DESCRIPTION = "Sunny two-bedroom near the park";
const API_URL = "http://localhost:5080";

// A 1x1 JPEG, base64 — a real image the upload's content check accepts.
const TINY_JPEG = Buffer.from(
  "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=",
  "base64",
);

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartmentStaff(DEPARTMENT_STAFF_USERNAME, TENANT_ID, []);
  await seedModuleEntitlement(TENANT_ID, "LEASING", "ACTIVE");
  await seedOwner(OWNER_ID, TENANT_ID, "Lee Owner", "lee.owner@example.com");
  await seedProperty(PROPERTY_ID, TENANT_ID, OWNER_ID, PROPERTY_ADDRESS);
  await seedUnitAsset(UNIT_ID, TENANT_ID, PROPERTY_ID, UNIT_LABEL);
  // BDR-RENT-001: lease execution starts a rent schedule for tenants with the add-on — and a schedule
  // left over from the unit's previous tenancy must be ended, never allowed to block the signed lease.
  await seedModuleEntitlement(TENANT_ID, "RENT_COLLECTION", "ACTIVE");
  await seedRentSchedule(randomUUID(), TENANT_ID, UNIT_ID, 800);

  await seedTenant(UNENTITLED_TENANT_ID, UNENTITLED_TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(UNENTITLED_ADMIN_USERNAME, UNENTITLED_TENANT_ID);
});

test.afterAll(async () => {
  await deleteRentLedgerDataForTenant(TENANT_ID);
  await deleteLeasingDataForTenant(TENANT_ID);
  await deleteDocumentDataForTenant(TENANT_ID);
  await deletePropertyAssetDataForTenant(TENANT_ID);
  await deleteBillingDataForTenant(TENANT_ID);
  await deleteAppUsers([
    TENANT_ADMIN_USERNAME,
    DEPARTMENT_STAFF_USERNAME,
    UNENTITLED_ADMIN_USERNAME,
  ]);
  await deleteTenantByName(TENANT_NAME);
  await deleteBillingDataForTenant(UNENTITLED_TENANT_ID);
  await deleteTenantByName(UNENTITLED_TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

async function expectNoAxeViolations(page: import("@playwright/test").Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
}

test("BDR-LSE-001: staff create a listing for a vacant unit and upload a photo", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Vacancy Listings", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/vacancy-listings$/);
  await expectNoAxeViolations(page);

  await page.getByRole("button", { name: "New listing" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Property" }).click();
  await page.getByRole("option", { name: PROPERTY_ADDRESS }).click();
  await dialog.getByRole("textbox", { name: "Unit" }).click();
  await page.getByRole("option", { name: UNIT_LABEL }).click();
  await dialog.getByLabel("Monthly rent").fill("1500");
  await dialog.getByLabel("Available from").fill("2026-12-01");
  await dialog.getByLabel("Description").fill(LISTING_DESCRIPTION);
  await dialog.getByRole("button", { name: "Create listing" }).click();

  await expect(page.getByRole("link", { name: LISTING_DESCRIPTION })).toBeVisible();
  await expect(page.getByText("LISTED", { exact: true })).toBeVisible();

  await page.getByRole("link", { name: LISTING_DESCRIPTION }).click();
  await page.locator('input[type="file"]').setInputFiles({
    name: "living-room.jpg",
    mimeType: "image/jpeg",
    buffer: TINY_JPEG,
  });
  await expect(page.getByText("living-room.jpg")).toBeVisible();
  await expect(page.getByText("CLEAN", { exact: true })).toBeVisible({ timeout: 10000 });
  await expectNoAxeViolations(page);
});

test("BDR-LSE-002: a listing can be browsed with no login, and an applicant can request a link", async ({
  page,
}) => {
  await page.goto(`/vacancies/${tenantSlugFor(TENANT_ID)}`);
  await expect(page.getByRole("heading", { name: "Available units" })).toBeVisible();
  await expect(page.getByRole("link", { name: UNIT_LABEL })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole("link", { name: UNIT_LABEL }).click();
  await expect(page.getByRole("heading", { name: UNIT_LABEL })).toBeVisible();
  await expect(page.getByRole("img", { name: `${UNIT_LABEL} photo 1` })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByLabel("Your email address").fill(APPLICANT_EMAIL);
  await page.getByRole("button", { name: "Email me the application link" }).click();
  await expect(page.getByText("Check your email")).toBeVisible();
});

test("BDR-LSE-002/008: an applicant submits via the secure link, which then stops working", async ({
  page,
}) => {
  await seedApplicationLinkToken(TENANT_ID, UNIT_ID, APPLICANT_EMAIL, RAW_TOKEN);

  await page.goto(`/apply/${RAW_TOKEN}`);
  await expect(page.getByRole("heading", { name: "Rental application" })).toBeVisible();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(APPLICANT_EMAIL);
  await expect(page.getByLabel("Email", { exact: true })).toHaveAttribute("readonly", "");
  await expectNoAxeViolations(page);

  await page.getByRole("button", { name: "Submit application" }).click();
  await expect(page.getByText("You must consent to screening to apply.")).toBeVisible();

  await page.getByLabel("Full name").fill("Pat Applicant");
  await page.getByLabel("Phone").fill("+27 82 555 0100");
  await page.getByLabel("Date of birth").fill("1990-05-20");
  await page.getByLabel("Current address").fill("1 Old Road");
  await page.getByLabel(/I consent to a credit and background screening/).check();
  await page.getByRole("button", { name: "Submit application" }).click();
  await expect(page.getByRole("heading", { name: "Application received" })).toBeVisible();

  await page.goto(`/apply/${RAW_TOKEN}`);
  await expect(page.getByText("This link isn't valid")).toBeVisible();
  await expectNoAxeViolations(page);
});

test("BDR-LSE-004/005: staff review the application, approve it, and a completed e-signature marks the unit occupied", async ({
  page,
}) => {
  await seedLeaseDraftDocument(DRAFT_DOCUMENT_ID, TENANT_ID, UNIT_ID);
  await login(page, DEPARTMENT_STAFF_USERNAME);

  await page.getByRole("link", { name: "Applications", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/applications$/);
  await page.getByRole("textbox", { name: "Listing" }).click();
  await page.getByRole("option", { name: new RegExp(LISTING_DESCRIPTION) }).click();

  await expect(page.getByRole("cell", { name: "Pat Applicant" })).toBeVisible();
  await expect(page.getByRole("cell", { name: APPLICANT_EMAIL })).toBeVisible();
  await expect(page.getByText("PASS", { exact: true })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole("button", { name: "History" }).click();
  await expect(page.getByText("SCREENING_RECEIVED")).toBeVisible();
  await expect(page.getByText("SUBMITTED", { exact: true }).first()).toBeVisible();
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Approve" }).click();
  await expect(page.getByText("APPROVED", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Send for signature" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Lease draft" }).click();
  await page.getByRole("option", { name: /Lease draft/ }).click();
  await dialog.getByRole("button", { name: "Send for signature" }).click();
  await expect(dialog).toBeHidden();

  const envelopeId = await getEnvelopeIdForTenant(TENANT_ID);
  const webhook = await page.request.post(`${API_URL}/webhooks/esignature`, {
    headers: { "x-esignature-signature": "e2e", "content-type": "application/json" },
    data: { event: "envelope.completed", envelopeId },
  });
  expect(webhook.status()).toBe(200);

  expect(await getUnitOccupancyStatus(UNIT_ID)).toBe("OCCUPIED");

  // BDR-LSE-006: the executed lease is the next VERSION of the draft staff sent — one document, two versions.
  const documents = await getDocumentsForUnit(UNIT_ID);
  expect(documents).toEqual([{ documentType: "LEASE", versionCount: 2, currentVersionNumber: 2 }]);

  // BDR-RENT-001: the previous tenancy's schedule is ended and a new one starts from the listing's terms.
  const schedules = await getRentSchedulesForUnit(UNIT_ID);
  expect(schedules).toHaveLength(2);
  expect(schedules[0]).toMatchObject({
    status: "ENDED",
    rentAmount: 800,
    hasLeaseApplication: false,
  });
  expect(schedules[1]).toMatchObject({
    status: "ACTIVE",
    rentAmount: 1500,
    dueDayOfMonth: 1,
    hasLeaseApplication: true,
  });

  await page.reload();
  await expect(page.getByText("LEASED", { exact: true }).first()).toBeVisible();

  await page.goto(`/vacancies/${tenantSlugFor(TENANT_ID)}`);
  await expect(page.getByRole("link", { name: UNIT_LABEL })).toHaveCount(0);
});

test("BDR-BIL-009: a tenant without the Leasing add-on sees an upsell instead of the module", async ({
  page,
}) => {
  await login(page, UNENTITLED_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Vacancy Listings", exact: true }).click();
  await expect(page.getByText("Leasing isn't enabled for your plan")).toBeVisible();
  await expectNoAxeViolations(page);
});
