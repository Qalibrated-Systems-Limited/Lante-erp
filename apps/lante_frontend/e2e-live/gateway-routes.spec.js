import { test, expect, request } from '@playwright/test'

/**
 * Tier B — runs against a REAL deployment. Not part of CI. Run with:
 *
 *   E2E_LIVE=1 npm run test:e2e:live
 *   E2E_LIVE=1 E2E_API_URL=https://<gateway-host> npm run test:e2e:live
 *
 * These need no credentials and write nothing, so they are safe against production.
 *
 * They exist because of #195: two calibration-certificate routes lived in the copy of
 * yarp.json under packages/ but not in the copy the gateway actually serves, so a shipped
 * feature returned 404 in production and nothing caught it. The frontend called those
 * endpoints in four places.
 *
 * The trick that found it is the assertion here: for a route that requires auth,
 *
 *   401 = the route is deployed and rejected us for credentials  -> healthy
 *   404 = there is no such route in the deployed gateway config  -> broken
 *
 * So an unauthenticated request distinguishes "missing route" from "bad token" without
 * ever holding a token. Add an entry below whenever the frontend starts calling a new
 * endpoint, and this will catch a gateway/route mismatch before a user does.
 */

const API = process.env.E2E_API_URL ?? 'https://kmk.support.qalibrated.co.ke'

/** Endpoints the frontend calls that must exist in the deployed gateway config. */
const REQUIRED_ROUTES = [
  // services/operations.js — these are the ones #195 was about
  '/api/v1/calibration-certificates',
  '/api/v1/service-requests',
  '/api/v1/projects',
  '/api/v1/assignments',
  // core platform
  '/api/v1/system-modules',
  '/api/v1/users',
]

test.describe('deployed gateway route table', () => {
  test('the gateway is up', async () => {
    const ctx = await request.newContext()
    const res = await ctx.get(`${API}/health`)
    expect(res.status()).toBe(200)
    await ctx.dispose()
  })

  for (const path of REQUIRED_ROUTES) {
    test(`${path} is routed (401, not 404)`, async () => {
      const ctx = await request.newContext()
      const res = await ctx.get(`${API}${path}`)
      await ctx.dispose()

      expect(
        res.status(),
        `${path} returned ${res.status()}. A 404 means the deployed gateway has no route ` +
        `for this path — check kubernetes/helm-charts/gateway-service/yarp.json, which is ` +
        `the copy that actually ships, not the one under packages/. See #195.`,
      ).not.toBe(404)

      // 401/403 both mean "routed, but you are not authenticated" — either is healthy.
      expect([401, 403]).toContain(res.status())
    })
  }
})

test.describe('deployed frontend', () => {
  test('serves the SPA', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveTitle(/lante/i)
    await expect(page.locator('body')).not.toBeEmpty()
  })

  test('serves the login page', async ({ page }) => {
    await page.goto('/login')
    await expect(page.locator('input[type="email"]')).toBeVisible()
  })
})
