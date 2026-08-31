import { defineConfig, devices } from '@playwright/test'

/**
 * Two tiers of browser test, and only one of them belongs in CI.
 *
 * TIER A — `e2e/` — hermetic. Every backend call is intercepted with page.route(), so
 *   these need no server, no credentials and no network. They run on every push, are
 *   deterministic, and test what the frontend itself owns: routing, auth guards,
 *   permission gating, error handling, and what the user actually sees.
 *
 * TIER B — `e2e-live/` — runs against a real backend. NOT part of CI. It needs
 *   credentials, and this project has no staging environment (see #190), so the only
 *   real backend available is production. Pointing CI at it would mean shipping
 *   production credentials to the runner, hammering the live box on every push, failing
 *   whenever that single node is down, and — for any test that posts a journal — writing
 *   real ledger rows on every run. Run these by hand, against a disposable tenant, with
 *   E2E_LIVE=1 and credentials in the environment.
 *
 * Tier A drives a production build via `vite preview` rather than the dev server: it is
 * what actually ships, and it starts faster and more deterministically than HMR.
 */
const LIVE = process.env.E2E_LIVE === '1'

export default defineConfig({
  testDir: LIVE ? './e2e-live' : './e2e',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],

  use: {
    baseURL: LIVE ? (process.env.E2E_BASE_URL ?? 'https://lante.africa') : 'http://127.0.0.1:4173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // Tier A serves the built app locally. Tier B targets an already-running deployment,
  // so no server is started.
  // `--host 127.0.0.1` is load-bearing: by default `vite preview` binds only to `localhost`,
  // which on this machine (and commonly on CI images) resolves to IPv6 ::1, so polling
  // 127.0.0.1 never connects and the server appears to time out while running perfectly.
  webServer: LIVE ? undefined : {
    command: 'npm run build && npm run preview -- --port 4173 --strictPort --host 127.0.0.1',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
})
