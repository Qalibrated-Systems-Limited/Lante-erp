import { describe, it, expect } from 'vitest'
import { expiryState, isExpired, needsAttention, EXPIRY_WARNING_DAYS } from './expiry.js'

const NOW = new Date('2026-08-20T09:00:00Z')

describe('expiryState', () => {
  describe('the failure this exists to stop', () => {
    // Every one of these is an insurance expiry six years in the past. The previous check —
    // `new Date(v) < new Date()` — reported NOT expired for all but the first, because an Invalid
    // Date compares false against everything. Verified in node before writing this.
    it.each([
      ['31/01/2020', 'dd/mm/yyyy — what a Kenyan user types'],
      ['31-01-2020', 'dd-mm-yyyy'],
      ['31.01.2020', 'dd.mm.yyyy'],
      ['2020-13-01', 'a real-looking date with an impossible month'],
      ['expired', 'someone typed a word into a free-text field'],
      ['n/a', 'the other thing people type'],
    ])('%s is unknown, not valid (%s)', (value) => {
      const r = expiryState(value, NOW)
      expect(r.state).toBe('unknown')
      // The point: it must NOT come back as valid. Reporting "we cannot read this" is actionable;
      // reporting nothing is how a vehicle stays on the road uninsured.
      expect(r.state).not.toBe('valid')
      expect(needsAttention(value, NOW)).toBe(true)
    })

    it('does not claim an unreadable value has expired either', () => {
      // Overstating is its own bug — it would ground a vehicle whose paperwork is fine and whose
      // date was merely typed oddly. `unknown` is the honest answer both ways.
      expect(isExpired('31/01/2020', NOW)).toBe(false)
      expect(expiryState('31/01/2020', NOW).state).toBe('unknown')
    })
  })

  describe('unset versus unreadable', () => {
    it.each([null, undefined, '', '   '])('%s is unset', (value) => {
      expect(expiryState(value, NOW).state).toBe('unset')
    })

    it('unset does not need attention but unreadable does', () => {
      // Nothing was claimed, so nothing is inconsistent — a vehicle with no insurance date recorded
      // is a data-entry gap, not a contradiction. An unreadable one is a claim nobody can check.
      expect(needsAttention(null, NOW)).toBe(false)
      expect(needsAttention('garbage', NOW)).toBe(true)
    })
  })

  describe('states', () => {
    it('a past date is expired', () => {
      const r = expiryState('2026-08-19', NOW)
      expect(r.state).toBe('expired')
      expect(r.daysLeft).toBe(-1)
    })

    it('today is not yet expired', () => {
      // The boundary. A document expiring today is valid today — a `<=` here would ground a vehicle
      // on the last day of cover it actually has.
      const r = expiryState('2026-08-20', NOW)
      expect(r.state).toBe('expiring')
      expect(r.daysLeft).toBe(0)
    })

    it('inside the warning window is expiring, not valid', () => {
      expect(expiryState('2026-09-10', NOW).state).toBe('expiring')
      expect(expiryState('2026-09-10', NOW).daysLeft).toBe(21)
    })

    it('exactly on the warning boundary is expiring', () => {
      const boundary = new Date(Date.UTC(2026, 7, 20 + EXPIRY_WARNING_DAYS))
      expect(expiryState(boundary, NOW).state).toBe('expiring')
    })

    it('one day past the boundary is valid', () => {
      const beyond = new Date(Date.UTC(2026, 7, 21 + EXPIRY_WARNING_DAYS))
      expect(expiryState(beyond, NOW).state).toBe('valid')
      expect(needsAttention(beyond, NOW)).toBe(false)
    })

    it('a far future date is valid', () => {
      expect(expiryState('2030-01-01', NOW).state).toBe('valid')
    })
  })

  describe('input shapes', () => {
    it('accepts a Date as well as a string', () => {
      // InspectionExpiryDate arrives as a real date; InsuranceExpiry as a string. One helper has to
      // read both or the caller ends up branching and the bug comes back on one branch.
      expect(expiryState(new Date('2026-08-19'), NOW).state).toBe('expired')
      expect(expiryState(new Date('2030-01-01'), NOW).state).toBe('valid')
    })

    it('accepts a full ISO timestamp, read in UTC', () => {
      // UTC deliberately, so every viewer sees the same compliance flag regardless of their clock.
      // The first version of the util mixed local getters with UTC construction and this case
      // answered differently at +03:00 than at UTC.
      expect(expiryState('2026-08-19T23:59:59Z', NOW).state).toBe('expired')
      expect(expiryState('2026-08-20T00:00:01Z', NOW).state).toBe('expiring')
    })

    it('ignores the time of day when counting days', () => {
      // Comparing instants rather than dates would make a document expire at 09:00 rather than at
      // end of day, so an afternoon check on the expiry date would wrongly read as expired.
      const lateNow = new Date('2026-08-20T23:30:00Z')
      expect(expiryState('2026-08-20', lateNow).state).toBe('expiring')
      expect(expiryState('2026-08-20', lateNow).daysLeft).toBe(0)
    })
  })
})
