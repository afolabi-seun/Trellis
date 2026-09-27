import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  countOverrideLogEntriesForTenant,
  deleteAiDataForTenant,
  deleteAppUsers,
  deleteDocumentDataForTenant,
  deleteTenantByName,
  deleteWorkflowDataForTenant,
  seedDepartment,
  seedTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 6 exit criterion: BDR-AI-002's extraction review/confirm loop and
// BDR-AI-003/004's routing-suggestion-then-override loop, both against the real NoOpAiProvider
// (fixed, deterministic output — no mocking, same "interface first, provider later" precedent as
// every other external-provider seam in this codebase). The true provider-failure exit criterion
// (BDR-AI-002's "never blocks a workflow") is proven at the backend unit level instead
// (ExtractionServiceTests' ~42s real-resilience-policy test) — there's no fault-injecting stub
// provider reachable from the browser to exercise it here. This spec's honest substitute is the
// STARTER-tenant test below: the upload path must succeed with no Ai gate ever touching it.
const TENANT_ID = "19191919-1919-1919-1919-191919191919";
const TENANT_NAME = `E2E Ai Tenant ${Date.now()}`;
const MAINTENANCE_DEPARTMENT_ID = "29292929-2929-2929-2929-292929292929";
const LEGAL_DEPARTMENT_ID = "39393939-3939-3939-3939-393939393939";
const TENANT_ADMIN_USERNAME = `e2e-ai-admin-${Date.now()}@trellis-platform.test`;

const STARTER_TENANT_ID = "49494949-4949-4949-4949-494949494949";
const STARTER_TENANT_NAME = `E2E Ai Starter Tenant ${Date.now()}`;
const STARTER_ADMIN_USERNAME = `e2e-ai-starter-admin-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenant(TENANT_ID, TENANT_NAME, "PROFESSIONAL");
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartment(MAINTENANCE_DEPARTMENT_ID, TENANT_ID, "Maintenance");
  await seedDepartment(LEGAL_DEPARTMENT_ID, TENANT_ID, "Legal");

  await seedTenant(STARTER_TENANT_ID, STARTER_TENANT_NAME, "STARTER");
  await seedTenantAdmin(STARTER_ADMIN_USERNAME, STARTER_TENANT_ID);
});

test.afterAll(async () => {
  await deleteAiDataForTenant(TENANT_ID);
  await deleteWorkflowDataForTenant(TENANT_ID);
  await deleteDocumentDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME]);
  await deleteTenantByName(TENANT_NAME);

  await deleteDocumentDataForTenant(STARTER_TENANT_ID);
  await deleteAppUsers([STARTER_ADMIN_USERNAME]);
  await deleteTenantByName(STARTER_TENANT_NAME);
});

async function login(page: import("@playwright/test").Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("BDR-AI-001/002: uploading a lease triggers extraction, and a corrected field persists on confirm", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await page.getByRole("button", { name: "Upload document" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Document type").click();
  await page.getByRole("option", { name: "Lease" }).click();
  await dialog.locator('input[type="file"]').setInputFiles({
    name: "lease.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4\n%e2e ai test content\n"),
  });
  await dialog.getByRole("button", { name: "Upload" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText("CLEAN")).toBeVisible();

  await page.getByRole("link", { name: "Extraction review", exact: true }).click();
  await expect(page.getByText("EXTRACTED")).toBeVisible();
  await page.getByRole("link", { name: "Review", exact: true }).click();

  const detailResults = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(detailResults.violations).toEqual([]);

  await expect(page.getByLabel("PartyName")).toHaveValue("Placeholder Tenant Ltd");
  await page.getByLabel("PartyName").fill("Acme Corrected Ltd");
  await page.getByRole("button", { name: "Confirm" }).click();
  await expect(page.getByText("CONFIRMED")).toBeVisible();

  await page.reload();
  await expect(page.getByLabel("PartyName")).toHaveValue("Acme Corrected Ltd");
});

test("BDR-AI-003/004: a routing suggestion pre-fills a department, and picking a different one logs an override", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Workflow templates", exact: true }).click();
  await page.getByRole("button", { name: "New template" }).click();
  const templateDialog = page.getByRole("dialog");
  await templateDialog.getByLabel("Name", { exact: true }).fill("Ai Routing Review");
  await templateDialog.getByLabel("Request type").fill("MAINTENANCE");
  await templateDialog.getByLabel("Step 1 name").fill("Review");
  await templateDialog.getByLabel("Default department").click();
  await page.getByRole("option", { name: "Legal" }).click();
  await templateDialog.getByRole("button", { name: "Create template" }).click();
  await expect(templateDialog).toBeHidden();

  await page.getByRole("link", { name: "Workflow instances", exact: true }).click();
  await page.getByRole("button", { name: "New instance" }).click();
  const instanceDialog = page.getByRole("dialog");
  await instanceDialog.getByLabel("Template").click();
  await page.getByRole("option", { name: /Ai Routing Review/ }).click();
  await instanceDialog
    .getByLabel("Request description")
    .fill("Leaking pipe under the kitchen sink");
  await instanceDialog.getByRole("button", { name: "Get AI suggestion" }).click();

  // NoOpAiProvider matches a department named "Maintenance" at 82% confidence.
  await expect(instanceDialog.getByText(/AI suggests: Maintenance/)).toBeVisible();
  await expect(instanceDialog.getByLabel("Requesting department")).toHaveValue("Maintenance");

  // Deliberately override the suggestion — pick Legal instead of the suggested Maintenance.
  await instanceDialog.getByLabel("Requesting department").click();
  await page.getByRole("option", { name: "Legal" }).click();
  await instanceDialog.getByRole("button", { name: "Create instance" }).click();
  await expect(instanceDialog).toBeHidden();

  await expect
    .poll(() => countOverrideLogEntriesForTenant(TENANT_ID, "ROUTING"), { timeout: 10_000 })
    .toBeGreaterThan(0);
});

test("BDR-AI-002 (tier gate): a STARTER tenant's upload still succeeds with no Ai gate surfaced", async ({
  page,
}) => {
  await login(page, STARTER_ADMIN_USERNAME);

  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await page.getByRole("button", { name: "Upload document" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Document type").click();
  await page.getByRole("option", { name: "Lease" }).click();
  await dialog.locator('input[type="file"]').setInputFiles({
    name: "lease.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4\n%e2e ai starter test content\n"),
  });
  await dialog.getByRole("button", { name: "Upload" }).click();
  await expect(dialog).toBeHidden();

  // The upload itself never knows Ai tiers exist — it succeeds exactly like any other tenant's.
  await expect(page.getByText("CLEAN")).toBeVisible();
});

for (const path of ["/admin/extraction-jobs"]) {
  test(`BDR-NFR-007: ${path} has no automatically detectable accessibility violations`, async ({
    page,
  }) => {
    await login(page, TENANT_ADMIN_USERNAME);
    await page.goto(path);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);
  });
}
