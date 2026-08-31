import { describe, it, expect } from 'vitest';
import { hasPermission } from './permissions';

/**
 * Tests for the frontend permission hierarchy.
 *
 * `hasPermission` gates UI across 44 files and mirrors the backend's
 * PermissionAuthorizationHandler. This is a convenience layer — the backend is the real
 * authority — but a mistake here silently shows or hides functionality for whole roles.
 *
 * The most important block is "deliberate non-inheritance". Several permissions are
 * intentionally NOT implied by the broader permission that would otherwise cover them,
 * and today that intent is recorded only in source comments. A well-meaning edit that
 * "completes" one of those lists would quietly remove a control nobody notices is gone.
 * Those tests exist to make such an edit fail loudly.
 */

describe('hasPermission — hierarchy', () => {
  it('grants a permission the user holds directly', () => {
    expect(hasPermission(['fleet.read'], 'fleet.read')).toBe(true);
  });

  it('grants a lesser permission implied by a greater one', () => {
    expect(hasPermission(['fleet.write'], 'fleet.read')).toBe(true);
    expect(hasPermission(['fleet.delete'], 'fleet.read')).toBe(true);
    expect(hasPermission(['stores.approve'], 'stores.read')).toBe(true);
    expect(hasPermission(['finance.approve'], 'finance.read')).toBe(true);
    expect(hasPermission(['projects.approve'], 'projects.read.own')).toBe(true);
  });

  it('matches the owning service for permissions the UI used to hide', () => {
    // Each of these is accepted by the service that actually serves the request, and was
    // missing from the frontend copy — so the user was authorised by the API and shown
    // nothing. See #271. Verified against the owning handler, not against the other copies,
    // which are stale (#204).
    expect(hasPermission(['fleet.dispatch.request'], 'fleet.read')).toBe(true);      // fleet-service
    expect(hasPermission(['subcontracts.delete'], 'subcontracts.read')).toBe(true);  // subcontracts
    expect(hasPermission(['subcontracts.delete'], 'subcontracts.write')).toBe(true); // subcontracts
  });

  it('does not grant a greater permission from a lesser one', () => {
    expect(hasPermission(['fleet.read'], 'fleet.write')).toBe(false);
    expect(hasPermission(['finance.read'], 'finance.approve')).toBe(false);
    expect(hasPermission(['stores.read'], 'stores.delete')).toBe(false);
  });

  it('treats system.admin as satisfying everything except licensing.*', () => {
    for (const p of [
      'fleet.viewMileage',
      'compliance.whistleblower.write',
      'statutory.approve',
      'permissions.manage',
      'some.permission.that.does.not.exist',
    ]) {
      expect(hasPermission(['system.admin'], p)).toBe(true);
    }
    // The one exception. Covered properly in the non-inheritance block below.
    expect(hasPermission(['system.admin'], 'licensing.read')).toBe(false);
  });

  it('falls back to exact match for a permission not in the hierarchy', () => {
    expect(hasPermission(['custom.thing'], 'custom.thing')).toBe(true);
    expect(hasPermission(['custom.other'], 'custom.thing')).toBe(false);
  });

  it('lets operations.* imply project reads, matching the backend', () => {
    expect(hasPermission(['operations.write'], 'projects.read.own')).toBe(true);
    expect(hasPermission(['operations.approve'], 'projects.read.dept')).toBe(true);
  });
});

