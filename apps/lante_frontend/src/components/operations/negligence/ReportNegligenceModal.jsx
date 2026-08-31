import { useState } from 'react'
import { Modal, Input, Select, Btn, Alert } from '../../ui.jsx'
import { SEVERITIES } from '../../../hooks/operations/useNegligence.js'

// O11.4 — report a negligence incident. Occurrence date matters: >24h before "now" is flagged as a
// late log by the backend, and a 2nd incident for the employee within 12 months is a repeat offense.
export default function ReportNegligenceModal({ onClose, onSave }) {
  const [f, setF] = useState({
    employeeId: '', employeeName: '', projectId: '', title: '', description: '',
    severity: 'Minor', occurredAt: new Date().toISOString().slice(0, 16),
  })
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')
  const set = (k) => (v) => setF(prev => ({ ...prev, [k]: v }))

  const submit = async () => {
    if (!f.employeeId.trim() || !f.title.trim()) { setError('Employee and title are required.'); return }
    setBusy(true); setError('')
    try {
      await onSave({
        employeeId: f.employeeId.trim(), employeeName: f.employeeName.trim(),
        projectId: f.projectId.trim() || null, title: f.title.trim(), description: f.description.trim(),
        severity: f.severity, occurredAt: new Date(f.occurredAt).toISOString(),
      })
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not log the incident.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title="Report Negligence Incident" onClose={onClose} width={560}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Input label="Employee ID"   value={f.employeeId}   onChange={set('employeeId')} required />
        <Input label="Employee name" value={f.employeeName} onChange={set('employeeName')} />
      </div>
      <Input label="Title" value={f.title} onChange={set('title')} required />
      <Input label="Description" value={f.description} onChange={set('description')} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Select label="Severity" value={f.severity} onChange={set('severity')} options={SEVERITIES.map(s => ({ value: s, label: s }))} />
        <Input label="Occurred at" type="datetime-local" value={f.occurredAt} onChange={set('occurredAt')} />
      </div>
      <Input label="Project ID (optional)" value={f.projectId} onChange={set('projectId')} />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Logging…' : 'Log Incident'}</Btn>
      </div>
    </Modal>
  )
}
