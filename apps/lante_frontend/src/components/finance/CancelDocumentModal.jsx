import { useState } from 'react'
import { T } from '../../theme/tokens.js'
import { Modal, Btn, Input, Alert } from '../ui.jsx'

/**
 * Confirms cancelling an invoice or supplier invoice, and collects the required reason.
 *
 * Shared by InvoicesTab and PaymentsTab because the two flows differ only in wording — both post a
 * reason, both reverse a GL posting when the document had one, and both are irreversible from the
 * UI's point of view. The API decides; this only asks.
 */
export default function CancelDocumentModal({ doc, label, posted, onClose, onConfirm }) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const trimmed = reason.trim()

  async function submit() {
    if (!trimmed) return
    setBusy(true)
    try { await onConfirm(trimmed) } finally { setBusy(false) }
  }

  return (
    <Modal title={`Cancel ${label} ${doc.number}`} onClose={busy ? () => {} : onClose} width={480}>
      <Alert type="warn">
        {posted
          // Named explicitly rather than left as "this will be cancelled". Someone approving this
          // is reversing a posting in the general ledger, and the audit trail keeps both entries.
          ? `This reverses the journal ${label === 'invoice' ? 'raised when the invoice was issued' : 'raised when the bill was approved'}. The original posting and its reversal both stay on the ledger.`
          : `This ${label} was never posted to the ledger, so nothing is reversed.`}
      </Alert>

      <Input
        label="Reason"
        value={reason}
        onChange={setReason}
        required
        placeholder="e.g. duplicate, raised against the wrong account"
        note="Stored on the document. This is the first thing anyone asks months later."
      />

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
        <Btn variant="ghost" onClick={onClose} disabled={busy}>Keep it</Btn>
        {/* Disabled until a reason is typed: the API rejects a blank one, so an enabled button here
            would just be a round trip to an error. */}
        <Btn variant="danger" onClick={submit} disabled={busy || !trimmed}>
          {busy ? 'Cancelling…' : `Cancel ${label}`}
        </Btn>
      </div>
      {!trimmed && (
        <div style={{ color: T.mgrey, fontSize: 12, marginTop: 8, textAlign: 'right' }}>
          A reason is required.
        </div>
      )}
    </Modal>
  )
}
