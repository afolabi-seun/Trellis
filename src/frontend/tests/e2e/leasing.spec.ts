import { createHmac, randomUUID } from "node:crypto";
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
  getLatestLeaseEndDate,
  getLeasesForUnit,
  getRentSchedulesForUnit,
  getUnitVacantSince,
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
import { waitForModalSettled } from "./support/modal";

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
// Matches ESignatureWebhookSecret in the running API's own .env — HmacESignatureSignatureValidator
// fails closed on any mismatch, so this must be the real dev-only value, not a placeholder header.
const ESIGNATURE_WEBHOOK_SECRET = "esig_test_dev_only_placeholder_not_a_real_secret";
const PROPERTY_ADDRESS = "42 Leasing Lane";
const UNIT_LABEL = "Unit 7A";
const LISTING_DESCRIPTION = "Sunny two-bedroom near the park";
// Overridable so a run can target an API on another port (e.g. two sessions sharing one machine).
const API_URL = process.env.E2E_API_URL ?? "http://localhost:5080";

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

/** yyyy-mm-dd, offset from today — lease dates must sit relative to the day the suite runs. */
function isoDay(offsetDays: number): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

// BDR-LSE-009: a lease that started a month ago and ends in 45 days lands in "31–60 days".
const LEASE_START = isoDay(-30);
const LEASE_END = isoDay(45);
const LEASE_ENDED_ON = isoDay(0);
// BDR-LSE-010: the accepted renewal runs a year past the current end, at a new rent from the day after it.
const RENEWED_END = (() => {
  const d = new Date(`${LEASE_END}T00:00:00Z`);
  d.setUTCFullYear(d.getUTCFullYear() + 1);
  return d.toISOString().slice(0, 10);
})();
const NEW_RENT_FROM = isoDay(46);

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
  // BDR-LSE-009: the send needs the lease term.
  await expect(dialog.getByRole("button", { name: "Send for signature" })).toBeDisabled();
  await dialog.getByLabel("Lease starts on").fill(LEASE_START);
  await dialog.getByLabel("Lease ends on").fill(LEASE_END);
  await dialog.getByRole("button", { name: "Send for signature" }).click();
  await expect(dialog).toBeHidden();

  const envelopeId = await getEnvelopeIdForTenant(TENANT_ID);
  const webhookBody = JSON.stringify({ event: "envelope.completed", envelopeId });
  const webhookSignature = createHmac("sha256", ESIGNATURE_WEBHOOK_SECRET)
    .update(webhookBody)
    .digest("hex");
  const webhook = await page.request.post(`${API_URL}/webhooks/esignature`, {
    headers: { "x-esignature-signature": webhookSignature, "content-type": "application/json" },
    data: webhookBody,
  });
  expect(webhook.status()).toBe(200);

  expect(await getUnitOccupancyStatus(UNIT_ID)).toBe("OCCUPIED");

  // BDR-LSE-009: the signed lease is recorded with the term staff set, and the unit's earlier,
  // manually started tenancy is superseded rather than blocking it.
  const leases = await getLeasesForUnit(UNIT_ID);
  expect(leases).toHaveLength(2);
  expect(leases[0]).toMatchObject({ source: "MANUAL", status: "ENDED", endReason: "OTHER" });
  expect(leases[1]).toMatchObject({
    source: "LEASING",
    status: "ACTIVE",
    termType: "FIXED",
    endDate: LEASE_END,
  });

  // BDR-LSE-006: the executed lease is the next VERSION of the draft staff sent — one document, two versions.
  const documents = await getDocumentsForUnit(UNIT_ID);
  expect(documents).toEqual([{ documentType: "LEASE", versionCount: 2, currentVersionNumber: 2 }]);

  // BDR-RENT-001: the previous tenancy's schedule is ended and a new one starts from the listing's
  // rent on the lease's start date, its due day taken from that date (capped at 28).
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
    dueDayOfMonth: Math.min(Number(LEASE_START.slice(8)), 28),
    hasLeaseApplication: true,
  });

  await page.reload();
  await expect(page.getByText("LEASED", { exact: true }).first()).toBeVisible();

  // BDR-LSE-009: it shows on the Leases page in the 31–60 day horizon.
  await page.getByRole("link", { name: "Leases", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/leases$/);
  await page.getByRole("tab", { name: "31–60 days (1)" }).click();
  await expect(page.getByRole("cell", { name: UNIT_LABEL })).toBeVisible();
  await expect(page.getByRole("cell", { name: LEASE_END })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.goto(`/vacancies/${tenantSlugFor(TENANT_ID)}`);
  await expect(page.getByRole("link", { name: UNIT_LABEL })).toHaveCount(0);
});

