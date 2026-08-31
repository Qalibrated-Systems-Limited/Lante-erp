import { useState } from 'react'
import { Modal, Input, Select, Btn, Alert } from '../../ui.jsx'

const STATUSES = ['NotStarted', 'InProgress', 'Completed', 'Delayed']

// O11.1/O3 — log a daily milestone progress update (satisfies the 5PM update requirement).
export default function MilestoneUpdateModal({ milestone, onClose, onSave }) {
  const [note, setNote]     = useState('')
  const [progress, setProg] = useState(milestone.progressPct ?? 0)
  const [status, setStatus] = useState(milestone.status ?? 'InProgress')
  const [busy, setBusy]     = useState(false)
  const [error, setError]   = useState('')

  const submit = async () => {
    setBusy(true); setError('')
    try {
      await onSave(milestone.id, { note: note.trim() || null, progressPct: Number(progress) || 0, status })
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not log the update.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={`Update — ${milestone.title}`} onClose={onClose} width={460}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Progress note" value={note} onChange={setNote} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Input label="Progress %" type="number" value={progress} onChange={setProg} />
        <Select label="Status" value={status} onChange={setStatus} options={STATUSES.map(s => ({ value: s, label: s }))} />
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Saving…' : 'Log update'}</Btn>
      </div>
    </Modal>
  )
}
