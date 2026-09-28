import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteBillingDataForTenant,
  deleteLeasingDataForTenant,
  deletePropertyAssetDataForTenant,
  deleteTenantByName,
  deleteTenantDomainsForTenant,
  seedModuleEntitlement,
  seedOwner,
  seedProperty,
  seedTenant,
  seedTenantAdmin,
  seedUnitAsset,
  seedVacancyListing,
  seedVerifiedDomain,
  SEED_PASSWORD,
  tenantSlugFor,
} from "./support/dbSeed";

// Public URLs identify a tenant by slug, never its GUID (BDR-SEC-004), and an ENTERPRISE tenant can
// serve them from its own custom domain. Real DNS verification can't be driven from a test (it is
// unit- and smoke-tested), so a VERIFIED domain is seeded directly; everything else — the settings UI,
// slug URLs, host resolution, dynamic CORS, and a browser actually loading the SPA from the tenant's own
// hostname (playwright.config maps *.test to 127.0.0.1) — runs against the real API. UUIDs are distinct
// from every other spec's (parallel workers share one Postgres).
const ENTERPRISE_TENANT_ID = "e0e0e0e0-e0e0-e0e0-e0e0-e0e0e0e0e0e0";
const ENTERPRISE_NAME = `E2E Domain Tenant ${Date.now()}`;
const OWNER_ID = "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1";
const PROPERTY_ID = "e3e3e3e3-e3e3-e3e3-e3e3-e3e3e3e3e3e3";
const UNIT_ID = "e4e4e4e4-e4e4-e4e4-e4e4-e4e4e4e4e4e4";
const LISTING_ID = "e6e6e6e6-e6e6-e6e6-e6e6-e6e6e6e6e6e6";
const STARTER_TENANT_ID = "f0f0f0f0-f0f0-f0f0-f0f0-f0f0f0f0f0f0";
const STARTER_NAME = `E2E Domain Starter Tenant ${Date.now()}`;
const ENTERPRISE_ADMIN = `e2e-domain-admin-${Date.now()}@trellis-platform.test`;
const STARTER_ADMIN = `e2e-domain-starter-${Date.now()}@trellis-platform.test`;
const CUSTOM_HOST = "acme-e2e.test";
const REGISTERED_DOMAIN = `e2e-${Date.now()}.example.com`;
const LISTING_DESCRIPTION = "Bright corner unit with a balcony";
const API_URL = "http://localhost:5080";
const SLUG = tenantSlugFor(ENTERPRISE_TENANT_ID);

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  await seedTenant(ENTERPRISE_TENANT_ID, ENTERPRISE_NAME, "ENTERPRISE");
  await seedTenantAdmin(ENTERPRISE_ADMIN, ENTERPRISE_TENANT_ID);
  await seedModuleEntitlement(ENTERPRISE_TENANT_ID, "LEASING", "ACTIVE");
  await seedOwner(OWNER_ID, ENTERPRISE_TENANT_ID, "Dee Owner", "dee.owner@example.com");
  await seedProperty(PROPERTY_ID, ENTERPRISE_TENANT_ID, OWNER_ID, "9 Domain Drive");
  await seedUnitAsset(UNIT_ID, ENTERPRISE_TENANT_ID, PROPERTY_ID, "Unit 12C");
  await seedVacancyListing(LISTING_ID, ENTERPRISE_TENANT_ID, UNIT_ID, LISTING_DESCRIPTION);
  await seedVerifiedDomain(ENTERPRISE_TENANT_ID, CUSTOM_HOST);

  await seedTenant(STARTER_TENANT_ID, STARTER_NAME, "STARTER");
  await seedTenantAdmin(STARTER_ADMIN, STARTER_TENANT_ID);
});

