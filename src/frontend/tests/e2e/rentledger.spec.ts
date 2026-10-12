import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteBillingDataForTenant,
  deletePropertyAssetDataForTenant,
  deleteRentLedgerDataForTenant,
  deleteReportingDataForTenant,
  deleteTenantByName,
  seedDepartmentStaff,
  seedModuleEntitlement,
  seedOwner,
  seedProperty,
  seedTenant,
  seedTenantAdmin,
  seedUnitAsset,
  SEED_PASSWORD,
  getLeasesForUnit,
  getUnitOccupancyStatus,
} from "./support/dbSeed";

const KES_TENANT_ID = "f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1";
const KES_TENANT_NAME = `E2E Rent KES Tenant ${Date.now()}`;
const KES_OWNER_ID = "f5f5f5f5-f5f5-f5f5-f5f5-f5f5f5f5f5f5";
const KES_PROPERTY_ID = "f2f2f2f2-f2f2-f2f2-f2f2-f2f2f2f2f2f2";
const KES_UNIT_ID = "f4f4f4f4-f4f4-f4f4-f4f4-f4f4f4f4f4f4";
const KES_ADMIN_USERNAME = `e2e-rent-kes-admin-${Date.now()}@trellis-platform.test`;

// Phase 10's exit criterion: a staff member records a rent payment against a schedule, an unpaid
// entry past its grace period surfaces as an arrears flag, and both appear correctly in the Owner
// portfolio report. The schedule is created through the real UI with a start date three months
// back, so it is genuinely past its grace period; payments are recorded, voided, and reflected in
// a real generated Owner report. Every screen is asserted to say Trellis records payments made
// elsewhere (BDR-RENT-006). UUIDs are distinct from every other spec's (parallel workers share one
// Postgres).
const TENANT_ID = "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0";
const TENANT_NAME = `E2E Rent Tenant ${Date.now()}`;
const OWNER_ID = "c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1";
const PROPERTY_ID = "c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2";
const UNIT_ID = "c4c4c4c4-c4c4-c4c4-c4c4-c4c4c4c4c4c4";
const UNENTITLED_TENANT_ID = "d0d0d0d0-d0d0-d0d0-d0d0-d0d0d0d0d0d0";
const UNENTITLED_TENANT_NAME = `E2E Rent Unentitled Tenant ${Date.now()}`;
const TENANT_ADMIN_USERNAME = `e2e-rent-admin-${Date.now()}@trellis-platform.test`;
const DEPARTMENT_STAFF_USERNAME = `e2e-rent-staff-${Date.now()}@trellis-platform.test`;
const UNENTITLED_ADMIN_USERNAME = `e2e-rent-unentitled-${Date.now()}@trellis-platform.test`;
const PROPERTY_ADDRESS = "77 Rent Row";
const UNIT_LABEL = "Unit 9Z";
const DISCLAIMER =
  "Trellis records payments made outside the platform. It does not process or hold funds.";

const threeMonthsAgo = (() => {
  const d = new Date();
  d.setUTCMonth(d.getUTCMonth() - 3, 1);
  return d.toISOString().slice(0, 10);
})();
const today = new Date().toISOString().slice(0, 10);
// BDR-LSE-009: the manual lease this schedule starts ends in 20 days — inside "Within 30 days".
const leaseEnd = (() => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + 20);
  return d.toISOString().slice(0, 10);
})();

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartmentStaff(DEPARTMENT_STAFF_USERNAME, TENANT_ID, []);
  await seedModuleEntitlement(TENANT_ID, "RENT_COLLECTION", "ACTIVE");
  await seedOwner(OWNER_ID, TENANT_ID, "Rae Owner", "rae.owner@example.com");
  await seedProperty(PROPERTY_ID, TENANT_ID, OWNER_ID, PROPERTY_ADDRESS);
  await seedUnitAsset(UNIT_ID, TENANT_ID, PROPERTY_ID, UNIT_LABEL);

  await seedTenant(UNENTITLED_TENANT_ID, UNENTITLED_TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(UNENTITLED_ADMIN_USERNAME, UNENTITLED_TENANT_ID);

  await seedTenant(KES_TENANT_ID, KES_TENANT_NAME, "PROFESSIONAL", undefined, "KE");
  await seedTenantAdmin(KES_ADMIN_USERNAME, KES_TENANT_ID);
  await seedModuleEntitlement(KES_TENANT_ID, "RENT_COLLECTION", "ACTIVE");
  await seedOwner(KES_OWNER_ID, KES_TENANT_ID, "Kesi Owner", "kesi.owner@example.com");
  await seedProperty(KES_PROPERTY_ID, KES_TENANT_ID, KES_OWNER_ID, "1 Nairobi Rd");
  await seedUnitAsset(KES_UNIT_ID, KES_TENANT_ID, KES_PROPERTY_ID, "Unit K1");
});