test("BDR-LSE-004: only a Tenant Admin sees the full screening report, behind a password re-entry", async ({
  page,
}) => {
  await login(page, DEPARTMENT_STAFF_USERNAME);
  await page.getByRole("link", { name: "Applications", exact: true }).click();
  await page.getByRole("textbox", { name: "Listing" }).click();
  await page.getByRole("option", { name: new RegExp(LISTING_DESCRIPTION) }).click();
  await expect(page.getByRole("cell", { name: "Pat Applicant" })).toBeVisible();
  await expect(page.getByRole("button", { name: "View full report" })).toHaveCount(0);

  await login(page, TENANT_ADMIN_USERNAME);
  await page.getByRole("link", { name: "Applications", exact: true }).click();
  await page.getByRole("textbox", { name: "Listing" }).click();
  await page.getByRole("option", { name: new RegExp(LISTING_DESCRIPTION) }).click();
  await page.getByRole("button", { name: "View full report" }).click();

  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("Re-enter your password")).toBeVisible();
  await dialog.getByLabel("Your password").fill("wrong-password-entirely");
  await dialog.getByRole("button", { name: "View report" }).click();
  await expect(dialog.getByText("That password isn't correct.")).toBeVisible();

  await dialog.getByLabel("Your password").fill(SEED_PASSWORD);
  await dialog.getByRole("button", { name: "View report" }).click();
  await expect(dialog.getByText("Sensitive information")).toBeVisible();
  await expect(dialog.getByText(/Provider: NOOP/)).toBeVisible();
  await expect(dialog.getByText(/"simulated":true/)).toBeVisible();
  await waitForModalSettled(page);
  await expectNoAxeViolations(page);

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
});

test("BDR-LSE-010: staff offer a renewal, record a decline, then an acceptance that extends the lease and changes the rent from its date", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);
  await page.getByRole("link", { name: "Leases", exact: true }).click();
  await page.getByRole("tab", { name: "31–60 days (1)" }).click();
  const leaseRow = page.getByRole("row", { name: new RegExp(UNIT_LABEL) });

  async function offerRenewal({ checkAccessibility }: { checkAccessibility: boolean }) {
    await leaseRow.getByRole("button", { name: "Offer renewal" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByLabel("New end date")).toHaveValue(RENEWED_END);
    await dialog.getByLabel("Change the rent").check();
    await dialog.getByLabel("New rent per period").fill("1650");
    await expect(dialog.getByLabel("New rent applies from")).toHaveValue(NEW_RENT_FROM);
    // The second offer opens the same dialog, so scanning it once is enough.
    if (checkAccessibility) {
      await waitForModalSettled(page);
      await expectNoAxeViolations(page);
    }
    await dialog.getByRole("button", { name: "Record offer" }).click();
    await expect(dialog).toBeHidden();
    await expect(leaseRow.getByText(/Offered · reply by/)).toBeVisible();
  }

  async function recordAnswer(answer: string, reason?: string) {
    await leaseRow.getByRole("button", { name: "Record answer" }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("textbox", { name: "Answer" }).click();
    await page.getByRole("option", { name: answer }).click();
    if (reason) {
      await dialog.getByRole("textbox", { name: "Why?" }).click();
      await page.getByRole("option", { name: reason }).click();
    }
    await dialog.getByRole("button", { name: "Save answer" }).click();
    await expect(dialog).toBeHidden();
  }

  // Declined: the lease is untouched, and the tenant's own reason list supplies the reason.
  await offerRenewal({ checkAccessibility: true });
  await recordAnswer("Declined", "Rent too high");
  await expect(leaseRow.getByRole("button", { name: "Offer renewal" })).toBeVisible();
  expect(await getLatestLeaseEndDate(UNIT_ID)).toBe(LEASE_END);

  // Accepted: the same lease is extended, so it leaves the 90-day horizon.
  await offerRenewal({ checkAccessibility: false });
  await recordAnswer("Accepted — extend the lease");
  await expect(page.getByRole("tab", { name: "31–60 days (0)" })).toBeVisible();
  expect(await getLatestLeaseEndDate(UNIT_ID)).toBe(RENEWED_END);
  expect(await getLeasesForUnit(UNIT_ID)).toHaveLength(2);

  await page.getByRole("tab", { name: "Renewal offers (0 open)" }).click();
  await expect(page.getByText("Accepted", { exact: true })).toBeVisible();
  await expect(page.getByText(/Rent too high/)).toBeVisible();
  await expectNoAxeViolations(page);

  // The rent changes only from the renewal's date; the current rent stays as it is.
  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();
  await page
    .getByRole("row", { name: new RegExp(`${UNIT_LABEL}.*1,500`) })
    .getByRole("link")
    .click();
  await expect(page.getByText(/1,500\.00 per period/)).toBeVisible();
  await expect(
    page.getByText(new RegExp(`Rent changes to .*1,650\\.00 from ${NEW_RENT_FROM}`)),
  ).toBeVisible();
});

