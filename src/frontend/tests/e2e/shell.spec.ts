import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// BDR-NFR-007 (WCAG 2.1 AA, real as of BRD v0.4) — an automated scan on every page under
// test, per frontend_architecture.md's Testing section, starting with the placeholder shell.
test("the app shell has no automatically detectable accessibility violations", async ({ page }) => {
  await page.goto("/");

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();

  expect(results.violations).toEqual([]);
});