test.afterAll(async () => {
  await deleteRentLedgerDataForTenant(TENANT_ID);
  await deleteReportingDataForTenant(TENANT_ID);
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

  await deleteRentLedgerDataForTenant(KES_TENANT_ID);
  await deletePropertyAssetDataForTenant(KES_TENANT_ID);
  await deleteBillingDataForTenant(KES_TENANT_ID);
  await deleteAppUsers([KES_ADMIN_USERNAME]);
  await deleteTenantByName(KES_TENANT_NAME);
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

async function recordPaymentReceived(
  page: import("@playwright/test").Page,
  amount: string,
  reference?: string,
) {
  await page.getByRole("button", { name: "Record payment received" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Amount received").fill(amount);
  if (reference) {
    await dialog.getByLabel("Reference (e.g. EFT ref)").fill(reference);
  }
  await dialog.getByRole("button", { name: "Save record" }).click();
  await expect(dialog).toBeHidden();
}

test("BDR-RENT-001/003: staff create a schedule for an occupied unit and, three months behind, it is flagged in arrears", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/rent-schedules$/);
  await expect(page.getByText(DISCLAIMER)).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole("button", { name: "New schedule" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Property" }).click();
  await page.getByRole("option", { name: PROPERTY_ADDRESS }).click();
  await dialog.getByRole("textbox", { name: "Unit" }).click();
  await page.getByRole("option", { name: UNIT_LABEL }).click();
  await dialog.getByLabel("Rent per period").fill("1000");
  await dialog.getByLabel("Start date").fill(threeMonthsAgo);
  // BDR-LSE-009: the unit has no lease yet, so the schedule starts one and needs its term.
  await dialog.getByRole("button", { name: "Create schedule" }).click();
  await expect(dialog.getByText("Lease end date is required.")).toBeVisible();
  await dialog.getByLabel("Lease end date").fill(leaseEnd);
  await dialog.getByRole("button", { name: "Create schedule" }).click();

  const row = page.getByRole("row", { name: new RegExp(UNIT_LABEL) });
  await expect(row).toBeVisible();
  await expect(row.getByText("In arrears", { exact: true })).toBeVisible();
  expect(await getUnitOccupancyStatus(UNIT_ID)).toBe("OCCUPIED");
  expect(await getLeasesForUnit(UNIT_ID)).toEqual([
    expect.objectContaining({
      source: "MANUAL",
      status: "ACTIVE",
      termType: "FIXED",
      endDate: leaseEnd,
    }),
  ]);

  // A manual tenancy is a lease like any other: it shows on the Leases page.
  await page.getByRole("link", { name: "Leases", exact: true }).click();
  await page.getByRole("tab", { name: "Within 30 days (1)" }).click();
  await expect(
    page.getByRole("row", { name: new RegExp(UNIT_LABEL) }).getByText("Rent schedule"),
  ).toBeVisible();
  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();

  await page.getByRole("link", { name: "Arrears", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/rent-arrears$/);
  await expect(page.getByText(DISCLAIMER)).toBeVisible();
  await expect(page.getByRole("link", { name: UNIT_LABEL })).toBeVisible();
  await expectNoAxeViolations(page);
});

test("BDR-RENT-002: a staff member records payments received, and paying up clears the arrears flag", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);
  await page.goto("/admin/rent-arrears");
  await page.getByRole("link", { name: UNIT_LABEL }).click();
  await expect(page.getByRole("heading", { name: `Rent — ${UNIT_LABEL}` })).toBeVisible();
  await expect(page.getByText(DISCLAIMER)).toBeVisible();
  await expectNoAxeViolations(page);

  // A partial payment records, but three-plus periods are still unpaid past grace.
  await recordPaymentReceived(page, "1000", "EFT ref 88213");
  await expect(page.getByRole("cell", { name: "EFT ref 88213" })).toBeVisible();
  await expect(page.getByText("In arrears", { exact: true }).first()).toBeVisible();

  await page.getByRole("button", { name: "Record payment received" }).click();
  await expect(page.getByText(/received outside Trellis/)).toBeVisible();
  // Mantine fades the modal in; scanning mid-transition reports a false low contrast.
  await expect(page.getByRole("dialog")).toHaveCSS("opacity", "1");
  await expectNoAxeViolations(page);
  await page.getByRole("dialog").getByRole("button", { name: "Close" }).click();

  // A large payment covers everything due — computed on read, so the flag clears at once.
  await recordPaymentReceived(page, "100000", "EFT ref 99001");
  await expect(page.getByText("Current", { exact: true }).first()).toBeVisible();

  await page.goto("/admin/rent-arrears");
  await expect(page.getByText("No rent schedules are in arrears.")).toBeVisible();
});

test("BDR-RENT-005: the recorded payments and rent status appear in the Owner portfolio report", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Owner reports", exact: true }).click();
  await page.getByRole("textbox", { name: "Owner" }).click();
  await page.getByRole("option", { name: "Rae Owner" }).click();
  await page.getByLabel("Start").fill(today.slice(0, 8) + "01");
  await page.getByLabel("End").fill(today);
  await page.getByRole("button", { name: "Generate report" }).click();
  await expect(page.getByText("READY")).toBeVisible();

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "CSV" }).click();
  const download = await downloadPromise;
  const csvPath = await download.path();
  const fs = await import("node:fs/promises");
  const csv = csvPath ? await fs.readFile(csvPath, "utf-8") : "";

  expect(csv).toContain("Rent schedules current,1");
  expect(csv).toContain("Rent schedules in arrears,0");
  expect(csv).toContain("Rent recorded as received in period (USD),101000.00");
});

