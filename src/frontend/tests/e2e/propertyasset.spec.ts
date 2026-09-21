import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deletePropertyAssetDataForTenant,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 2 exit criterion, verbatim: "A Tenant Admin can create an owner, a
// property, and its units end-to-end through the UI — this is the first phase a design partner
// could plausibly click through unassisted." This spec is that criterion, executable.
const TENANT_ID = "55555555-5555-5555-5555-555555555555";
const TENANT_ADMIN_USERNAME = `e2e-tenant-admin-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
});

test.afterAll(async () => {
  await deletePropertyAssetDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME]);
});

async function login(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(TENANT_ADMIN_USERNAME);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("BDR-OWN-001/BDR-AST-001/BDR-AST-002: a Tenant Admin creates an owner, a property, and a unit end-to-end", async ({
  page,
}) => {
  await login(page);

  // Step 1: no owners yet, "Add property" is disabled — the UI shouldn't let a Tenant Admin
  // create a property with nothing to attach it to.
  await expect(page.getByRole("button", { name: "Add property" })).toBeDisabled();

  // Step 2: create an owner.
  await page.getByRole("link", { name: "Owners", exact: true }).click();
  await page.getByRole("button", { name: "Add owner" }).click();
  await page.getByLabel("Name").fill("Acme Holdings");
  await page.getByLabel("Email").fill("contact@acme-holdings.test");
  await page.getByRole("dialog").getByRole("button", { name: "Add owner", exact: true }).click();
  await expect(page.getByText("Acme Holdings")).toBeVisible();

  // Step 3: create a property owned by it, including the BDR-AST-002 attributes.
  await page.getByRole("link", { name: "Properties", exact: true }).click();
  await page.getByRole("button", { name: "Add property" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Owner").click();
  await page.getByRole("option", { name: "Acme Holdings" }).click();
  await dialog.getByLabel("Address").fill("123 Main St");
  await dialog.getByLabel("Property type").click();
  await page.getByRole("option", { name: "Mixed use" }).click();
  await dialog.getByLabel("Size (sq ft)").fill("5000");
  await dialog.getByLabel("Acquisition value").fill("500000");
  await dialog.getByRole("button", { name: "Add property", exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText("123 Main St")).toBeVisible();

  // Step 4: open the property and add a unit — the exit criterion's final step.
  await page.getByRole("link", { name: "123 Main St" }).click();
  await expect(page).toHaveURL(/\/admin\/properties\/[0-9a-f-]+$/);
  await page.getByRole("button", { name: "Add unit / asset" }).click();
  await page.getByLabel("Label").fill("Unit 4B");
  await page.getByLabel("Size (sq ft)").fill("800");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Add unit / asset", exact: true })
    .click();
  await expect(page.getByText("Unit 4B")).toBeVisible();
});

test("BDR-SEC-002: archiving an owner with an active property is rejected, not silently allowed", async ({
  page,
}) => {
  await login(page);

  await page.getByRole("link", { name: "Owners", exact: true }).click();
  // Must be registered before the click — confirm() can fire synchronously inside the button's
  // onClick handler, and Playwright auto-dismisses (confirm() -> false) any dialog with no
  // handler registered yet, which silently skipped the archive call entirely on the first run.
  page.once("dialog", (d) => d.accept());
  await page.getByRole("button", { name: "Archive" }).click();
  await expect(page.getByText("Could not archive owner")).toBeVisible();
});

for (const path of ["/admin/properties", "/admin/owners"]) {
  test(`BDR-NFR-007: ${path} has no automatically detectable accessibility violations`, async ({
    page,
  }) => {
    await login(page);
    await page.goto(path);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);
  });
}
