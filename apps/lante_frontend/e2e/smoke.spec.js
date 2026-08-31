import { test, expect } from '@playwright/test'
import { mockApi, signIn, fakeUser, SYSTEM_MODULES } from './helpers.js'

/**
 * Hermetic smoke tests — no backend, no credentials, safe in CI.
 *
 * These cover the things unit tests structurally cannot see: that the built bundle boots
 * at all, that route guards redirect, that permission gating works end to end through the
 * real router, and that an expired session does not leave the user staring at a dashboard.
 */

test.describe('the app boots', () => {
  test('serves the landing page without console errors', async ({ page }) => {
    const errors = []
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()) })
    page.on('pageerror', e => errors.push(e.message))

    await mockApi(page)
    await page.goto('/')

    await expect(page).toHaveTitle(/lante/i)
    // A blank page still "loads" — assert something rendered.
    await expect(page.locator('body')).not.toBeEmpty()
    expect(errors, `console errors on load:\n${errors.join('\n')}`).toEqual([])
  })

  test('renders the login form', async ({ page }) => {
    await mockApi(page)
    await page.goto('/login')

    await expect(page.locator('input[type="email"]')).toBeVisible()
    await expect(page.locator('input[type="password"]')).toBeVisible()
  })
})

test.describe('route guards', () => {
  test('sends an anonymous visitor from a protected route to /login', async ({ page }) => {
    await mockApi(page)
    await page.goto('/modules/operations')
    await expect(page).toHaveURL(/\/login/)
  })

  test('keeps a signed-in user out of the login page', async ({ page }) => {
    await signIn(page)
    await mockApi(page, { [SYSTEM_MODULES]: { data: [] } })
    await page.goto('/login')
    await expect(page).not.toHaveURL(/\/login/)
  })

  test('validates the stored token on boot rather than trusting it', async ({ page }) => {
    // AuthContext piggybacks on /system-modules to check the token is still good, so the
    // dashboard never flashes on a revoked session.
    await signIn(page)
    const calls = await mockApi(page, { [SYSTEM_MODULES]: { data: [] } })
    await page.goto('/dashboard')
    await expect.poll(() => calls.some(c => c.url.includes(SYSTEM_MODULES))).toBe(true)
  })

  test('signs out and redirects when the stored token is rejected', async ({ page }) => {
    // The regression this guards: a stale token rendering the dashboard until some later
    // call happens to 401.
    await signIn(page)
    await mockApi(page, { [SYSTEM_MODULES]: { status: 401, body: { message: 'expired' } } })

    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login/)
    expect(await page.evaluate(() => localStorage.getItem('lante_token'))).toBeNull()
  })
})

test.describe('permission gating', () => {
  test('redirects away from a route the user lacks permission for', async ({ page }) => {
    // The certificate route requires operations.read.own.
    await signIn(page, fakeUser({ permissions: ['fleet.read'] }))
    await mockApi(page, { [SYSTEM_MODULES]: { data: [] } })

    await page.goto('/modules/operations/assignments/123/certificate')
    await expect(page).toHaveURL(/\/dashboard/)
  })

  test('allows a route the user does have permission for', async ({ page }) => {
    await signIn(page, fakeUser({ permissions: ['operations.read.own'] }))
    await mockApi(page, { [SYSTEM_MODULES]: { data: [] } })

    await page.goto('/modules/operations/assignments/123/certificate')
    await expect(page).not.toHaveURL(/\/dashboard$/)
  })

  test('honours the hierarchy, not just exact matches', async ({ page }) => {
    // operations.write implies operations.read.own — the same rule the unit tests cover,
    // asserted here through the real router and AuthContext rather than in isolation.
    await signIn(page, fakeUser({ permissions: ['operations.write'] }))
    await mockApi(page, { [SYSTEM_MODULES]: { data: [] } })

    await page.goto('/modules/operations/assignments/123/certificate')
    await expect(page).not.toHaveURL(/\/dashboard$/)
  })
})

test.describe('login form behaviour', () => {
  test('surfaces a rejected credential instead of failing silently', async ({ page }) => {
    await mockApi(page, {
      '/api/v1/auth/login': { status: 401, body: { message: 'Invalid email or password' } },
    })

    await page.goto('/login')
    await page.locator('input[type="email"]').fill('nobody@example.co.ke')
    await page.locator('input[type="password"]').fill('wrong-password')
    await page.locator('form').first().locator('button[type="submit"]').click()

    await expect(page.getByText(/invalid|incorrect|failed/i).first()).toBeVisible()
    await expect(page).toHaveURL(/\/login/)
  })

  test('does not send a login request until both fields are filled', async ({ page }) => {
    // The inputs are `required`, so the browser blocks submission — worth pinning, since
    // removing the attribute would start sending empty credentials to the auth endpoint.
    const calls = await mockApi(page)

    await page.goto('/login')
    await page.locator('form').first().locator('button[type="submit"]').click()
    await page.waitForTimeout(300)

    expect(calls.filter(c => c.url.includes('/auth/login'))).toHaveLength(0)
  })
})
