/**
 * When may a finance document be cancelled?
 *
 * Extracted from the JSX deliberately. There is no React Testing Library in this app — vitest
 * covers src/utils and src/theme, and the Playwright suite is route-level — so a rule left inline
 * in a row renderer is a rule with no tests. The button cannot really be wrong in an interesting
 * way; this decision can.
 *
 * It mirrors the guards in FinanceService's InvoiceService.CancelAsync and
 * SupplierInvoiceService.CancelAsync (#332). Mirrored, not authoritative: the frontend can only
 * hide a button, and the API enforces regardless. Getting this wrong shows someone a control that
 * 502s, or hides one they were entitled to — a UX defect, never a security one. Said plainly here
 * so nobody later "simplifies" it on the assumption it is load-bearing for safety.
 */

/** Statuses that mean the document is finished with, on either side of the ledger. */
const SETTLED = ['Paid', 'PartPaid']

/**
 * @param {object} doc      an InvoiceReadDto or SupplierInvoiceReadDto
 * @param {object} opts
 * @param {boolean} opts.canApprove  holder of finance.approve, which both endpoints require
 * @returns {{allowed: boolean, reason: string|null}} reason is null when allowed, otherwise a
 *          short phrase suitable for a tooltip on the disabled state.
 */
export function cancellability(doc, { canApprove } = {}) {
  if (!doc) return { allowed: false, reason: 'No document.' }

  if (!canApprove) {
    return { allowed: false, reason: 'Cancelling needs finance.approve.' }
  }

  if (doc.status === 'Cancelled') {
    return { allowed: false, reason: 'Already cancelled.' }
  }

  // Checked BEFORE status, and on the money rather than the status, because that is the order the
  // API uses and for the same reason. ReceiptService and PaymentVoucherService both set
  // Paid/PartPaid as a side effect of moving money, so the status is a derived summary of two
  // separate code paths while paidAmount is the fact itself. A document carrying money with a
  // status that never caught up must still show as uncancellable, or the user gets a button that
  // fails every time they press it.
  if (Number(doc.paidAmount) > 0) {
    return { allowed: false, reason: 'Money has been received or paid against this document.' }
  }

  if (SETTLED.includes(doc.status)) {
    return { allowed: false, reason: `Cannot cancel a ${doc.status} document.` }
  }

  return { allowed: true, reason: null }
}

/** Convenience for a row renderer that only needs the boolean. */
export function canCancel(doc, opts) {
  return cancellability(doc, opts).allowed
}
