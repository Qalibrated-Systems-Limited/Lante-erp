/**
 * Helpers for the hermetic (Tier A) browser tests.
 *
 * Nothing here touches a real backend. Every request the app makes is intercepted, so the
 * tests are deterministic, need no credentials, and are safe to run on every push.
 */

/** Shape of the user object AuthContext persists to localStorage. */
export function fakeUser(overrides = {}) {
  return {
    id: 'u-1',
    email: 'tester@example.co.ke',
    fullName: 'Test User',
    permissions: ['operations.read.own'],
    ...overrides,
  }
}

/**
 * Puts the app in a signed-in state before it boots.
 *
 * AuthContext reads `lante_token` / `lante_user` from localStorage on first render, so
 * seeding them is equivalent to having logged in — without needing a real credential or a
 * live auth endpoint.
 */
export async function signIn(page, user = fakeUser(), token = 'test-token') {
  await page.addInitScript(([t, u]) => {
    localStorage.setItem('lante_token', t)
    localStorage.setItem('lante_user', JSON.stringify(u))
  }, [token, user])
}

/**
 * Intercepts every `/api/**` call.
 *
 * `routes` maps a substring of the URL to either a response body or a function returning
 * one. Anything unmatched resolves to an empty success envelope rather than failing, so a
 * test only has to describe the calls it actually cares about. Pass `status` on a route to
 * simulate an error.
 *
 * Returns a `calls` array so a test can assert on what the app requested, which is often
 * more meaningful than what it rendered.
 */
export async function mockApi(page, routes = {}) {
  const calls = []

  await page.route('**/api/**', async (route) => {
    const url = route.request().url()
    const method = route.request().method()
    calls.push({ url, method })

    const key = Object.keys(routes).find(k => url.includes(k))
    const entry = key ? routes[key] : undefined
    const resolved = typeof entry === 'function' ? entry(route.request()) : entry

    if (resolved?.status && resolved.status >= 400) {
      await route.fulfill({
        status: resolved.status,
        contentType: 'application/json',
        body: JSON.stringify(resolved.body ?? { message: 'error' }),
      })
      return
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(resolved ?? { data: [] }),
    })
  })

  return calls
}

/** The authenticated GET AuthContext makes on boot to validate the stored token. */
export const SYSTEM_MODULES = '/api/v1/system-modules'
