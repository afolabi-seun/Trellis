import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import {
  deleteAppUsers,
  deleteWorkflowDataForTenant,
  seedDepartment,
  seedDepartmentStaff,
  seedTenantAdmin,
  SEED_PASSWORD,
} from "./support/dbSeed";

// roadmap.md's Phase 3 exit criterion, verbatim: "a workflow instance can be created, routed to
// the correct department, and approved by a Department Staff user with the right role." This
// spec is that criterion, executable — the department is seeded directly (not through the UI) so
// its id is known ahead of time for the Department Staff user's department_ids.
const TENANT_ID = "66666666-6666-6666-6666-666666666666";
const DEPARTMENT_ID = "77777777-7777-7777-7777-777777777777";
const TENANT_ADMIN_USERNAME = `e2e-wf-admin-${Date.now()}@trellis-platform.test`;
const DEPARTMENT_STAFF_USERNAME = `e2e-wf-staff-${Date.now()}@trellis-platform.test`;

test.beforeAll(async () => {
  await seedTenantAdmin(TENANT_ADMIN_USERNAME, TENANT_ID);
  await seedDepartment(DEPARTMENT_ID, TENANT_ID, "Facilities");
  await seedDepartmentStaff(DEPARTMENT_STAFF_USERNAME, TENANT_ID, [DEPARTMENT_ID]);
});

test.afterAll(async () => {
  await deleteWorkflowDataForTenant(TENANT_ID);
  await deleteAppUsers([TENANT_ADMIN_USERNAME, DEPARTMENT_STAFF_USERNAME]);
});

async function login(page: import("@playwright/test").Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(SEED_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/properties$/);
}

test("BDR-WF-001/002/003: a Tenant Admin builds a workflow template, creates an instance, and a Department Staff user with the right department approves it", async ({
  page,
}) => {
  await login(page, TENANT_ADMIN_USERNAME);

  // Step 1: the pre-seeded department is visible.
  await page.getByRole("link", { name: "Departments", exact: true }).click();
  await expect(page.getByText("Facilities")).toBeVisible();

  // Step 2: build a one-step workflow template, routed to Facilities by default (no conditional
  // rules needed — the exit criterion only requires a correct route, not a matched rule).
  await page.getByRole("link", { name: "Workflow templates", exact: true }).click();
  await page.getByRole("button", { name: "New template" }).click();
  const templateDialog = page.getByRole("dialog");
  await templateDialog.getByLabel("Name", { exact: true }).fill("Maintenance Approval");
  await templateDialog.getByLabel("Request type").fill("MAINTENANCE");
  await templateDialog.getByLabel("Step 1 name").fill("Facilities Review");
  await templateDialog.getByLabel("Default department").click();
  await page.getByRole("option", { name: "Facilities" }).click();
  await templateDialog.getByRole("button", { name: "Create template" }).click();
  await expect(templateDialog).toBeHidden();
  await expect(page.getByText("Maintenance Approval")).toBeVisible();

  // Step 3: create an instance from that template.
  await page.getByRole("link", { name: "Workflow instances", exact: true }).click();
  await page.getByRole("button", { name: "New instance" }).click();
  const instanceDialog = page.getByRole("dialog");
  await instanceDialog.getByLabel("Template").click();
  await page.getByRole("option", { name: /Maintenance Approval/ }).click();
  await instanceDialog.getByRole("button", { name: "Create instance" }).click();
  await expect(instanceDialog).toBeHidden();
  // The instance's own status and its (only) step's status both read IN_PROGRESS right after
  // creation — .first() rather than asserting a specific one of the two identical badges.
  await expect(page.getByText("IN_PROGRESS").first()).toBeVisible();
  await expect(page.getByText("Facilities Review")).toBeVisible();

  // Step 4: the Department Staff user (a Facilities member) approves it.
  await page.getByRole("button", { name: "Sign out" }).click();
  await login(page, DEPARTMENT_STAFF_USERNAME);
  await page.getByRole("link", { name: "Workflow instances", exact: true }).click();
  await page.getByRole("button", { name: "Approve" }).click();
  // Same duplicate-badge reasoning as the IN_PROGRESS assertion above — the single step's own
  // status and the instance's overall status both read APPROVED once the last step clears.
  await expect(page.getByText("APPROVED").first()).toBeVisible();
});

for (const path of [
  "/admin/departments",
  "/admin/workflow-templates",
  "/admin/workflow-instances",
]) {
  test(`BDR-NFR-007: ${path} has no automatically detectable accessibility violations`, async ({
    page,
  }) => {
    await login(page, TENANT_ADMIN_USERNAME);
    await page.goto(path);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);
  });
}
