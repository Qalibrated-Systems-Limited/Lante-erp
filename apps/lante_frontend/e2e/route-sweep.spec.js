import { test, expect } from '@playwright/test'
import { mockApi, signIn, fakeUser, SYSTEM_MODULES } from './helpers.js'

/**
 * Route sweep — visits every parameterless route as a fully-permissioned user with every
 * API call mocked, and fails on an uncaught exception or a blank render.
 *
 * Why this exists: the app has 273 components and the smoke suite covers login and route
 * guarding only. A component that throws on mount — a null dereference on an empty API
 * response, a bad import, a hook called conditionally — is invisible to unit tests that
 * never mount it and to a code read that never runs it. This mounts all of them.
 *
 * Every unmatched /api/** call resolves to `{ data: [] }` (see helpers.mockApi), so this
 * also exercises the empty-state path of every page, which is where null-guard bugs live.
 */

const PERMISSIONS = ['calibration.certificates.read', 'compliance.read', 'crm.read.own', 'crm.write', 'departments.manage', 'finance.read', 'fleet.read', 'fleet.write', 'hse.read', 'licensing.read', 'operations.read.dept', 'operations.read.own', 'operations.write', 'permissions.manage', 'projects.read.all', 'projects.read.own', 'projects.write', 'reports.view', 'roles.manage', 'settings.manage', 'stores.read', 'subcontracts.read', 'tickets.read.own', 'tickets.write', 'users.read', 'platform.licensing.manage', 'system.admin']

const ROUTES = ['/', '/about', '/accept-invite', '/alerts', '/cart', '/checkout', '/contact', '/dashboard', '/login', '/modules/admin', '/modules/assets', '/modules/bids', '/modules/compliance', '/modules/crm', '/modules/crm/after-sales', '/modules/crm/customers/new', '/modules/crm/dashboard', '/modules/crm/legal', '/modules/crm/marketing', '/modules/crm/payment-alerts', '/modules/debtors', '/modules/departments', '/modules/finance', '/modules/fleet', '/modules/fleet/drivers', '/modules/fleet/field-vehicles', '/modules/fleet/materials', '/modules/fleet/my-profile', '/modules/fleet/requests', '/modules/fleet/trips', '/modules/fleet/trips/new', '/modules/fleet/trip-types', '/modules/fleet/trucks', '/modules/fleet/vehicle-classes', '/modules/hr', '/modules/hse', '/modules/ic', '/modules/inspection', '/modules/integrations', '/modules/licensing', '/modules/operations', '/modules/operations/assignments', '/modules/operations/certificates', '/modules/operations/equipment-history', '/modules/operations/field-vehicles', '/modules/operations/negligence', '/modules/operations/projects', '/modules/operations/projects-dashboard', '/modules/operations/project-templates', '/modules/operations/reference-standards', '/modules/operations/service-requests', '/modules/operations/service-requests/new', '/modules/operations/technical-dashboard', '/modules/operations/timesheets', '/modules/operations/workload', '/modules/permissions', '/modules/procurement', '/modules/projects', '/modules/projects/new', '/modules/quality', '/modules/reports', '/modules/requisitions', '/modules/roles', '/modules/settings', '/modules/shop', '/modules/sops', '/modules/stores', '/modules/stores/categories', '/modules/stores/grn', '/modules/stores/issues', '/modules/stores/items', '/modules/stores/locations', '/modules/stores/sold-items', '/modules/stores/stock-take', '/modules/stores/suppliers', '/modules/stores/transfers', '/modules/stores/units-of-measure', '/modules/subcontracts', '/modules/tasks', '/modules/tax', '/modules/ticketing', '/modules/ticketing/dashboard', '/modules/ticketing/kb', '/modules/ticketing/macros', '/modules/ticketing/new', '/modules/ticketing/settings', '/modules/ticketing/tags', '/modules/ticketing/workflows', '/modules/users', '/notifications', '/platform', '/platform/broadcast', '/platform/companies', '/platform/companies/new', '/platform/dashboard', '/platform/login', '/platform/plans', '/portal', '/portal/service-request', '/portal/submit', '/portal/survey', '/portal/track', '/services', '/shop', '/staff', '/verify', '/workspace']

for (const route of ROUTES) {
  test(`renders ${route}`, async ({ page }) => {
    const errors = []
    page.on('pageerror', e => errors.push(`pageerror: ${e.message}`))
    page.on('console', m => { if (m.type() === 'error') errors.push(`console: ${m.text()}`) })

    await signIn(page, fakeUser({ permissions: PERMISSIONS, isCompanyAdmin: true }))
    // The default `{ data: [] }` stands in for "endpoint returned nothing", which is the
    // empty-state path worth exercising. `/projects/workload` is overridden because its DTO
    // always returns an object with an initialised Rows list (WorkloadDto.Rows = new()), so
    // a bare [] is a shape the real API cannot produce — asserting on it tests the mock.
    await mockApi(page, {
      [SYSTEM_MODULES]: { data: [] },
      '/projects/workload': { data: { from: '2026-01-01', to: '2026-01-28', workingDays: 20, rows: [] } },
    })
    await page.goto(route)
    await page.waitForLoadState('networkidle')

    // Errors first: a component that throws on mount also renders an empty body, and
    // "body is empty" hides the exception that caused it.
    expect(errors, `${route} produced:\n${errors.join('\n')}`).toEqual([])
    await expect(page.locator('body')).not.toBeEmpty()
  })
}
