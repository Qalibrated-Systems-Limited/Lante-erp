import { useState } from 'react'
import { Modal, Input, Btn, Alert } from '../../ui.jsx'

// O11.1 — client sign-off on a milestone (completes it; billable milestones trigger the Finance
// invoice seam on the backend).
export default function MilestoneSignOffModal({ milestone, onClose, onSave }) {
  const [signOffBy, setSignOffBy] = useState('')
  const [notes, setNotes]   = useState('')
  const [busy, setBusy]     = useState(false)
  const [error, setError]   = useState('')

  const submit = async () => {
    if (!signOffBy.trim()) { setError('The accepting party’s name is required.'); return }
    setBusy(true); setError('')
    try {
      await onSave(milestone.id, { signOffBy: signOffBy.trim(), notes: notes.trim() || null })
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not sign off the milestone.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={`Sign off — ${milestone.title}`} onClose={onClose} width={460}>
      {error && <Alert type="error">{error}</Alert>}
      {milestone.isBillable && <Alert type="info">This milestone is billable — sign-off raises a Finance invoice.</Alert>}
      <Input label="Accepted by (client representative)" value={signOffBy} onChange={setSignOffBy} required />
      <Input label="Notes" value={notes} onChange={setNotes} />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="green" onClick={submit} disabled={busy}>{busy ? 'Signing…' : 'Sign off'}</Btn>
      </div>
    </Modal>
  )
}
