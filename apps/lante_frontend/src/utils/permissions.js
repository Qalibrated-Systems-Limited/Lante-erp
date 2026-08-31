// Permission hierarchy — mirrors PermissionAuthorizationHandler on the backend.
// Key = required permission; value = set of user permissions that satisfy it.
const HIERARCHY = {
  // Tickets
  'tickets.read.own':   ['tickets.read.own', 'tickets.read.dept', 'tickets.read.all', 'tickets.write', 'tickets.assign', 'tickets.resolve', 'tickets.delete', 'system.admin'],
  'tickets.read.dept':  ['tickets.read.dept', 'tickets.read.all', 'system.admin'],
  'tickets.read.all':   ['tickets.read.all', 'system.admin'],
  'tickets.write':      ['tickets.write', 'tickets.delete', 'system.admin'],
  'tickets.assign':     ['tickets.assign', 'system.admin'],
  'tickets.resolve':    ['tickets.resolve', 'system.admin'],
  'tickets.delete':     ['tickets.delete', 'system.admin'],

  // Projects
  'projects.read.own':  ['projects.read.own', 'projects.read.dept', 'projects.read.all', 'projects.write', 'projects.delete', 'projects.approve', 'operations.write', 'operations.delete', 'operations.approve', 'system.admin'],
  'projects.read.dept': ['projects.read.dept', 'projects.read.all', 'projects.write', 'projects.delete', 'projects.approve', 'operations.write', 'operations.delete', 'operations.approve', 'system.admin'],
  'projects.read.all':  ['projects.read.all', 'projects.approve', 'system.admin'],
  'projects.write':     ['projects.write', 'projects.delete', 'projects.approve', 'system.admin'],
  'projects.delete':    ['projects.delete', 'projects.approve', 'system.admin'],
  'projects.approve':   ['projects.approve', 'system.admin'],

  // Operations
  // read.dept/read.all were enforced in operations-service since it existed (AssignmentsController,
  // TimesheetsController, FieldVehiclesController + its own PermissionAuthorizationHandler) but never
  // seeded on the backend, so no role could ever hold them — see #282. Added here to match the
  // backend hierarchy now that they're seeded; this alone doesn't grant them to anyone.
  'operations.read.own':  ['operations.read.own', 'operations.read.dept', 'operations.read.all', 'operations.write', 'operations.delete', 'operations.approve', 'system.admin'],
  'operations.read.dept': ['operations.read.dept', 'operations.read.all', 'system.admin'],
  'operations.read.all':  ['operations.read.all', 'system.admin'],
  'operations.write':    ['operations.write', 'operations.delete', 'operations.approve', 'system.admin'],
  'operations.delete':   ['operations.delete', 'system.admin'],
  'operations.approve':  ['operations.approve', 'system.admin'],

  // Finance
  'finance.read':    ['finance.read', 'finance.write', 'finance.approve', 'finance.reports', 'system.admin'],
  'finance.write':   ['finance.write', 'finance.approve', 'system.admin'],
  'finance.approve': ['finance.approve', 'system.admin'],
  'finance.reports': ['finance.reports', 'finance.approve', 'system.admin'],

  // Reports
  'reports.view':     ['reports.view', 'reports.export', 'reports.schedule', 'system.admin'],
  'reports.export':   ['reports.export', 'system.admin'],
  'reports.schedule': ['reports.schedule', 'system.admin'],

  // Fleet
  'fleet.read':     ['fleet.read', 'fleet.write', 'fleet.delete', 'fleet.expenses', 'fleet.dispatch.request', 'system.admin'],
  'fleet.write':    ['fleet.write', 'fleet.delete', 'system.admin'],
  'fleet.delete':   ['fleet.delete', 'system.admin'],
  'fleet.expenses': ['fleet.expenses', 'fleet.write', 'fleet.delete', 'system.admin'],
  // Restricted to Admins/Supervisors, deliberately NOT implied by fleet.write — a driver can
  // have fleet.write (to create/complete their own trips) without being able to see Start
  // Mileage, which exists to catch a dishonest driver fudging the end reading.
  'fleet.viewMileage': ['fleet.viewMileage', 'fleet.delete', 'system.admin'],
  'fleet.approve': ['fleet.approve', 'system.admin'],

  // Stores
  'stores.read':    ['stores.read', 'stores.write', 'stores.delete', 'stores.approve', 'system.admin'],
  'stores.write':   ['stores.write', 'stores.delete', 'stores.approve', 'system.admin'],
  'stores.delete':  ['stores.delete', 'system.admin'],
  'stores.approve': ['stores.approve', 'system.admin'],

  // Technician (legacy)
  'technician.read':    ['technician.read', 'technician.write', 'technician.delete', 'technician.approve', 'system.admin'],
  'technician.write':   ['technician.write', 'technician.delete', 'technician.approve', 'system.admin'],
  'technician.delete':  ['technician.delete', 'system.admin'],
  'technician.approve': ['technician.approve', 'system.admin'],

  // Licensing
  // Deliberately NOT chained to system.admin, and exempt from the system.admin shortcut in
  // hasPermission below. Mirrors LicenseService's PermissionAuthorizationHandler: system.admin
  // means "full admin within your OWN tenant", but this service manages every CUSTOMER's software
  // licence — a platform-wide resource, not tenant data. Every tenant Admin role carries
  // system.admin, so granting it here would show tenant admins a platform-only capability the
  // backend then refuses. Only platform.licensing.manage, held solely by role-platform-admin and
  // never seeded into a tenant role, may.
  'licensing.read':   ['licensing.read', 'licensing.write', 'licensing.delete', 'platform.licensing.manage'],
  'licensing.write':  ['licensing.write', 'licensing.delete', 'platform.licensing.manage'],
  'licensing.delete': ['licensing.delete', 'platform.licensing.manage'],

  // HSE
  'hse.read':    ['hse.read', 'hse.write', 'hse.delete', 'hse.approve', 'system.admin'],
  'hse.write':   ['hse.write', 'hse.delete', 'hse.approve', 'system.admin'],
  'hse.delete':  ['hse.delete', 'system.admin'],
  'hse.approve': ['hse.approve', 'system.admin'],

  // Compliance — whistleblower.* is deliberately its own isolated pair, not
  // implied by compliance.read/write, matching COMP-003's restricted-access
  // requirement and the backend's PermissionAuthorizationHandler.
  'compliance.read':    ['compliance.read', 'compliance.write', 'compliance.delete', 'compliance.approve', 'system.admin'],
  'compliance.write':   ['compliance.write', 'compliance.delete', 'compliance.approve', 'system.admin'],
  'compliance.delete':  ['compliance.delete', 'system.admin'],
  'compliance.approve': ['compliance.approve', 'system.admin'],
  'compliance.whistleblower.read':  ['compliance.whistleblower.read', 'compliance.whistleblower.write', 'system.admin'],
  'compliance.whistleblower.write': ['compliance.whistleblower.write', 'system.admin'],

  // Statutory Compliance Calendar — statutory.approve is its own MD/Cosec tier,
  // mirroring the backend's isolated hierarchy for annual return sign-off.
  'statutory.read':    ['statutory.read', 'statutory.write', 'statutory.approve', 'system.admin'],
  'statutory.write':   ['statutory.write', 'statutory.approve', 'system.admin'],
  'statutory.approve': ['statutory.approve', 'system.admin'],

  // Subcontractor Engagement & Prequalification
  'subcontracts.read':    ['subcontracts.read', 'subcontracts.write', 'subcontracts.delete', 'subcontracts.approve', 'system.admin'],
  'subcontracts.write':   ['subcontracts.write', 'subcontracts.delete', 'subcontracts.approve', 'system.admin'],
  'subcontracts.approve': ['subcontracts.approve', 'system.admin'],

  // Issued-certificate register — owned by Technical, read by CRM too: Technical tracks what it
  // issued, CRM chases the client recalls. Either department's read permission satisfies it.
  // Mirrors PermissionAuthorizationHandler on the backend; withdrawal is gated separately on
  // calibration.sign.
  'calibration.certificates.read': ['calibration.certificates.read',
                                    'operations.read.own', 'operations.read.dept', 'operations.read.all',
                                    'operations.write', 'operations.delete', 'operations.approve',
                                    'crm.read.own', 'crm.read.dept', 'crm.read.all', 'crm.write',
                                    'system.admin'],

  // Admin
  'users.read':        ['users.read', 'users.write', 'users.delete', 'operations.write', 'operations.approve', 'projects.write', 'projects.approve', 'system.admin'],
  'users.write':       ['users.write', 'users.delete', 'system.admin'],
  'departments.manage':['departments.manage', 'system.admin'],
  'roles.manage':      ['roles.manage', 'system.admin'],
  'permissions.manage':['permissions.manage', 'system.admin'],
  'settings.manage':   ['settings.manage', 'system.admin'],
}

/**
 * Returns true if the user's permission list satisfies the required permission,
 * respecting the hierarchy (e.g. fleet.write satisfies fleet.read).
 *
 * system.admin satisfies everything EXCEPT licensing.*, which is platform-scoped — see the
 * licensing block in HIERARCHY.
 *
 * @param {string[]} userPermissions - permissions from the stored user object
 * @param {string} required - permission string to check
 */
export function hasPermission(userPermissions, required) {
  if (!required) return true
  if (!userPermissions?.length) return false
  // licensing.* is exempt from the system.admin shortcut — see the HIERARCHY comment above.
  // Every other resource keeps the usual bypass.
  const isLicensingResource = String(required).toLowerCase().startsWith('licensing.')
  if (!isLicensingResource && userPermissions.includes('system.admin')) return true
  const accepted = HIERARCHY[required] ?? [required]
  return accepted.some(p => userPermissions.includes(p))
}