test("BDR-LSE: staff end a lease, freeing the unit and its rent schedule so the unit can be re-listed", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Applications", exact: true }).click();
  await page.getByRole("textbox", { name: "Listing" }).click();
  await page.getByRole("option", { name: new RegExp(LISTING_DESCRIPTION) }).click();

  const row = page.getByRole("row", { name: "Pat Applicant" });
  await expect(row.getByText("LEASED", { exact: true })).toBeVisible();

  await row.getByRole("button", { name: "End lease" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This ends the lease")).toBeVisible();
  await dialog.getByLabel("Lease ended on").fill(LEASE_ENDED_ON);
  // BDR-LSE-011: why and by whom, from fixed lists — and Other needs a note.
  await dialog.getByRole("textbox", { name: "Why did it end?" }).click();
  await page.getByRole("option", { name: "Other (add a note)" }).click();
  await dialog.getByRole("textbox", { name: "Who ended it?" }).click();
  await page.getByRole("option", { name: "The lessee" }).click();
  await expect(dialog.getByRole("button", { name: "End lease" })).toBeDisabled();
  await dialog.getByRole("textbox", { name: "Note" }).fill("Relocated for work");
  await waitForModalSettled(page);
  await expectNoAxeViolations(page);
  await dialog.getByRole("button", { name: "End lease" }).click();
  await expect(dialog).toBeHidden();

  await expect(row.getByText("LEASE_ENDED", { exact: true })).toBeVisible();
  await expect(row.getByRole("button", { name: "End lease" })).toHaveCount(0);
  await expectNoAxeViolations(page);

  expect(await getUnitOccupancyStatus(UNIT_ID)).toBe("VACANT");
  // BDR-AST-005: vacant from the lease's end date.
  expect(await getUnitVacantSince(UNIT_ID)).toBe(LEASE_ENDED_ON);
  expect((await getLeasesForUnit(UNIT_ID)).at(-1)).toMatchObject({
    source: "LEASING",
    status: "ENDED",
    endReason: "OTHER",
    endedBy: "LESSEE",
    endNote: "Relocated for work",
  });
  const schedules = await getRentSchedulesForUnit(UNIT_ID);
  expect(schedules.at(-1)).toMatchObject({ status: "ENDED", hasLeaseApplication: true });

  // The occupancy column on the property page reflects the vacated unit.
  await page.getByRole("link", { name: "Properties", exact: true }).click();
  await page.getByRole("link", { name: PROPERTY_ADDRESS }).click();
  const unitRow = page.getByRole("row", { name: UNIT_LABEL });
  await expect(unitRow.getByText("VACANT", { exact: true })).toBeVisible();
  await expect(unitRow.getByText(`${LEASE_ENDED_ON} (0 days)`)).toBeVisible();

  // The now-vacant unit can be re-listed.
  await page.getByRole("link", { name: "Vacancy Listings", exact: true }).click();
  await page.getByRole("button", { name: "New listing" }).click();
  const listingDialog = page.getByRole("dialog");
  await listingDialog.getByRole("textbox", { name: "Property" }).click();
  await page.getByRole("option", { name: PROPERTY_ADDRESS }).click();
  await listingDialog.getByRole("textbox", { name: "Unit" }).click();
  await page.getByRole("option", { name: UNIT_LABEL }).click();
  await listingDialog.getByLabel("Monthly rent").fill("1700");
  await listingDialog.getByLabel("Available from").fill("2026-07-01");
  await listingDialog.getByLabel("Description").fill("Re-listed after move-out");
  await listingDialog.getByRole("button", { name: "Create listing" }).click();
  await expect(listingDialog).toBeHidden();
  await expect(page.getByRole("link", { name: "Re-listed after move-out" })).toBeVisible();
});

test("BDR-BIL-009: a tenant without the Leasing add-on sees an upsell instead of the module", async ({
  page,
}) => {
  await login(page, UNENTITLED_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Vacancy Listings", exact: true }).click();
  await expect(page.getByText("Leasing isn't enabled for your plan")).toBeVisible();
  await expectNoAxeViolations(page);
});
