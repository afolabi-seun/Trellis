import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteBillingDataForTenant,
  deleteTenantByName,
  seedInvoiceWithLineItem,
  seedModuleEntitlement,
  seedSubscription,
  seedSuperAdmin,
  seedTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 7 exit criterion: a real invoice, a Tenant Admin's own view of it, a Super
// Admin's cross-tenant view, and BDR-BIL-009's module-entitlement 403/upsell split. A full billing
// cycle can't be driven through the UI alone (the daily UsageSnapshotBackgroundService poll isn't
// something a test can trigger on demand), so the subscription/invoice/entitlement state is
// seeded directly, mirroring seedWorkflowInstance's own precedent — login, session, and RLS all
// still have to actually work end to end.
const TENANT_ID = "59595959-5959-5959-5959-595959595959";
const TENANT_NAME = `E2E Billing Tenant ${Date.now()}`;
const SUBSCRIPTION_ID = "69696969-6969-6969-6969-696969696969";
const INVOICE_ID = "79797979-7979-7979-7979-797979797979";
const TENANT_ADMIN_USERNAME = `e2e-billing-admin-${Date.now()}@trellis-platform.test`;

const DISABLED_TENANT_ID = "89898989-8989-8989-8989-898989898989";
const DISABLED_TENANT_NAME = `E2E Billing Disabled Tenant ${Date.now()}`;
const DISABLED_TENANT_ADMIN_USERNAME = `e2e-billing-disabled-admin-${Date.now()}@trellis-platform.test`;

const SUPER_ADMIN_USERNAME = `e2e-billing-super-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedSubscription(SUBSCRIPTION_ID, TENANT_ID, "ACTIVE");
  await seedInvoiceWithLineItem(INVOICE_ID, TENANT_ID, SUBSCRIPTION_ID, 25);
  await seedModuleEntitlement(TENANT_ID, "INSPECTION", "ACTIVE");

  await seedTenant(DISABLED_TENANT_ID, DISABLED_TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(DISABLED_TENANT_ADMIN_USERNAME, DISABLED_TENANT_ID);
  await seedModuleEntitlement(DISABLED_TENANT_ID, "INSPECTION", "DISABLED");

  await seedSuperAdmin(SUPER_ADMIN_USERNAME);
});

test.afterAll(async () => {
  await deleteBillingDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME]);
  await deleteTenantByName(TENANT_NAME);

  await deleteBillingDataForTenant(DISABLED_TENANT_ID);
  await deleteAppUsers([DISABLED_TENANT_ADMIN_USERNAME]);
  await deleteTenantByName(DISABLED_TENANT_NAME);

  await deleteAppUsers([SUPER_ADMIN_USERNAME]);
});

// Every caller relies on login() having fully completed — including the SPA's in-memory auth
// state — before proceeding; a bare click() only waits for the click event to dispatch, not for
// the async login request or its redirect, so each role-specific landing URL is awaited here once
// rather than left to chance at each call site.
async function login(page: import("@playwright/test").Page, username: string, landingUrl: RegExp) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(landingUrl);
}

test("BDR-BIL-006: a Tenant Admin views their Billing settings page and sees their invoice history", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);

  await page.getByRole("link", { name: "Billing", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/billing$/);

  await expect(page.getByText("ACTIVE")).toBeVisible();
  await expect(page.getByText("$25.00")).toBeVisible();
  await expect(page.getByText("PAID")).toBeVisible();
});

test("BDR-BIL-007: a Super Admin views the platform billing page and sees the tenant's subscription and invoice", async ({
  page,
}) => {
  await login(page, SUPER_ADMIN_USERNAME, /\/platform\/tenants$/);

  await page.getByText("Billing →").click();
  await expect(page).toHaveURL(/\/platform\/billing$/);

  await expect(page.getByText(TENANT_ID).first()).toBeVisible();
  await expect(page.getByText("$25.00").first()).toBeVisible();
});

test("BDR-BIL-009: a tenant with the Inspection entitlement disabled gets an upsell state, not a bare error", async ({
  page,
}) => {
  await login(page, DISABLED_TENANT_ADMIN_USERNAME, /\/admin\/properties$/);
  await page.goto("/admin/inspections");

  await expect(page.getByText("Inspections isn't enabled for your plan")).toBeVisible();
  await expect(page.getByRole("button", { name: "Schedule inspection" })).toHaveCount(0);
});

test("BDR-BIL-009: a tenant with the Inspection entitlement active still reaches the page normally", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);
  await page.goto("/admin/inspections");

  await expect(page.getByRole("button", { name: "Schedule inspection" })).toBeVisible();
  await expect(page.getByText("Inspections isn't enabled for your plan")).toHaveCount(0);
});

test("BDR-NFR-007: /admin/billing has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME, /\/admin\/properties$/);
  await page.goto("/admin/billing");
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});

test("BDR-NFR-007: /platform/billing has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page, SUPER_ADMIN_USERNAME, /\/platform\/tenants$/);
  await page.goto("/platform/billing");
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});
