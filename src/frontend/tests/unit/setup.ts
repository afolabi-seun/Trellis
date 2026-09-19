import "@testing-library/jest-dom/vitest";

// jsdom doesn't implement matchMedia — Mantine's color-scheme detection needs it.
// Standard testing-library/jsdom polyfill, not app behavior under test.
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
