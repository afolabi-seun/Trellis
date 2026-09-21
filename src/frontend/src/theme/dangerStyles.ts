// BDR-NFR-007 (WCAG 2.1 AA) — Mantine's "light"/"subtle" variant color resolver doesn't respect
// an explicit "red.8" shade override the way Badge's filled variant does (found empirically via
// tests/e2e's axe-core scan: the rendered text stayed a ~3.1-3.9:1 red regardless of the shade
// passed through the `color` prop). Styles API override on the `label` part bypasses variant
// resolution entirely — #a61e1e measures ~7.4:1 against a pale-red Alert background. Shared here
// (originally duplicated across ResetPasswordPage.tsx and TenantsListPage.tsx) since every
// destructive-action button/alert needs the identical override.
export const dangerAlertStyles = { label: { color: "#a61e1e" } };
export const dangerButtonStyles = { label: { color: "#a61e1e" } };
