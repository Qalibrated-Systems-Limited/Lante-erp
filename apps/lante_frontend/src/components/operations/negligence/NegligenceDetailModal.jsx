import { useState } from 'react'
import { Modal, Input, Select, Btn, Badge, Alert } from '../../ui.jsx'
import { RESPONDER_ROLES } from '../../../hooks/operations/useNegligence.js'

const fmt = (d) => (d ? new Date(d).toLocaleString() : '—')

// O11.4 — negligence incident detail + response. Approvers can respond (HR/DeptHead/MD) with an
// optional payroll deduction and can close the incident.
export default function NegligenceDetailModal({ incident, canRespond, onClose, onRespond }) {
  const closed = incident.status === 'Closed'
  const [f, setF] = useState({ responderRole: 'HR', responseText: '', actionTaken: '', payrollDeductionAmount: '', closeIncident: false })
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')
  const set = (k) => (v) => setF(prev => ({ ...prev, [k]: v }))

  const submit = async () => {
    if (!f.responseText.trim()) { setError('A response is required.'); return }
    setBusy(true); setError('')
    try {
      await onRespond(incident.id, {
        responderRole: f.responderRole, responseText: f.responseText.trim(),
        actionTaken: f.actionTaken.trim() || null,
        payrollDeductionAmount: f.payrollDeductionAmount === '' ? null : Number(f.payrollDeductionAmount),
        closeIncident: f.closeIncident,
      })
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not save the response.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={incident.incidentNumber} onClose={onClose} width={640}>
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
        <Badge variant={incident.status === 'Closed' ? 'green' : incident.status === 'Responded' ? 'blue' : 'amber'}>{incident.status}</Badge>
        <Badge variant={incident.severity === 'Critical' || incident.severity === 'Major' ? 'red' : 'default'}>{incident.severity}</Badge>
        {incident.loggedLate && <Badge variant="amber">Logged late</Badge>}
        {incident.isRepeatOffense && <Badge variant="red">Repeat offense</Badge>}
        {incident.finalWarningIssued && <Badge variant="red">Final warning</Badge>}
        {incident.escalatedToMdAt && <Badge variant="purple">Escalated to MD</Badge>}
      </div>

      <div style={{ fontSize: 13, color: '#334', lineHeight: 1.7 }}>
        <div><b>Employee:</b> {incident.employeeName} ({incident.employeeId})</div>
        <div><b>Title:</b> {incident.title}</div>
        {incident.description && <div><b>Details:</b> {incident.description}</div>}
        <div><b>Occurred:</b> {fmt(incident.occurredAt)} · <b>Reported:</b> {fmt(incident.reportedAt)}</div>
        <div><b>Response deadline:</b> {fmt(incident.responseDeadline)}</div>
      </div>

      <div style={{ marginTop: 14, borderTop: '1px solid #eef1f5', paddingTop: 12 }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#5b6b7c', marginBottom: 8 }}>Responses ({(incident.responses ?? []).length})</div>
        {(incident.responses ?? []).length === 0
          ? <p style={{ fontSize: 12, color: '#9aa7b4' }}>No responses yet.</p>
          : (incident.responses ?? []).map(r => (
            <div key={r.id} style={{ background: '#f7f9fb', borderRadius: 8, padding: 10, marginBottom: 8 }}>
              <div style={{ fontSize: 12, color: '#334' }}><b>{r.responderRole}</b> · {r.responderName} · {fmt(r.respondedAt)}</div>
              <div style={{ fontSize: 13, color: '#334', marginTop: 4 }}>{r.responseText}</div>
              {r.actionTaken && <div style={{ fontSize: 12, color: '#5b6b7c', marginTop: 3 }}>Action: {r.actionTaken}</div>}
              {r.payrollDeductionAmount > 0 && <div style={{ fontSize: 12, color: '#c0392b', marginTop: 3 }}>Payroll deduction: {Number(r.payrollDeductionAmount).toLocaleString()}</div>}
            </div>
          ))}
      </div>

      {canRespond && !closed && (
        <div style={{ marginTop: 14, borderTop: '1px solid #eef1f5', paddingTop: 12 }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: '#5b6b7c', marginBottom: 8 }}>Add response</div>
          {error && <Alert type="error">{error}</Alert>}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <Select label="Responding as" value={f.responderRole} onChange={set('responderRole')} options={RESPONDER_ROLES.map(r => ({ value: r, label: r }))} />
            <Input label="Payroll deduction" type="number" value={f.payrollDeductionAmount} onChange={set('payrollDeductionAmount')} note="Optional; posts to HR." />
          </div>
          <Input label="Response" value={f.responseText} onChange={set('responseText')} required />
          <Input label="Action taken" value={f.actionTaken} onChange={set('actionTaken')} />
          <label style={{ fontSize: 12, color: '#5b6b7c', display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="checkbox" checked={f.closeIncident} onChange={e => set('closeIncident')(e.target.checked)} /> Close incident
          </label>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
            <Btn variant="ghost" onClick={onClose}>Close</Btn>
            <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Saving…' : 'Submit Response'}</Btn>
          </div>
        </div>
      )}
    </Modal>
  )
}