test("BDR-RENT-002: a mistaken record is voided, not edited — and the arrears flag returns", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);
  await page.goto("/admin/rent-schedules");
  await page.getByRole("link", { name: UNIT_LABEL }).click();

  await page
    .getByRole("row", { name: /100,000/ })
    .getByRole("button", { name: "Void" })
    .click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Reason").fill("Entered against the wrong unit");
  await dialog.getByRole("button", { name: "Void record" }).click();
  await expect(dialog).toBeHidden();

  await expect(page.getByText("Voided", { exact: true })).toBeVisible();
  await expect(page.getByText("Entered against the wrong unit")).toBeVisible();
  await expect(page.getByText("In arrears", { exact: true }).first()).toBeVisible();

  await page.goto("/admin/rent-arrears");
  await expect(page.getByRole("link", { name: UNIT_LABEL })).toBeVisible();
});

test("BDR-RENT-001: terms can be edited, and an ended schedule accepts no more records", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);
  await page.goto("/admin/rent-schedules");
  await page.getByRole("link", { name: UNIT_LABEL }).click();

  await page.getByRole("button", { name: "Edit terms" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Grace period (days)").fill("10");
  await dialog.getByRole("button", { name: "Save changes" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText(/Grace period: 10 days/)).toBeVisible();
  // BDR-RENT-007: the months already past grace were recorded as received only today, so none were on time.
  await expect(
    page.getByText(
      /Payment timeliness: 0 of \d+ periods paid on time \(0%\) · late periods averaged/,
    ),
  ).toBeVisible();

  // The schedule started its own (MANUAL) lease, so ending it ends the tenancy and asks why (BDR-LSE-011).
  await page.getByRole("button", { name: "End tenancy" }).click();
  const endDialog = page.getByRole("dialog");
  await endDialog.getByLabel("Lease ended on").fill(today);
  await endDialog.getByRole("textbox", { name: "Why did it end?" }).click();
  await page.getByRole("option", { name: "Not renewed" }).click();
  await endDialog.getByRole("textbox", { name: "Who ended it?" }).click();
  await page.getByRole("option", { name: "The lessee" }).click();
  await endDialog.getByRole("button", { name: "End tenancy" }).click();
  await expect(endDialog).toBeHidden();

  await expect(page.getByText("Ended", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Record payment received" })).toHaveCount(0);
  // Ending a manual schedule used to leave its unit OCCUPIED; ending the lease vacates it.
  expect(await getUnitOccupancyStatus(UNIT_ID)).toBe("VACANT");
  expect((await getLeasesForUnit(UNIT_ID)).at(-1)).toMatchObject({
    status: "ENDED",
    endReason: "NON_RENEWAL",
    endedBy: "LESSEE",
  });
});

test("BDR-RENT-001: a DEPARTMENT_STAFF login reaches the rent screens", async ({ page }) => {
  await login(page, DEPARTMENT_STAFF_USERNAME);

  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Rent schedules" })).toBeVisible();
  await expect(page.getByText(DISCLAIMER)).toBeVisible();
});

test("BDR-BIL-009: a tenant without the Rent Collection add-on sees an upsell instead of the module", async ({
  page,
}) => {
  await login(page, UNENTITLED_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();
  await expect(page.getByText("Rent tracking isn't enabled for your plan")).toBeVisible();
  await expectNoAxeViolations(page);

  await page.goto("/admin/rent-arrears");
  await expect(page.getByText("Rent tracking isn't enabled for your plan")).toBeVisible();
});

test("a tenant on a KES market gets KES as the default currency, not a hard-coded USD", async ({
  page,
}) => {
  await login(page, KES_ADMIN_USERNAME);
  await page.getByRole("link", { name: "Rent Schedules", exact: true }).click();

  await page.getByRole("button", { name: "New schedule" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Currency")).toHaveValue("KES");

  await dialog.getByRole("textbox", { name: "Property" }).click();
  await page.getByRole("option", { name: "1 Nairobi Rd" }).click();
  await dialog.getByRole("textbox", { name: "Unit" }).click();
  await page.getByRole("option", { name: "Unit K1" }).click();
  await dialog.getByLabel("Rent per period").fill("50000");
  await dialog.getByLabel("Start date").fill(today);
  await dialog.getByRole("textbox", { name: "Lease term" }).click();
  await page.getByRole("option", { name: "Month-to-month" }).click();
  await dialog.getByRole("button", { name: "Create schedule" }).click();
  await expect(dialog).toBeHidden();

  await expect(page.getByRole("cell", { name: "KES", exact: false }).first()).toBeVisible();
});
