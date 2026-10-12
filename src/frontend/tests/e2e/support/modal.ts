import { expect, type Page } from "@playwright/test";

/**
 * Mantine fades a Modal's content and overlay in over ~200ms. Scanning with axe mid-transition
 * blends the half-transparent content with the overlay and reports a false color-contrast failure
 * (seen intermittently on data-subjects, rentledger and leasing). Wait until both have reached
 * full opacity and no transition is still running inside the modal before scanning.
 */
export async function waitForModalSettled(page: Page): Promise<void> {
  const dialog = page.getByRole("dialog");
  const overlay = page.locator(".mantine-Modal-overlay");
  await expect(dialog).toHaveCSS("opacity", "1");
  await expect(overlay).toHaveCSS("opacity", "1");
  await expect
    .poll(() =>
      page.evaluate(() => {
        const root = document.querySelector(".mantine-Modal-root");
        if (!root) return 0;
        return root.getAnimations({ subtree: true }).filter((a) => a.playState === "running")
          .length;
      }),
    )
    .toBe(0);
}