test.afterAll(async () => {
  await deleteTenantDomainsForTenant(ENTERPRISE_TENANT_ID);
  await deleteLeasingDataForTenant(ENTERPRISE_TENANT_ID);
  await deletePropertyAssetDataForTenant(ENTERPRISE_TENANT_ID);
  await deleteBillingDataForTenant(ENTERPRISE_TENANT_ID);
  await deleteAppUsers([ENTERPRISE_ADMIN, STARTER_ADMIN]);
  await deleteTenantByName(ENTERPRISE_NAME);
  await deleteTenantByName(STARTER_NAME);
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

test("BDR-SEC-004: a listing is served by slug, and neither a GUID nor an unknown slug reveals anything", async ({
  page,
}) => {
  await page.goto(`/vacancies/${SLUG}`);
  await expect(page.getByRole("heading", { name: "Available units" })).toBeVisible();
  await expect(page.getByText(ENTERPRISE_NAME)).toBeVisible();
  await expect(page.getByRole("link", { name: "Unit 12C" })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.goto(`/vacancies/${ENTERPRISE_TENANT_ID}`);
  await expect(page.getByText("These listings are unavailable right now.")).toBeVisible();

  await page.goto("/vacancies/no-such-tenant");
  await expect(page.getByText("These listings are unavailable right now.")).toBeVisible();
});

test("BDR-SEC-004: the public API never returns the tenant id", async ({ request }) => {
  const profile = await request.get(`${API_URL}/api/v1/public/tenants/${SLUG}`);
  expect(profile.status()).toBe(200);
  const body = await profile.text();
  expect(body).toContain(ENTERPRISE_NAME);
  expect(body).not.toContain(ENTERPRISE_TENANT_ID);

  const listings = await request.get(`${API_URL}/api/v1/public/tenants/${SLUG}/vacancy-listings`);
  expect(listings.status()).toBe(200);
  expect(await listings.text()).not.toContain(ENTERPRISE_TENANT_ID);

  const byGuid = await request.get(
    `${API_URL}/api/v1/public/tenants/${ENTERPRISE_TENANT_ID}/vacancy-listings`,
  );
  expect(byGuid.status()).toBe(404);
});

test("BDR-SEC-004: a verified custom domain resolves to its tenant, and only it is allowed by CORS", async ({
  request,
}) => {
  const resolved = await request.get(
    `${API_URL}/api/v1/public/domain-resolution?host=${CUSTOM_HOST}`,
  );
  expect(resolved.status()).toBe(200);
  expect(await resolved.text()).toContain(SLUG);

  const unknown = await request.get(`${API_URL}/api/v1/public/domain-resolution?host=unknown.test`);
  expect(unknown.status()).toBe(404);

  const allowed = await request.fetch(`${API_URL}/api/v1/public/tenants/${SLUG}`, {
    method: "OPTIONS",
    headers: {
      Origin: `http://${CUSTOM_HOST}:4173`,
      "Access-Control-Request-Method": "GET",
    },
  });
  expect(allowed.headers()["access-control-allow-origin"]).toBe(`http://${CUSTOM_HOST}:4173`);

  const refused = await request.fetch(`${API_URL}/api/v1/public/tenants/${SLUG}`, {
    method: "OPTIONS",
    headers: { Origin: "http://evil.test:4173", "Access-Control-Request-Method": "GET" },
  });
  expect(refused.headers()["access-control-allow-origin"]).toBeUndefined();
});

test("BDR-SEC-004: opened on the tenant's own domain, the app shows that tenant's listings — no slug in the URL", async ({
  page,
}) => {
  await page.goto(`http://${CUSTOM_HOST}:4173/`);
  await expect(page.getByRole("heading", { name: "Available units" })).toBeVisible();
  await expect(page.getByText(ENTERPRISE_NAME)).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole("link", { name: "Unit 12C" }).click();
  await expect(page).toHaveURL(new RegExp(`http://${CUSTOM_HOST}:4173/listings/${LISTING_ID}$`));
  await expect(page.getByRole("heading", { name: "Unit 12C" })).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole("link", { name: "Back to all units" }).click();
  await expect(page).toHaveURL(`http://${CUSTOM_HOST}:4173/`);
});

test("BDR-SEC-004: an unrecognised host shows nothing", async ({ page }) => {
  await page.goto("http://stranger.test:4173/");
  await expect(page.getByText("These listings are unavailable right now.")).toBeVisible();
});

test("BDR-SEC-004: an ENTERPRISE tenant registers a domain, is shown the DNS record, and cannot verify without it", async ({
  page,
}) => {
  await login(page, ENTERPRISE_ADMIN);
  await page.getByRole("link", { name: "Settings", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/settings$/);

  await page.getByLabel("Domain").fill(REGISTERED_DOMAIN);
  await page.getByRole("button", { name: "Add domain" }).click();

  const row = page.getByRole("row", { name: new RegExp(REGISTERED_DOMAIN.replace(/\./g, "\\.")) });
  await expect(row).toBeVisible();
  await expect(row.getByText("Pending", { exact: true })).toBeVisible();
  await expect(row.getByText(`_trellis-verify.${REGISTERED_DOMAIN}`)).toBeVisible();
  await expect(row.getByText(/^trellis-verify=/)).toBeVisible();
  await expectNoAxeViolations(page);

  // No TXT record exists for this made-up domain: verification fails closed.
  await row.getByRole("button", { name: "Verify" }).click();
  await expect(page.getByText(/couldn't find the DNS record yet/)).toBeVisible({ timeout: 30000 });
  await expect(row.getByText("Pending", { exact: true })).toBeVisible();

  page.once("dialog", (d) => d.accept());
  await row.getByRole("button", { name: "Remove" }).click();
  await expect(row).toHaveCount(0);
});

test("BDR-SEC-004: an invalid domain is rejected, by the form and by the server", async ({
  page,
}) => {
  await login(page, ENTERPRISE_ADMIN);
  await page.goto("/admin/settings");

  await page.getByLabel("Domain").fill("localhost");
  await page.getByRole("button", { name: "Add domain" }).click();
  await expect(page.getByText("Enter a bare domain like listings.example.com.")).toBeVisible();

  // An IP address gets past the form's shape check but the server refuses it.
  await page.getByLabel("Domain").fill("127.0.0.1");
  await page.getByRole("button", { name: "Add domain" }).click();
  await expect(
    page.getByText("That isn't a valid domain, or it belongs to the platform itself."),
  ).toBeVisible();
});

test("BDR-SEC-004: a tenant not on the Enterprise plan sees an upsell instead of registering a domain", async ({
  page,
}) => {
  await login(page, STARTER_ADMIN);
  await page.goto("/admin/settings");

  await page.getByLabel("Domain").fill("listings.starter-example.com");
  await page.getByRole("button", { name: "Add domain" }).click();

  await expect(page.getByText("Custom domains aren't enabled for your plan")).toBeVisible();
  await expectNoAxeViolations(page);
});
