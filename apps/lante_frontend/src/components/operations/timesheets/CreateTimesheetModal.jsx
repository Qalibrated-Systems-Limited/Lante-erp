import { useState } from 'react'
import { Modal, Input, Btn, Alert } from '../../ui.jsx'

// O11.2 — start a weekly timesheet. Any date in the target week is accepted; the backend normalizes
// it to Monday and rejects a duplicate for that week.
export default function CreateTimesheetModal({ onClose, onCreate }) {
  const [date, setDate]   = useState(new Date().toISOString().slice(0, 10))
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')

  const submit = async () => {
    setBusy(true); setError('')
    try {
      await onCreate(date)
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not create the timesheet.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title="New Weekly Timesheet" onClose={onClose} width={420}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Any date in the week" type="date" value={date} onChange={setDate}
        note="Normalized to the Monday of that week." />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Creating…' : 'Create'}</Btn>
      </div>
    </Modal>
  )
}
