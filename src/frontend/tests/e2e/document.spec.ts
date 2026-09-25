import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteDocumentDataForTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 3 exit criterion's other half: BDR-SEC-007's upload pipeline, exercised
// against the real LocalDiskFileStorage/NoOpFileScanner (no mocking) — a document clears CLEAN
// almost immediately since the stub scanner never actually blocks.
const TENANT_ID = "88888888-8888-8888-8888-888888888888";
const TENANT_ADMIN_USERNAME = `e2e-doc-admin-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
});

test.afterAll(async () => {
  await deleteDocumentDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME]);
});

async function login(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(TENANT_ADMIN_USERNAME);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("BDR-DOC-001/BDR-SEC-007: a Tenant Admin uploads a document and it clears to CLEAN", async ({
  page,
}) => {
  await login(page);

  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await page.getByRole("button", { name: "Upload document" }).click();

  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Document type").click();
  await page.getByRole("option", { name: "Lease" }).click();

  await dialog.locator('input[type="file"]').setInputFiles({
    name: "lease.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4\n%e2e test content\n"),
  });

  await dialog.getByRole("button", { name: "Upload" }).click();
  await expect(dialog).toBeHidden();

  await expect(page.getByText("lease.pdf")).toBeVisible();
  await expect(page.getByText("CLEAN")).toBeVisible();
});

test("BDR-SEC-007: an unrecognized file type is rejected before it's ever stored", async ({
  page,
}) => {
  await login(page);

  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await page.getByRole("button", { name: "Upload document" }).click();

  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Document type").click();
  await page.getByRole("option", { name: "Other" }).click();

  // An .exe renamed to .pdf — magic-number sniffing, not the extension, is what catches it.
  await dialog.locator('input[type="file"]').setInputFiles({
    name: "fake.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from([0x4d, 0x5a, 0x90, 0x00]),
  });

  await dialog.getByRole("button", { name: "Upload" }).click();
  await expect(page.getByText("Upload failed")).toBeVisible();
});

test("BDR-NFR-007: /admin/documents has no automatically detectable accessibility violations", async ({
  page,
}) => {
  await login(page);
  await page.goto("/admin/documents");
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  expect(results.violations).toEqual([]);
});
