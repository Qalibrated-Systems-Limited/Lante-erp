import { defineConfig } from 'vitest/config'

/**
 * Vitest owns the unit tests under `src/`; Playwright owns the browser tests under `e2e/`
 * and `e2e-live/`.
 *
 * Without this, vitest's default glob picks up the Playwright `*.spec.js` files, tries to
 * run them in node, and fails on `@playwright/test` imports — 96 passing unit tests still
 * report as a failed run. Keeping the two runners to explicit, non-overlapping directories
 * avoids that, and makes `npm test` safe to wire into CI on its own.
 */
export default defineConfig({
  test: {
    include: ['src/**/*.{test,spec}.{js,jsx,ts,tsx}'],
    exclude: ['node_modules/**', 'dist/**', 'e2e/**', 'e2e-live/**'],
    environment: 'node',
  },
})
