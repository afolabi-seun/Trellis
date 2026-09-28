import { createTheme, InputWrapper, Modal, type MantineColorsTuple } from "@mantine/core";

// frontend_architecture.md, "Design System" — the pitch deck's brand palette as the app's
// starting theme tokens. Mantine needs a 10-shade tuple per named color; these are a first
// pass around the two brand colors, not yet contrast-checked against WCAG 2.1 AA
// (frontend_architecture.md's Accessibility section flags that check as still outstanding
// before the palette is treated as final).
const navy: MantineColorsTuple = [
  "#eef1f6",
  "#d7dde8",
  "#b3bfd4",
  "#8b9dbe",
  "#6a80ab",
  "#556da0",
  "#4a639b",
  "#3c5288",
  "#2f4270",
  "#1e293b",
];

const sage: MantineColorsTuple = [
  "#f1f6f3",
  "#e0ebe4",
  "#c1d6c9",
  "#9fc0ac",
  "#82ad93",
  "#6da081",
  "#639875",
  "#528062",
  "#456c53",
  "#5b8a72",
];

// BDR-NFR-007 (WCAG 2.1 AA) — an ACTIVE-status Badge needs white-text-on-filled contrast, and
// even Mantine's built-in green's darkest shade (green.9, #2b8a3e) measures only 4.36:1, just
// short of the 4.5:1 floor (found live, via tests/e2e's axe-core scan). A custom tuple, same
// mechanism already proven for navy/sage: Badge's filled variant respects an explicit
// "success.8" shade request. #1e7e34 at index 8 measures ~5.14:1.
const success: MantineColorsTuple = [
  "#eafbf0",
  "#d1f4dc",
  "#a6e8bd",
  "#78db9d",
  "#52d183",
  "#3bc973",
  "#2ec369",
  "#20a856",
  "#1e7e34",
  "#175e27",
];

export const theme = createTheme({
  primaryColor: "sage",
  // BDR-NFR-007 (WCAG 2.1 AA) — Mantine's default primaryShade (6) is this palette's
  // "#639875", which measures 3.34:1 white-on-sage, below the 4.5:1 floor for normal text
  // (found by the Playwright/axe-core suite in tests/e2e, the check this file's own earlier
  // comment flagged as still outstanding). Shade 8 ("#456c53") measures ~5.96:1.
  primaryShade: 8,
  colors: { navy, sage, success },
  fontFamily: "'IBM Plex Sans', system-ui, sans-serif",
  headings: { fontFamily: "'DM Sans', system-ui, sans-serif" },
  components: {
    // BDR-NFR-007 — Mantine's default field description is gray-6-ish (#868e96) at 3.32:1 on white,
    // below the 4.5:1 floor (found by leasing.spec.ts's axe scan). gray.8 (#343a40) clears it, and
    // fixing it here covers every current and future field description instead of one page's.
    InputWrapper: InputWrapper.extend({
      styles: { description: { color: "var(--mantine-color-gray-8)" } },
    }),
    // BDR-NFR-007 — Mantine's modal close button is an icon with no accessible name (axe:
    // "button-name", critical), found by rentledger.spec.ts scanning an open modal. Set once here
    // so every current and future Modal gets it.
    Modal: Modal.extend({
      defaultProps: { closeButtonProps: { "aria-label": "Close" } },
    }),
  },
});
