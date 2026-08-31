import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H8 — salary increments (P13, HR-013). REAL, wired to hr-service.
//
// HR proposes, the MD approves, and approval writes the new salary assignment
// for the effective month — the payroll run then applies it on its own.
//
// Two hard gates stand in front: incomplete mandatory training (HR-029) and any
// expired professional certification (HR-035). They are enforced on the server
// and re-checked at approval, because eligibility can lapse between the
// proposal and the signature. This screen SHOWS the gates so HR knows what to
// fix; it never decides them.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)

const STATUS_VARIANT = { PendingMd: 'amber', Approved: 'green', Rejected: 'red', Withdrawn: 'default' }
const STATUS_LABEL = { PendingMd: 'With the MD', Approved: 'Approved', Rejected: 'Rejected', Withdrawn: 'Withdrawn' }

function NoAccess() {
  return (
    <Card style={{ padding: 28, textAlign: 'center' }}>
      <p style={{ fontSize: 32, margin: 0 }}>🔒</p>
      <p style={{ fontWeight: 700, color: T.navy, marginTop: 10 }}>Payroll data is restricted</p>
      <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6, maxWidth: 460, marginInline: 'auto' }}>
        Salary increments need the payroll permissions (<code>hr.payroll.read</code> and above).
      </p>
    </Card>
  )
}

const isForbidden = (e) => e?.response?.status === 403

function relay(flash, r) {
  const warnings = r?.warnings ?? []
  const message = r?.message ?? 'Done.'
  if (warnings.length) flash(`${message} — ${warnings.join(' ')}`, 'warning')
  else flash(message)
}
const relayError = (flash, e, fallback) => flash(e.response?.data?.message ?? fallback, 'error')

export function SalaryIncrementsTab({ flash }) {
  const [status, setStatus] = useState('')
  const [proposing, setProposing] = useState(false)
  const [state, setState] = useState({ loading: true, denied: false, data: null })

  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    hr.listIncrements({ status: status || undefined })
      .then(data => setState({ loading: false, denied: false, data }))
      .catch(e => setState({ loading: false, denied: isForbidden(e), data: null }))
  }, [status])
  useEffect(() => { load() }, [load])

  const decide = async (i, decision) => {
    const needsReason = decision !== 'Approve'
    const reason = needsReason
      ? window.prompt(`${decision} the increment to ${money(i.proposedSalary, i.currencyCode)} for ${i.employeeName}? Give a reason:`)
      : null
    if (needsReason && reason === null) return
    try { relay(flash, await hr.decideIncrement(i.id, { decision, reason })); load() }
    catch (e) { relayError(flash, e, 'Could not record the decision.') }
  }

  if (state.loading) return <Loading />
  if (state.denied) return <NoAccess />

  return (
    <div>
      <Alert type="info">
        <strong>HR proposes, the MD approves.</strong> Approval writes the new salary for the effective month
        and the payroll run applies it automatically — there is no separate pay change to make. Incomplete
        mandatory training or an expired professional certification blocks a proposal outright, and the gates
        are checked again at approval in case they have lapsed since.
      </Alert>

      <SectionHeader
        title="Salary Increments"
        sub="A rejected or withdrawn proposal leaves pay untouched. An approved one supersedes the previous salary and keeps it as history."
        action={<Btn size="sm" onClick={() => setProposing(true)}>+ Propose increment</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['PendingMd', 'Approved', 'Rejected', 'Withdrawn'].map(v =>
            <option key={v} value={v}>{STATUS_LABEL[v]}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Employee', 'Current', 'Proposed', 'Rise', 'Effective', 'Status', 'Proposed', 'Actions']}
        empty={status ? 'No increments match this filter.' : 'No increments raised yet.'}
        rows={(state.data ?? []).map(i => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{i.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{i.employeeName}</span>
            {i.justification && <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{i.justification}</span>}
          </span>,
          money(i.currentSalary, i.currencyCode),
          <strong>{money(i.proposedSalary, i.currencyCode)}</strong>,
          <span style={{ color: T.green, fontWeight: 600 }}>+{i.increasePercent}%</span>,
          <span style={{ fontFamily: 'monospace' }}>{i.effectivePeriodCode}</span>,
          <span>
            <Badge variant={STATUS_VARIANT[i.status] ?? 'default'}>{STATUS_LABEL[i.status] ?? i.status}</Badge>
            {i.decisionReason && <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 3 }}>{i.decisionReason}</span>}
          </span>,
          <span style={{ fontSize: 12 }}>{fmtDate(i.proposedAt)}</span>,
          i.status === 'PendingMd'
            ? <div style={{ display: 'flex', gap: 6 }}>
                <Btn size="sm" variant="green" onClick={() => decide(i, 'Approve')}>Approve</Btn>
                <Btn size="sm" variant="ghost" onClick={() => decide(i, 'Reject')}>Reject</Btn>
                <Btn size="sm" variant="ghost" onClick={() => decide(i, 'Withdraw')}>Withdraw</Btn>
              </div>
            : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
        ])}
      />

      {proposing && <ProposeIncrementModal flash={flash}
        onClose={() => setProposing(false)} onSaved={() => { setProposing(false); load() }} />}
    </div>
  )
}

function ProposeIncrementModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [periods, setPeriods] = useState([])
  const [preview, setPreview] = useState(null)
  const [checking, setChecking] = useState(false)
  const [f, setF] = useState({ employeeId: '', proposedSalary: '', effectivePeriodId: '', justification: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
    hr.listPayrollPeriods(new Date().getFullYear()).then(p => setPeriods(p ?? [])).catch(() => {})
  }, [])

  // The gates are the point of this screen — check as soon as an employee is picked, so HR sees what stands
  // in the way before typing a figure that cannot be proposed.
  const pick = async (employeeId) => {
    setF({ ...f, employeeId, effectivePeriodId: '' })
    setPreview(null)
    if (!employeeId) return
    setChecking(true)
    try {
      const p = await hr.previewIncrement(employeeId)
      setPreview(p)
      if (p.suggestedPeriodId) setF(s => ({ ...s, employeeId, effectivePeriodId: p.suggestedPeriodId }))
    } catch (e) { relayError(flash, e, 'Could not check eligibility.') }
    finally { setChecking(false) }
  }

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.proposeIncrement({
        employeeId: f.employeeId,
        proposedSalary: Number(f.proposedSalary),
        effectivePeriodId: f.effectivePeriodId || null,
        justification: f.justification || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not raise the proposal.') }
    finally { setBusy(false) }
  }

  const openPeriods = periods.filter(p => p.status === 'Open')
  const rise = preview?.currentSalary > 0 && Number(f.proposedSalary) > 0
    ? Number(f.proposedSalary) - preview.currentSalary : 0

  return (
    <Modal title="Propose a salary increment" onClose={onClose} width={620}>
      <Select label="Employee" value={f.employeeId} onChange={pick}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />

      {checking && <Loading />}

      {preview && (
        <>
          {preview.blockers?.length > 0 ? (
            <Alert type="error">
              <strong>Not eligible for an increment.</strong>
              <ul style={{ margin: '6px 0 0', paddingLeft: 18 }}>
                {preview.blockers.map((b, i) => <li key={i}>{b}</li>)}
              </ul>
            </Alert>
          ) : (
            <Alert type="success">Eligibility gates clear. Training hours: {preview.hoursYtd} of {preview.targetHours}.</Alert>
          )}

          {preview.warnings?.length > 0 && (
            <Alert type="warning">
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {preview.warnings.map((w, i) => <li key={i}>{w}</li>)}
              </ul>
            </Alert>
          )}

          {preview.hasCurrentSalary && (
            <Card style={{ padding: 12, marginBottom: 12 }}>
              <p style={{ margin: 0, fontSize: 13 }}>
                Currently on <strong>{money(preview.currentSalary, preview.currencyCode)}</strong> from{' '}
                <span style={{ fontFamily: 'monospace' }}>{preview.currentPeriodCode}</span>
                {preview.salaryStructureName && ` on ${preview.salaryStructureName}`}.
              </p>
            </Card>
          )}
        </>
      )}

      <Input label="Proposed salary" type="number" value={f.proposedSalary}
        onChange={v => setF({ ...f, proposedSalary: v })} required
        note={rise > 0 ? `A rise of ${money(rise, preview?.currencyCode)} (+${(rise / preview.currentSalary * 100).toFixed(1)}%)` : undefined} />

      <Select label="Effective from" value={f.effectivePeriodId} onChange={v => setF({ ...f, effectivePeriodId: v })}
        options={[{ value: '', label: 'Earliest available' },
          ...openPeriods.map(p => ({ value: p.id, label: p.code }))]} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Must be later than the period the current salary starts in. The payroll run for that month applies it
        without any further step.
      </p>

      <Input label="Justification" value={f.justification} onChange={v => setF({ ...f, justification: v })} />

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.proposedSalary || (preview?.blockers?.length ?? 0) > 0}>
          {busy ? 'Proposing…' : 'Propose'}
        </Btn>
      </div>
    </Modal>
  )
}
