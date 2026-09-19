import { createTheme, type MantineColorsTuple } from "@mantine/core";

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

export const theme = createTheme({
  primaryColor: "sage",
  colors: { navy, sage },
  fontFamily: "'IBM Plex Sans', system-ui, sans-serif",
  headings: { fontFamily: "'DM Sans', system-ui, sans-serif" },
});