describe('hasPermission — deliberate non-inheritance (security invariants)', () => {
  /**
   * If one of these starts failing, do not "fix" the test. Each encodes a control that
   * exists on purpose, and the corresponding backend hierarchy must change first.
   */

  it('system.admin must NOT grant any licensing.* permission', () => {
    // licensing.* manages every CUSTOMER's software licence — a platform-wide resource, not
    // tenant data. Every tenant Admin role carries system.admin, so if the shortcut applied
    // here the UI would offer a platform-only capability to every tenant admin, which
    // LicenseService then refuses. Mirrors its PermissionAuthorizationHandler, which excludes
    // licensing.* from the same bypass.
    expect(hasPermission(['system.admin'], 'licensing.read')).toBe(false);
    expect(hasPermission(['system.admin'], 'licensing.write')).toBe(false);
    expect(hasPermission(['system.admin'], 'licensing.delete')).toBe(false);
    // Not defeatable by casing — the exemption is matched case-insensitively.
    expect(hasPermission(['system.admin'], 'LICENSING.read')).toBe(false);
    // A tenant admin's other permissions must not sneak it in either.
    expect(hasPermission(['system.admin', 'users.manage'], 'licensing.read')).toBe(false);
  });

  it('platform.licensing.manage grants the licensing permissions', () => {
    // The only permission that may. Held solely by role-platform-admin and never seeded
    // into a tenant role.
    expect(hasPermission(['platform.licensing.manage'], 'licensing.read')).toBe(true);
    expect(hasPermission(['platform.licensing.manage'], 'licensing.write')).toBe(true);
    expect(hasPermission(['platform.licensing.manage'], 'licensing.delete')).toBe(true);
  });

  it('keeps the licensing hierarchy working among its own permissions', () => {
    // Exempting the system.admin shortcut must not break normal inheritance within licensing.
    expect(hasPermission(['licensing.delete'], 'licensing.read')).toBe(true);
    expect(hasPermission(['licensing.write'], 'licensing.read')).toBe(true);
    expect(hasPermission(['licensing.read'], 'licensing.write')).toBe(false);
  });

  it('fleet.write must NOT grant fleet.viewMileage', () => {
    // Anti-fraud control: a driver holds fleet.write to create and complete their own
    // trips, but Start Mileage exists to catch a dishonest driver fudging the end
    // reading. Seeing it would defeat the control.
    expect(hasPermission(['fleet.write'], 'fleet.viewMileage')).toBe(false);
    expect(hasPermission(['fleet.expenses'], 'fleet.viewMileage')).toBe(false);
    expect(hasPermission(['fleet.read'], 'fleet.viewMileage')).toBe(false);

    // Only these do.
    expect(hasPermission(['fleet.viewMileage'], 'fleet.viewMileage')).toBe(true);
    expect(hasPermission(['fleet.delete'], 'fleet.viewMileage')).toBe(true);
  });

  it('compliance.* must NOT grant whistleblower access', () => {
    // COMP-003 restricted access: whistleblower reports are isolated from the general
    // compliance role precisely so compliance staff cannot read them by default.
    expect(hasPermission(['compliance.read'], 'compliance.whistleblower.read')).toBe(false);
    expect(hasPermission(['compliance.write'], 'compliance.whistleblower.read')).toBe(false);
    expect(hasPermission(['compliance.approve'], 'compliance.whistleblower.read')).toBe(false);
    expect(hasPermission(['compliance.delete'], 'compliance.whistleblower.write')).toBe(false);

    expect(hasPermission(['compliance.whistleblower.read'], 'compliance.whistleblower.read')).toBe(true);
    expect(hasPermission(['compliance.whistleblower.write'], 'compliance.whistleblower.read')).toBe(true);
  });

  it('whistleblower.read must NOT grant whistleblower.write', () => {
    expect(hasPermission(['compliance.whistleblower.read'], 'compliance.whistleblower.write')).toBe(false);
  });

  it('statutory.write must NOT grant statutory.approve', () => {
    // Annual return sign-off is an MD/Cosec tier, separate from preparing the filing.
    expect(hasPermission(['statutory.write'], 'statutory.approve')).toBe(false);
    expect(hasPermission(['statutory.read'], 'statutory.approve')).toBe(false);
    expect(hasPermission(['statutory.approve'], 'statutory.write')).toBe(true);
  });

  it('subcontracts.write must NOT grant subcontracts.approve', () => {
    expect(hasPermission(['subcontracts.write'], 'subcontracts.approve')).toBe(false);
  });

  it('tickets.read.own must NOT widen to dept or all', () => {
    expect(hasPermission(['tickets.read.own'], 'tickets.read.dept')).toBe(false);
    expect(hasPermission(['tickets.read.own'], 'tickets.read.all')).toBe(false);
    expect(hasPermission(['tickets.read.dept'], 'tickets.read.all')).toBe(false);
  });
});

describe('hasPermission — calibration certificate register', () => {
  // Owned by Technical, read by CRM too: Technical tracks what it issued, CRM chases
  // client recalls. Either department's read permission satisfies it.
  it('is satisfied by either an operations or a crm read permission', () => {
    for (const p of [
      'operations.read.own', 'operations.read.dept', 'operations.read.all',
      'operations.write', 'operations.approve',
      'crm.read.own', 'crm.read.dept', 'crm.read.all', 'crm.write',
    ]) {
      expect(hasPermission([p], 'calibration.certificates.read')).toBe(true);
    }
  });

  it('is not satisfied by an unrelated department', () => {
    expect(hasPermission(['fleet.write'], 'calibration.certificates.read')).toBe(false);
    expect(hasPermission(['stores.approve'], 'calibration.certificates.read')).toBe(false);
  });

  it('does not imply the right to withdraw a certificate', () => {
    // Withdrawal is gated separately on calibration.sign, which is not in the hierarchy
    // and therefore requires an exact match.
    expect(hasPermission(['calibration.certificates.read'], 'calibration.sign')).toBe(false);
    expect(hasPermission(['operations.approve'], 'calibration.sign')).toBe(false);
  });
});

describe('hasPermission — edge cases', () => {
  it('denies when the user has no permissions', () => {
    expect(hasPermission([], 'fleet.read')).toBe(false);
    expect(hasPermission(null, 'fleet.read')).toBe(false);
    expect(hasPermission(undefined, 'fleet.read')).toBe(false);
  });

  // Fail-open by design: components pass `undefined` for "no permission needed" so an
  // ungated element renders. Worth knowing, because a typo'd or undefined permission
  // constant silently grants access rather than denying it.
  it('grants access when no permission is required (fail-open)', () => {
    expect(hasPermission([], undefined)).toBe(true);
    expect(hasPermission([], '')).toBe(true);
    expect(hasPermission(null, null)).toBe(true);
  });

  it('is unaffected by unrelated permissions the user also holds', () => {
    expect(hasPermission(['hse.read', 'stores.write', 'fleet.read'], 'fleet.write')).toBe(false);
    expect(hasPermission(['hse.read', 'fleet.delete'], 'fleet.read')).toBe(true);
  });
});
