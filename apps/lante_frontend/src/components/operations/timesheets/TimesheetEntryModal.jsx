import { useState, useEffect } from 'react'
import { Modal, Input, Select, Btn, Alert } from '../../ui.jsx'
import { listProjects } from '../../../services/operations.js'

// O11.2 — add / edit a timesheet entry. Overtime hours entered here start unapproved and need an
// approval to already exist in HR (checked from the row action) before the sheet can be submitted.
export default function TimesheetEntryModal({ entry, weekStart, onClose, onSave }) {
  const editing = !!entry?.id
  const [f, setF] = useState({
    workDate:      entry?.workDate?.slice(0, 10) ?? weekStart?.slice(0, 10) ?? new Date().toISOString().slice(0, 10),
    projectId:     entry?.projectId ?? '',
    description:   entry?.description ?? '',
    hours:         entry?.hours ?? '',
    overtimeHours: entry?.overtimeHours ?? '',
    // PR2 — defaults to billable; most logged time is chargeable, and an existing entry keeps its flag.
    isBillable:    entry?.isBillable ?? true,
  })
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')
  const [projects, setProjects] = useState([])
  const set = (k) => (v) => setF(prev => ({ ...prev, [k]: v }))

  useEffect(() => {
    let live = true
    listProjects({ page: 1, pageSize: 200, status: 'Active' })
      .then(r => { if (live) setProjects(r?.items ?? r ?? []) })
      .catch(() => {})
    return () => { live = false }
  }, [])

  const projectOptions = [
    { value: '', label: '— None —' },
    ...projects.map(p => ({ value: p.id, label: p.name })),
  ]

  const submit = async () => {
    if (!f.description.trim()) { setError('A description is required.'); return }
    if (!(Number(f.hours) > 0) && !(Number(f.overtimeHours) > 0)) { setError('Enter regular or overtime hours.'); return }
    setBusy(true); setError('')
    try {
      await onSave({
        workDate:      f.workDate,
        projectId:     f.projectId.trim() || null,
        description:   f.description.trim(),
        hours:         Number(f.hours) || 0,
        overtimeHours: Number(f.overtimeHours) || 0,
        isBillable:    f.isBillable,
      }, entry?.id)
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not save the entry.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={editing ? 'Edit Entry' : 'Add Entry'} onClose={onClose} width={480}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Work date" type="date" value={f.workDate} onChange={set('workDate')} required />
      <Select label="Project" value={f.projectId} onChange={set('projectId')} options={projectOptions} help="Optional — links the hours to a project for the labour journal." helpKey="ts-entry-project" />
      <Input label="Description" value={f.description} onChange={set('description')} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Input label="Regular hours"  type="number" value={f.hours}         onChange={set('hours')} />
        <Input label="Overtime hours" type="number" value={f.overtimeHours} onChange={set('overtimeHours')} note="Must already be approved in HR." />
      </div>
      <label style={{ display: 'flex', alignItems: 'flex-start', gap: 10, margin: '4px 0 8px', cursor: 'pointer' }}>
        <input
            type="checkbox"
            checked={f.isBillable}
            onChange={e => set('isBillable')(e.target.checked)}
            style={{ marginTop: 3 }}
        />
        <span>
          <span style={{ fontWeight: 600, fontSize: 13 }}>Chargeable to the client</span>
          <span style={{ display: 'block', fontSize: 12, color: '#9aa7b4' }}>
            Clear this for travel, rework or warranty time. It still costs the project and still posts
            to the labour journal — it just must not reach an invoice.
          </span>
        </span>
      </label>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Saving…' : editing ? 'Save' : 'Add'}</Btn>
      </div>
    </Modal>
  )
}
