import { useState } from 'react'
import { Card, Btn, Badge, Loading, Alert } from '../../ui.jsx'
import { useHandovers, HANDOVER_STEPS, SIGNATURE_ROLES } from '../../../hooks/operations/useHandovers.js'
import HandoverSignatureModal from './HandoverSignatureModal.jsx'

const STATUS_VARIANT = { Draft: 'default', InProgress: 'amber', Completed: 'green' }
const parseSteps = (json) => {
  try { const s = JSON.parse(json); if (Array.isArray(s)) return s } catch { /* seed below */ }
  return HANDOVER_STEPS.map(label => ({ label, done: false }))
}

// O11.4 — project handover tab: 8-step checklist + four mandatory signatures + permanent completion.
export default function HandoverTab({ projectId }) {
  const { items, loading, error, canWrite, canComplete, create, update, sign, complete } = useHandovers(projectId)
  const [sigModal, setSigModal] = useState(null)   // { role, roleLabel, existing }
  const guard = (p) => p.catch(() => {})

  if (loading) return <Loading />

  const handover = items[0] ?? null
  const completed = handover?.status === 'Completed'

  if (!handover) {
    return (
      <Card>
        <p style={{ fontSize: 13, color: '#5b6b7c', marginBottom: 14 }}>No handover started. The close-out requires an 8-step checklist and four mandatory signatures.</p>
        {canWrite && <Btn variant="primary" onClick={() => guard(create({ stepsJson: JSON.stringify(HANDOVER_STEPS.map(label => ({ label, done: false }))) }))}>Start Handover</Btn>}
      </Card>
    )
  }

  const steps = parseSteps(handover.stepsJson)
  const toggleStep = (i) => {
    if (completed || !canWrite) return
    const next = steps.map((s, j) => (j === i ? { ...s, done: !s.done } : s))
    guard(update(handover.id, { stepsJson: JSON.stringify(next) }))
  }
  const sigByRole = Object.fromEntries((handover.signatures ?? []).map(s => [s.role, s]))
  const missing = handover.missingMandatoryRoles ?? []

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 16 }}>
      {error && <div style={{ gridColumn: '1 / -1' }}><Alert type="error">{error}</Alert></div>}

      {/* Checklist */}
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: '#1b3a5c', margin: 0 }}>{handover.handoverNumber}</h3>
          <div style={{ display: 'flex', gap: 6 }}>
            {completed && <Badge variant="navy">Permanent</Badge>}
            <Badge variant={STATUS_VARIANT[handover.status] ?? 'default'}>{handover.status}</Badge>
          </div>
        </div>
        <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
          {steps.map((s, i) => (
            <li key={i} style={{ display: 'flex', gap: 10, alignItems: 'center', fontSize: 13, color: '#334' }}>
              <input type="checkbox" checked={!!s.done} disabled={completed || !canWrite} onChange={() => toggleStep(i)} />
              <span style={{ textDecoration: s.done ? 'line-through' : 'none', color: s.done ? '#9aa7b4' : '#334' }}>{s.label}</span>
            </li>
          ))}
        </ul>
      </Card>

      {/* Signatures */}
      <Card>
        <h3 style={{ fontSize: 14, fontWeight: 700, color: '#1b3a5c', margin: '0 0 12px' }}>Mandatory Signatures</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {SIGNATURE_ROLES.map(r => {
            const sig = sigByRole[r.value]
            return (
              <div key={r.value} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#334' }}>{r.label}</div>
                  {sig
                    ? <div style={{ fontSize: 11, color: '#9aa7b4' }}>{sig.signatoryName} · {sig.signedAt?.slice(0, 10)}</div>
                    : <div style={{ fontSize: 11, color: '#c0392b' }}>Not signed</div>}
                </div>
                {sig
                  ? <Badge variant="green">✓ Signed</Badge>
                  : (canWrite && !completed && <Btn size="sm" variant="outline" onClick={() => setSigModal({ role: r.value, roleLabel: r.label })}>Sign</Btn>)}
              </div>
            )
          })}
        </div>

        <div style={{ marginTop: 16, borderTop: '1px solid #eef1f5', paddingTop: 14 }}>
          {completed
            ? <Alert type="success">Handover completed on {handover.completedAt?.slice(0, 10)} — permanent record.</Alert>
            : (
              <Btn variant="green" disabled={!canComplete || missing.length > 0} onClick={() => guard(complete(handover.id))}>
                {missing.length > 0 ? `Awaiting ${missing.length} signature(s)` : 'Complete Handover'}
              </Btn>
            )}
          {!completed && !canComplete && <p style={{ fontSize: 11, color: '#9aa7b4', marginTop: 6 }}>Completion needs the projects.approve permission.</p>}
        </div>
      </Card>

      {sigModal && (
        <HandoverSignatureModal
          role={sigModal.role} roleLabel={sigModal.roleLabel}
          onClose={() => setSigModal(null)}
          onSave={(dto) => sign(handover.id, dto)}
        />
      )}
    </div>
  )
}
