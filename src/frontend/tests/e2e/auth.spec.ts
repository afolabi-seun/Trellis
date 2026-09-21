import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// frontend_architecture.md's Testing section: test names tagged with BDR IDs where a test
// verifies a specific requirement, axe-core run against the same suite rather than a separate
// manual pass. None of these need the seeded-user flows in platform-tenants.spec.ts — every
// case here is reachable without ever having logged in.

test("BDR-SEC-004: an unauthenticated visit to any admin route redirects to /login", async ({
  page,
}) => {
  await page.goto("/platform");
  await expect(page).toHaveURL(/\/login$/);
});

test("BDR-SEC-002: a wrong username/password shows a generic error, not an account-specific one", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Username").fill("nobody-with-this-username@trellis-platform.test");
  await page.getByLabel("Password").fill("whatever-password");
  await page.getByRole("button", { name: "Sign in" }).click();

  // security_guide.md's existence-non-disclosure: the message never confirms or denies the
  // account exists — it's the same TRL-E-AUT-1000 generic message whether or not the username
  // is real (ServiceResult.Forbidden's own fallback, error_handling.md's Forbidden mapping).
  await expect(page.getByText("Sign-in failed")).toBeVisible();
  await expect(page.getByText("You do not have permission to perform this action.")).toBeVisible();
});

test("BDR-SEC-005: requesting a password reset always shows the same generic success message", async ({
  page,
}) => {
  await page.goto("/forgot-password");
  await page.getByLabel("Username").fill("nobody-with-this-username@trellis-platform.test");
  await page.getByRole("button", { name: "Send reset link" }).click();

  await expect(page.getByTestId("forgot-password-success")).toContainText(
    "If an account with that username exists, a password reset link has been sent.",
  );
});

test("BDR-SEC-005: a reset link with no token shows an actionable error, not a blank form", async ({
  page,
}) => {
  await page.goto("/reset-password");
  await expect(page.getByText("Invalid link")).toBeVisible();
});

for (const path of ["/login", "/forgot-password", "/reset-password"]) {
  test(`BDR-NFR-007: ${path} has no automatically detectable accessibility violations`, async ({
    page,
  }) => {
    await page.goto(path);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);
  });
}
