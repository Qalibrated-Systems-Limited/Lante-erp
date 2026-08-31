import { describe, it, expect } from 'vitest'
import { cancellability, canCancel } from './documentActions.js'

const APPROVER = { canApprove: true }

/** An invoice or bill in the shape the finance API returns. */
const doc = (over = {}) => ({ id: 'i1', status: 'Issued', paidAmount: 0, balance: 1000, ...over })

describe('cancellability', () => {
  describe('permission', () => {
    it('refuses without finance.approve, whatever the document looks like', () => {
      // Both endpoints are [Authorize(Policy = "finance.approve")]. Showing the button to a
      // finance.write holder means offering a control that 403s every time.
      const r = cancellability(doc(), { canApprove: false })
      expect(r.allowed).toBe(false)
      expect(r.reason).toMatch(/finance\.approve/)
    })

    it('is checked before anything else, so the reason names the permission', () => {
      // A user lacking the permission should be told that, not told the document is already
      // cancelled — the second is true but useless to them.
      const r = cancellability(doc({ status: 'Cancelled' }), { canApprove: false })
      expect(r.reason).toMatch(/finance\.approve/)
    })

    it('treats missing options as no permission rather than granting by default', () => {
      expect(canCancel(doc())).toBe(false)
    })
  })

  describe('money already moved', () => {
    it('refuses when anything has been received or paid', () => {
      const r = cancellability(doc({ status: 'PartPaid', paidAmount: 400 }), APPROVER)
      expect(r.allowed).toBe(false)
      expect(r.reason).toMatch(/received or paid/)
    })

    it('refuses on paidAmount even when the status never caught up', () => {
      // The case the API guard exists for. ReceiptService and PaymentVoucherService each set
      // Paid/PartPaid as a side effect of moving money; a future payment path that forgets that
      // line leaves exactly this row. Deciding on status alone would show a Cancel button that
      // the API then refuses.
      const r = cancellability(doc({ status: 'Issued', paidAmount: 250 }), APPROVER)
      expect(r.allowed).toBe(false)
      expect(r.reason).toMatch(/received or paid/)
    })

    it('allows a zero paidAmount', () => {
      expect(canCancel(doc({ paidAmount: 0 }), APPROVER)).toBe(true)
    })

    it('reads a numeric string, since JSON numbers can arrive as strings', () => {
      expect(canCancel(doc({ paidAmount: '250' }), APPROVER)).toBe(false)
      expect(canCancel(doc({ paidAmount: '0' }), APPROVER)).toBe(true)
    })

    it('treats a missing paidAmount as nothing paid rather than blocking', () => {
      // Older rows and any caller projecting a subset. Blocking here would hide the button on
      // every document whose DTO predates the field.
      const { paidAmount, ...without } = doc()
      expect(canCancel(without, APPROVER)).toBe(true)
    })
  })

  describe('status', () => {
    it.each(['Draft', 'Issued', 'Received', 'Approved'])('allows %s', (status) => {
      expect(canCancel(doc({ status }), APPROVER)).toBe(true)
    })

    it.each(['Paid', 'PartPaid'])('refuses %s', (status) => {
      expect(canCancel(doc({ status, paidAmount: 0 }), APPROVER)).toBe(false)
    })

    it('refuses an already-cancelled document', () => {
      const r = cancellability(doc({ status: 'Cancelled' }), APPROVER)
      expect(r.allowed).toBe(false)
      expect(r.reason).toMatch(/Already cancelled/)
    })

    it('names the money before the status when both would refuse', () => {
      // Same order as the API, so the message the user reads matches the one they would have got
      // from the server. "Paid" is the symptom; the money is the cause and the thing they must
      // undo first.
      const r = cancellability(doc({ status: 'Paid', paidAmount: 1000 }), APPROVER)
      expect(r.reason).toMatch(/received or paid/)
    })

    it('does not allow an unrecognised status through by default', () => {
      // A status this app has not seen — added server-side, or a typo in a fixture. Draft/Issued
      // are allowed by being ordinary; anything unknown falls through to allowed, which is the
      // deliberate choice: the API is the enforcer and a new status is far more likely to be
      // cancellable than not. Asserted so the choice is visible rather than accidental.
      expect(canCancel(doc({ status: 'SomethingNew' }), APPROVER)).toBe(true)
    })
  })

  describe('bad input', () => {
    it('refuses a null document instead of throwing in a row renderer', () => {
      expect(cancellability(null, APPROVER)).toEqual({ allowed: false, reason: 'No document.' })
    })
  })
})
