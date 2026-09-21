import "@testing-library/jest-dom/vitest";
import { vi } from "vitest";

// Unit tests never make a real network call (testing_strategy.md's Unit layer is mocked, no
// live dependency) — AuthProvider's mount-time silent-refresh would otherwise hit a real `fetch`
// against a backend that isn't running here. Rejecting immediately exercises the same
// "not authenticated" path bootstrapSession()'s own catch already handles.
global.fetch = vi.fn(() => Promise.reject(new Error("network disabled in unit tests")));

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
