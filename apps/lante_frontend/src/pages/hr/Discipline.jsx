import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H10 — discipline, warnings, grievances and separation (P18–P21). REAL.
//
// This is the part of HR whose record may have to stand up outside the company,
// so the screens follow the process rather than offering a free-for-all: each
// case shows only the action it is actually ready for, and the server refuses
// anything out of turn. Two people are needed at the sharp ends — whoever
// records an outcome cannot decide the appeal against it, and whoever prepares
// final dues cannot approve them.
//
// Deadlines shown here are WORKING days, computed on the same calendar leave
// and attendance use.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)

const CASE_VARIANT = {
  Open: 'default', ShowCauseIssued: 'amber', AwaitingHearing: 'blue',
  OutcomeRecorded: 'purple', Appealed: 'amber', Closed: 'green', Withdrawn: 'default',
}
const OUTCOME_VARIANT = { NoAction: 'green', Warning: 'amber', Suspension: 'red', Termination: 'red', None: 'default' }
const WARNING_VARIANT = { Verbal: 'default', Written: 'amber', FinalWritten: 'red' }
const GRIEVANCE_VARIANT = {
  Submitted: 'amber', Acknowledged: 'blue', Investigating: 'blue',
  Resolved: 'green', PartiallyResolved: 'amber', Escalated: 'red', Withdrawn: 'default',
}
const SEP_VARIANT = { Draft: 'default', PendingMd: 'amber', Approved: 'blue', Paid: 'green', Cancelled: 'red' }

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 20, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

function relay(flash, r) {
  const warnings = r?.warnings ?? []
  const message = r?.message ?? 'Done.'
  if (warnings.length) flash(`${message} — ${warnings.join(' ')}`, 'warning')
  else flash(message)
}
const relayError = (flash, e, fallback) => flash(e.response?.data?.message ?? fallback, 'error')

function useData(loader, deps = []) {
  const [state, setState] = useState({ loading: true, denied: false, data: null })
  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    loader().then(data => setState({ loading: false, denied: false, data }))
      .catch(e => setState({ loading: false, denied: e?.response?.status === 403, data: null }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
  useEffect(() => { load() }, [load])
  return { ...state, reload: load }
}

function SweepButton({ flash, onDone }) {
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const r = await hr.runDisciplineSweep()
      const n = r.warningsExpired + r.warningEscalations + r.showCauseOverdueAlerts + r.grievanceSlaBreaches
      flash(n === 0
        ? 'Sweep complete — nothing new to raise.'
        : `Sweep: ${r.warningsExpired} warning(s) expired, ${r.warningEscalations} termination review(s), ${r.showCauseOverdueAlerts} show-cause overdue, ${r.grievanceSlaBreaches} grievance SLA breach(es).`)
      onDone?.()
    } catch (e) { relayError(flash, e, 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Sweeping…' : 'Run sweep'}</Btn>
}

// ═════════════════════════════════════════════════════════════════════════════
// Disciplinary cases and warnings (P18 + P19)
// ═════════════════════════════════════════════════════════════════════════════
export function DisciplinaryTab({ flash }) {
  const [status, setStatus] = useState('')
  const [opening, setOpening] = useState(false)
  const [acting, setActing] = useState(null)
  const [warningFor, setWarningFor] = useState(false)

  const summary = useData(() => hr.disciplineSummary(), [])
  const cases = useData(() => hr.listCases({ status: status || undefined }), [status])
  const warnings = useData(() => hr.listWarnings({ includeExpired: true }), [])

  if (cases.loading) return <Loading />
  const s = summary.data
  const reload = () => { summary.reload(); cases.reload(); warnings.reload() }

  return (
    <div>
      <Alert type="info">
        <strong>Incident → show cause → hearing → outcome → appeal.</strong> Each stage waits on the one before
        it, and the response window is five <em>working</em> days. Whoever records an outcome cannot decide the
        appeal against it.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Open cases" value={s?.openCases ?? 0} />
        <Kpi label="Awaiting response" value={s?.awaitingResponse ?? 0} color={T.amber} />
        <Kpi label="Response overdue" value={s?.responseOverdue ?? 0} color={s?.responseOverdue ? T.red : T.green} />
        <Kpi label="Awaiting hearing" value={s?.awaitingHearing ?? 0} color={T.blue} />
        <Kpi label="Under appeal" value={s?.underAppeal ?? 0} />
        <Kpi label="Active warnings" value={s?.activeWarnings ?? 0} />
        <Kpi label="At review threshold" value={s?.employeesAtReviewThreshold ?? 0}
          color={s?.employeesAtReviewThreshold ? T.red : T.green} sub="3+ in 12 months" />
      </div>

      <SectionHeader
        title="Disciplinary Cases"
        sub="The full trail is kept because it may be read outside the company."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            <SweepButton flash={flash} onDone={reload} />
            <Btn size="sm" onClick={() => setOpening(true)}>+ Case</Btn>
          </div>
        }
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Open', 'ShowCauseIssued', 'AwaitingHearing', 'OutcomeRecorded', 'Appealed', 'Closed', 'Withdrawn'].map(v =>
            <option key={v} value={v}>{v}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Case', 'Employee', 'Incident', 'Status', 'Outcome', 'Next step', 'Actions']}
        empty="No disciplinary cases."
        rows={(cases.data ?? []).map(c => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{c.caseNumber}</span>,
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{c.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{c.employeeName}</span>
            {c.sourceModule && <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>from {c.sourceModule}</span>}
          </span>,
          <span>
            {fmtDate(c.incidentDate)}
            <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{(c.description ?? '').slice(0, 40)}</span>
          </span>,
          <span>
            <Badge variant={CASE_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>
            {c.responseOverdue && <Badge variant="red">overdue</Badge>}
          </span>,
          c.outcome === 'None' ? '—' : <Badge variant={OUTCOME_VARIANT[c.outcome] ?? 'default'}>{c.outcome}</Badge>,
          <span style={{ fontSize: 12, color: T.mgrey }}>{c.nextStep}</span>,
          ['Closed', 'Withdrawn'].includes(c.status)
            ? <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>
            : <Btn size="sm" onClick={() => setActing(c)}>Open</Btn>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="Warnings"
          sub="A verbal warning stands six months, anything written twelve. Three live inside a rolling twelve months triggers a termination review."
          action={<Btn size="sm" onClick={() => setWarningFor(true)}>+ Warning</Btn>}
        />
      </div>
      {warnings.loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Type', 'Issued', 'Expires', 'Reason', 'Acknowledged', 'Live in 12mo']}
          empty="No warnings on file."
          rows={(warnings.data ?? []).map(w => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{w.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{w.employeeName}</span>
            </span>,
            <Badge variant={WARNING_VARIANT[w.warningType] ?? 'default'}>{w.warningType}</Badge>,
            fmtDate(w.issuedDate),
            <span style={{ color: w.isActive ? T.dgrey : T.mgrey }}>
              {fmtDate(w.expiryDate)}{!w.isActive && ' (expired)'}
            </span>,
            <span style={{ fontSize: 12, color: T.mgrey }}>{(w.reason ?? '').slice(0, 44)}</span>,
            w.acknowledgedByEmployee
              ? <Badge variant="green">{fmtDate(w.acknowledgedAt)}</Badge>
              : <Badge variant="amber">not yet</Badge>,
            <strong style={{ color: w.activeInLast12Months >= 3 ? T.red : T.dgrey }}>{w.activeInLast12Months}</strong>,
          ])}
        />
      )}

      {opening && <OpenCaseModal flash={flash} onClose={() => setOpening(false)}
        onSaved={() => { setOpening(false); reload() }} />}
      {acting && <CaseModal caseId={acting.id} flash={flash}
        onClose={() => setActing(null)} onSaved={reload} />}
      {warningFor && <WarningModal flash={flash} onClose={() => setWarningFor(false)}
        onSaved={() => { setWarningFor(false); reload() }} />}
    </div>
  )
}

function OpenCaseModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    employeeId: '', incidentDate: new Date().toISOString().slice(0, 10),
    description: '', witnesses: '', sourceModule: '', sourceReference: '',
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.openCase({
        employeeId: f.employeeId, incidentDate: `${f.incidentDate}T00:00:00Z`,
        description: f.description, witnesses: f.witnesses || null,
        sourceModule: f.sourceModule || null, sourceReference: f.sourceReference || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not open the case.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Open a disciplinary case" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Input label="Incident date" type="date" value={f.incidentDate} onChange={v => setF({ ...f, incidentDate: v })} required />
      <Input label="What happened" value={f.description} onChange={v => setF({ ...f, description: v })} required />
      <Input label="Witnesses" value={f.witnesses} onChange={v => setF({ ...f, witnesses: v })} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Raised from" value={f.sourceModule} onChange={v => setF({ ...f, sourceModule: v })}
          options={[{ value: '', label: 'Raised directly by HR' },
            { value: 'Operations', label: 'An operations negligence incident' },
            { value: 'HR', label: 'A failed improvement plan' }]} />
        <Input label="Reference" value={f.sourceReference} onChange={v => setF({ ...f, sourceReference: v })}
          note="The other record's number" />
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Raising a case from another module points at that record — it does not copy it. Operations keeps owning
        its negligence log; this is the formal process that may follow from it.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.description.trim()}>{busy ? 'Opening…' : 'Open case'}</Btn>
      </div>
    </Modal>
  )
}

function CaseModal({ caseId, flash, onClose, onSaved }) {
  const [f, setF] = useState({})
  const [busy, setBusy] = useState(false)
  const detail = useData(() => hr.getCase(caseId), [caseId])

  if (detail.loading) return <Modal title="Case" onClose={onClose}><Loading /></Modal>
  const c = detail.data
  if (!c) return <Modal title="Case" onClose={onClose}><Alert type="error">Case not found.</Alert></Modal>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload(); onSaved() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${c.caseNumber} — ${c.employeeName}`} onClose={onClose} width={680}>
      <Alert type="info"><strong>{c.nextStep}</strong></Alert>

      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}><strong>Incident {fmtDate(c.incidentDate)}:</strong> {c.description}</p>
        {c.witnesses && <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>Witnesses: {c.witnesses}</p>}
        {c.showCauseIssuedAt && (
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            Show cause issued {fmtDate(c.showCauseIssuedAt)}, response due {fmtDate(c.responseDeadline)}
            {c.employeeRespondedAt ? ` — responded ${fmtDate(c.employeeRespondedAt)}` : c.responseOverdue ? ' — OVERDUE' : ''}
          </p>
        )}
        {c.employeeResponse && <p style={{ margin: '4px 0 0', fontSize: 12, fontStyle: 'italic' }}>“{c.employeeResponse}”</p>}
        {c.hearingDate && <p style={{ margin: '6px 0 0', fontSize: 12 }}>Hearing {fmtDate(c.hearingDate)} — {c.hearingPanel ?? 'panel not named'}</p>}
        {c.outcome !== 'None' && (
          <p style={{ margin: '6px 0 0', fontSize: 13 }}>
            <strong>Outcome: {c.outcome}</strong> — {c.outcomeNotes}
            {c.rightOfAppealDeadline && <span style={{ display: 'block', fontSize: 12, color: T.mgrey }}>Appeal by {fmtDate(c.rightOfAppealDeadline)}</span>}
          </p>
        )}
        {c.appealGrounds && <p style={{ margin: '6px 0 0', fontSize: 12 }}>Appeal: {c.appealGrounds}{c.appealOutcome && ` → ${c.appealOutcome}`}</p>}
      </Card>

      {c.status === 'Open' && (
        <>
          <Input label="Show-cause letter" value={f.letter ?? ''} onChange={v => setF({ ...f, letter: v })} />
          <Input label="Response window (working days)" type="number" value={f.responseDays ?? 5}
            onChange={v => setF({ ...f, responseDays: v })} />
          <Btn onClick={() => act(() => hr.issueShowCause(caseId, { letter: f.letter, responseDays: Number(f.responseDays) || 5 }), 'Could not issue.')}
            disabled={busy}>Issue show cause</Btn>
        </>
      )}

      {c.status === 'ShowCauseIssued' && (
        <>
          <Input label="Employee response" value={f.response ?? ''} onChange={v => setF({ ...f, response: v })}
            note="Leave blank if none was given — the hearing can proceed on the papers" />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Hearing date" type="date" value={f.hearingDate ?? ''} onChange={v => setF({ ...f, hearingDate: v })} />
            <Input label="Hearing panel" value={f.hearingPanel ?? ''} onChange={v => setF({ ...f, hearingPanel: v })} />
          </div>
          <Btn onClick={() => act(() => hr.recordCaseResponse(caseId, {
            response: f.response || null,
            hearingDate: f.hearingDate ? `${f.hearingDate}T00:00:00Z` : null,
            hearingPanel: f.hearingPanel || null,
          }), 'Could not record.')} disabled={busy}>Record response</Btn>
        </>
      )}

      {c.status === 'AwaitingHearing' && (
        <>
          <Select label="Outcome" value={f.outcome ?? ''} onChange={v => setF({ ...f, outcome: v })}
            options={[{ value: '', label: 'Select…' },
              { value: 'NoAction', label: 'No action — no case to answer' },
              { value: 'Warning', label: 'Warning' },
              { value: 'Suspension', label: 'Suspension' },
              { value: 'Termination', label: 'Termination' }]} required />
          {f.outcome === 'Warning' && (
            <Select label="Warning type" value={f.warningType ?? ''} onChange={v => setF({ ...f, warningType: v })}
              options={[{ value: '', label: 'Select…' },
                { value: 'Verbal', label: 'Verbal (6 months)' },
                { value: 'Written', label: 'Written (12 months)' },
                { value: 'FinalWritten', label: 'Final written (12 months)' }]} required />
          )}
          <Input label="Why" value={f.notes ?? ''} onChange={v => setF({ ...f, notes: v })} required
            note="This record may be read outside the company" />
          <Input label="Hearing notes" value={f.hearingNotes ?? ''} onChange={v => setF({ ...f, hearingNotes: v })} />
          {f.outcome === 'Termination' && (
            <Alert type="warning">
              Recording a termination does not end the employment on its own — open a separation to compute and
              approve the final dues. The employee stays active until that is paid.
            </Alert>
          )}
          <Btn onClick={() => act(() => hr.recordCaseOutcome(caseId, {
            outcome: f.outcome, warningType: f.warningType || null,
            notes: f.notes, hearingNotes: f.hearingNotes || null,
          }), 'Could not record the outcome.')}
            disabled={busy || !f.outcome || !f.notes?.trim() || (f.outcome === 'Warning' && !f.warningType)}>
            Record outcome
          </Btn>
        </>
      )}

      {c.status === 'OutcomeRecorded' && (
        <>
          <Input label="Appeal grounds" value={f.grounds ?? ''} onChange={v => setF({ ...f, grounds: v })}
            note={`Appeal window closes ${fmtDate(c.rightOfAppealDeadline)}`} />
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn onClick={() => act(() => hr.recordCaseAppeal(caseId, { grounds: f.grounds }), 'Could not lodge the appeal.')}
              disabled={busy || !f.grounds?.trim()}>Lodge appeal</Btn>
            <Btn variant="ghost" onClick={() => act(() => hr.closeCase(caseId, f.grounds || 'Case closed.'), 'Could not close.')}
              disabled={busy}>Close case</Btn>
          </div>
        </>
      )}

      {c.status === 'Appealed' && (
        <>
          <Input label="Appeal decision" value={f.decision ?? ''} onChange={v => setF({ ...f, decision: v })} required />
          <p style={{ fontSize: 12, color: T.mgrey }}>
            This needs someone other than whoever recorded the outcome.
          </p>
          <Btn onClick={() => act(() => hr.recordCaseAppeal(caseId, { decision: f.decision }), 'Could not decide the appeal.')}
            disabled={busy || !f.decision?.trim()}>Decide appeal</Btn>
        </>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Close</Btn>
      </div>
    </Modal>
  )
}

function WarningModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    employeeId: '', warningType: 'Verbal',
    incidentDate: new Date().toISOString().slice(0, 10), reason: '',
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.issueWarning({
        employeeId: f.employeeId, warningType: f.warningType,
        incidentDate: `${f.incidentDate}T00:00:00Z`, reason: f.reason || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not issue the warning.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Issue a warning" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Select label="Type" value={f.warningType} onChange={v => setF({ ...f, warningType: v })}
        options={[{ value: 'Verbal', label: 'Verbal — stands 6 months' },
          { value: 'Written', label: 'Written — stands 12 months' },
          { value: 'FinalWritten', label: 'Final written — stands 12 months' }]} />
      <Input label="Incident date" type="date" value={f.incidentDate} onChange={v => setF({ ...f, incidentDate: v })} />
      <Input label="Reason" value={f.reason} onChange={v => setF({ ...f, reason: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        The employee acknowledges it themselves — an unacknowledged warning is weaker evidence if it is ever
        relied on.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId}>{busy ? 'Issuing…' : 'Issue warning'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Grievances (P20)
// ═════════════════════════════════════════════════════════════════════════════
export function GrievancesTab({ flash }) {
  const [status, setStatus] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [acting, setActing] = useState(null)
  const rows = useData(() => hr.listGrievances({ status: status || undefined }), [status])

  if (rows.loading) return <Loading />

  const act = async (fn, fallback) => {
    try { relay(flash, await fn()); rows.reload() }
    catch (e) { relayError(flash, e, fallback) }
  }

  return (
    <div>
      <Alert type="info">
        <strong>HR must acknowledge within two working days</strong> (HR-023) — missing that escalates to the MD
        automatically. A grievance marked confidential is restricted to HR and the assigned investigator, and
        nobody may investigate their own.
      </Alert>

      <SectionHeader title="Grievances" sub="Submission → acknowledgement → investigation → outcome."
        action={<Btn size="sm" onClick={() => setSubmitting(true)}>+ Grievance</Btn>} />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Submitted', 'Acknowledged', 'Investigating', 'Resolved', 'PartiallyResolved', 'Escalated'].map(v =>
            <option key={v} value={v}>{v}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Case', 'Raised by', 'Category', 'Submitted', 'Status', 'Investigator', 'Next step', 'Actions']}
        empty="No grievances."
        rows={(rows.data ?? []).map(g => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{g.caseNumber}</span>,
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{g.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{g.employeeName}</span>
            {g.isConfidential && <Badge variant="navy">confidential</Badge>}
          </span>,
          <span>{g.category}<span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{(g.description ?? '').slice(0, 36)}</span></span>,
          <span>
            {fmtDate(g.submittedAt)}
            {g.slaBreached && <span style={{ display: 'block', fontSize: 11, color: T.red }}>SLA breached</span>}
          </span>,
          <Badge variant={GRIEVANCE_VARIANT[g.status] ?? 'default'}>{g.status}</Badge>,
          g.assignedToName ?? '—',
          <span style={{ fontSize: 12, color: T.mgrey }}>{g.nextStep}</span>,
          <div style={{ display: 'flex', gap: 6 }}>
            {g.status === 'Submitted' &&
              <Btn size="sm" onClick={() => act(() => hr.acknowledgeGrievance(g.id), 'Could not acknowledge.')}>Acknowledge</Btn>}
            {['Submitted', 'Acknowledged'].includes(g.status) &&
              <Btn size="sm" variant="outline" onClick={() => setActing({ ...g, mode: 'assign' })}>Assign</Btn>}
            {g.status === 'Investigating' &&
              <Btn size="sm" onClick={() => setActing({ ...g, mode: 'resolve' })}>Resolve</Btn>}
          </div>,
        ])}
      />

      {submitting && <GrievanceModal flash={flash} onClose={() => setSubmitting(false)}
        onSaved={() => { setSubmitting(false); rows.reload() }} />}
      {acting && <GrievanceActionModal grievance={acting} flash={flash}
        onClose={() => setActing(null)} onSaved={() => { setActing(null); rows.reload() }} />}
    </div>
  )
}

function GrievanceModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({ employeeId: '', category: '', description: '', isConfidential: false })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try { relay(flash, await hr.submitGrievance(f)); onSaved() }
    catch (e) { relayError(flash, e, 'Could not submit the grievance.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Raise a grievance" onClose={onClose}>
      <Select label="Raised by" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Input label="Category" value={f.category} onChange={v => setF({ ...f, category: v })} required
        note="e.g. Working conditions, Treatment by a colleague, Pay" />
      <Input label="What happened" value={f.description} onChange={v => setF({ ...f, description: v })} required />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 8 }}>
        <input type="checkbox" checked={f.isConfidential} onChange={e => setF({ ...f, isConfidential: e.target.checked })} />
        Confidential — restrict the detail to HR and the investigator
      </label>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.category.trim() || !f.description.trim()}>
          {busy ? 'Submitting…' : 'Submit'}
        </Btn>
      </div>
    </Modal>
  )
}

function GrievanceActionModal({ grievance, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({ investigatorEmployeeId: '', outcome: '', findings: '', outcomeNotes: '', followUpActions: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 })
      .then(r => setEmployees((r.data ?? []).filter(e => e.id !== grievance.employeeId))).catch(() => {})
  }, [grievance.employeeId])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, grievance.mode === 'assign'
        ? await hr.assignGrievance(grievance.id, { investigatorEmployeeId: f.investigatorEmployeeId })
        : await hr.resolveGrievance(grievance.id, {
            outcome: f.outcome, findings: f.findings || null,
            outcomeNotes: f.outcomeNotes || null, followUpActions: f.followUpActions || null,
          }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${grievance.caseNumber} — ${grievance.mode === 'assign' ? 'assign investigator' : 'record outcome'}`} onClose={onClose}>
      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}><strong>{grievance.category}</strong> — {grievance.description}</p>
      </Card>

      {grievance.mode === 'assign' ? (
        <>
          <Select label="Investigator" value={f.investigatorEmployeeId}
            onChange={v => setF({ ...f, investigatorEmployeeId: v })}
            options={[{ value: '', label: 'Select…' },
              ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
          <p style={{ fontSize: 12, color: T.mgrey }}>The person who raised it cannot investigate it.</p>
        </>
      ) : (
        <>
          <Select label="Outcome" value={f.outcome} onChange={v => setF({ ...f, outcome: v })}
            options={[{ value: '', label: 'Select…' },
              { value: 'Resolved', label: 'Resolved — closed' },
              { value: 'PartiallyResolved', label: 'Partially resolved — follow-up needed' },
              { value: 'Escalated', label: 'Unresolved — escalate to the MD' }]} required />
          <Input label="Findings" value={f.findings} onChange={v => setF({ ...f, findings: v })} />
          <Input label="What was decided" value={f.outcomeNotes} onChange={v => setF({ ...f, outcomeNotes: v })} required />
          {f.outcome === 'PartiallyResolved' && (
            <Input label="Follow-up actions" value={f.followUpActions} onChange={v => setF({ ...f, followUpActions: v })} required
              note="Without these, a partial resolution is just an unresolved grievance with a nicer label" />
          )}
        </>
      )}

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || (grievance.mode === 'assign' ? !f.investigatorEmployeeId : !f.outcome || !f.outcomeNotes.trim())}>
          {busy ? 'Saving…' : 'Save'}
        </Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Separation and final dues (P21)
// ═════════════════════════════════════════════════════════════════════════════
export function SeparationsTab({ flash }) {
  const [status, setStatus] = useState('')
  const [initiating, setInitiating] = useState(false)
  const [openId, setOpenId] = useState(null)
  const rows = useData(() => hr.listSeparations({ status: status || undefined }), [status])

  if (rows.loading) return <Loading />
  if (rows.denied) return (
    <Card style={{ padding: 28, textAlign: 'center' }}>
      <p style={{ fontSize: 32, margin: 0 }}>🔒</p>
      <p style={{ fontWeight: 700, color: T.navy, marginTop: 10 }}>Final dues are restricted</p>
      <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6 }}>
        Separations carry pay data and need the payroll permissions.
      </p>
    </Card>
  )

  if (openId) return <SeparationDetail id={openId} flash={flash}
    onBack={() => { setOpenId(null); rows.reload() }} />

  return (
    <div>
      <Alert type="info">
        <strong>The employee stays on the payroll until the final dues are PAID</strong> — not when the
        separation is approved. Deactivating at approval would drop them from a run that still owed them money.
        Revoking their login is a separate step in user administration; HR cannot do it from here.
      </Alert>

      <SectionHeader title="Separations"
        sub="Every component of the dues is stored, not just the total — a leaver disputing their payment needs the arithmetic."
        action={<Btn size="sm" onClick={() => setInitiating(true)}>+ Separation</Btn>} />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Draft', 'PendingMd', 'Approved', 'Paid', 'Cancelled'].map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Reference', 'Employee', 'Type', 'Leaves', 'Service', 'Net dues', 'Status', '']}
        empty="No separations."
        rows={(rows.data ?? []).map(s => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{s.separationNumber}</span>,
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{s.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{s.employeeName}</span>
          </span>,
          s.separationType,
          fmtDate(s.effectiveDate),
          `${s.yearsOfService} yr`,
          <strong style={{ color: s.netDues < 0 ? T.red : T.dgrey }}>{money(s.netDues, s.currencyCode)}</strong>,
          <span>
            <Badge variant={SEP_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>
            <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 2 }}>{s.nextStep}</span>
          </span>,
          <Btn size="sm" variant="outline" onClick={() => setOpenId(s.id)}>Open</Btn>,
        ])}
      />

      {initiating && <InitiateSeparationModal flash={flash} onClose={() => setInitiating(false)}
        onSaved={id => { setInitiating(false); rows.reload(); if (id) setOpenId(id) }} />}
    </div>
  )
}

function InitiateSeparationModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    employeeId: '', separationType: 'Resignation',
    noticeDate: new Date().toISOString().slice(0, 10),
    effectiveDate: '', noticePeriodDays: 30, noticeWaived: false, reason: '',
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.initiateSeparation({
        employeeId: f.employeeId, separationType: f.separationType,
        noticeDate: `${f.noticeDate}T00:00:00Z`, effectiveDate: `${f.effectiveDate}T00:00:00Z`,
        noticePeriodDays: Number(f.noticePeriodDays) || 30,
        noticeWaived: f.noticeWaived, reason: f.reason || null,
      })
      relay(flash, r)
      onSaved(r?.id)
    } catch (e) { relayError(flash, e, 'Could not open the separation.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Open a separation" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Select label="Type" value={f.separationType} onChange={v => setF({ ...f, separationType: v })}
        options={['Resignation', 'Termination', 'Retirement', 'EndOfContract', 'Death'].map(v => ({ value: v, label: v }))} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Notice given" type="date" value={f.noticeDate} onChange={v => setF({ ...f, noticeDate: v })} />
        <Input label="Last day" type="date" value={f.effectiveDate} onChange={v => setF({ ...f, effectiveDate: v })} required />
      </div>
      <Input label="Notice period (days)" type="number" value={f.noticePeriodDays}
        onChange={v => setF({ ...f, noticePeriodDays: v })} />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 8 }}>
        <input type="checkbox" checked={f.noticeWaived} onChange={e => setF({ ...f, noticeWaived: e.target.checked })} />
        Notice waived — pay in lieu
      </label>
      <Input label="Reason" value={f.reason} onChange={v => setF({ ...f, reason: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        The dues are computed from the leave balance, the salary in force and any outstanding advances finance
        can tell us about. If finance cannot be reached, that is said plainly rather than assumed to be zero.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.effectiveDate}>{busy ? 'Computing…' : 'Open and compute dues'}</Btn>
      </div>
    </Modal>
  )
}

function SeparationDetail({ id, flash, onBack }) {
  const [f, setF] = useState({})
  const [busy, setBusy] = useState(false)
  const detail = useData(() => hr.getSeparation(id), [id])

  if (detail.loading) return <Loading />
  const s = detail.data
  if (!s) return <Alert type="error">That separation no longer exists.</Alert>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  const line = (label, value, note) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 0', borderBottom: `1px solid ${T.lgrey}` }}>
      <span style={{ fontSize: 13 }}>{label}{note && <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{note}</span>}</span>
      <span style={{ fontSize: 13, fontWeight: 600 }}>{money(value, s.currencyCode)}</span>
    </div>
  )

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14, flexWrap: 'wrap' }}>
        <Btn size="sm" variant="ghost" onClick={onBack}>← All separations</Btn>
        <span style={{ fontFamily: 'monospace', fontWeight: 800, fontSize: 17, color: T.navy }}>{s.separationNumber}</span>
        <Badge variant={SEP_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>
      </div>

      <Alert type="info"><strong>{s.nextStep}</strong></Alert>
      {s.duesNotes && <Alert type="warning">{s.duesNotes}</Alert>}

      <Card style={{ padding: 16, marginBottom: 16 }}>
        <p style={{ fontWeight: 700, color: T.navy, margin: 0 }}>
          {s.employeeName} · {s.separationType} · last day {fmtDate(s.effectiveDate)} · {s.yearsOfService} years' service
        </p>
        <p style={{ fontSize: 12, color: T.mgrey, margin: '4px 0 12px' }}>
          Hired {fmtDate(s.hireDate)} · notice {fmtDate(s.noticeDate)}
          {s.noticeWaived && ' · notice waived (paid in lieu)'}
          {s.reason && ` · ${s.reason}`}
        </p>

        <p style={{ fontWeight: 700, color: T.navy, fontSize: 13, marginBottom: 4 }}>Final dues</p>
        {line('Unused leave', s.leavePayout, `${s.leaveDaysBalance} day(s) at ${money(s.dailyRate, s.currencyCode)}`)}
        {line('Final month worked', s.proRataSalary, `${s.finalMonthDaysWorked} working day(s)`)}
        {line('Notice pay', s.noticePay, s.noticeWaived ? `${s.noticePeriodDays} days in lieu` : 'notice served')}
        {line('Other earnings', s.otherEarnings)}
        {line('Less: advances recovered', -s.advanceRecovery)}
        {line('Less: other deductions', -s.otherDeductions)}
        <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 10, marginTop: 4, borderTop: `2px solid ${T.navy}` }}>
          <span style={{ fontWeight: 800, color: T.navy }}>NET DUES</span>
          <span style={{ fontWeight: 800, fontSize: 16, color: s.netDues < 0 ? T.red : T.green }}>
            {money(s.netDues, s.currencyCode)}
          </span>
        </div>
        <p style={{ fontSize: 11, color: T.mgrey, marginTop: 6 }}>
          Monthly salary {money(s.monthlySalary, s.currencyCode)} · daily rate {money(s.dailyRate, s.currencyCode)}
        </p>
      </Card>

      {s.status === 'Draft' && (
        <Card style={{ padding: 14 }}>
          <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 14 }}>Adjust the dues</p>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
            <Input label="Other earnings" type="number" value={f.otherEarnings ?? s.otherEarnings}
              onChange={v => setF({ ...f, otherEarnings: v })} />
            <Input label="Advances recovered" type="number" value={f.advanceRecovery ?? s.advanceRecovery}
              onChange={v => setF({ ...f, advanceRecovery: v })} note="Check finance if it could not be read" />
            <Input label="Other deductions" type="number" value={f.otherDeductions ?? s.otherDeductions}
              onChange={v => setF({ ...f, otherDeductions: v })} />
          </div>
          <Input label="Exit interview notes" value={f.exitInterviewNotes ?? s.exitInterviewNotes ?? ''}
            onChange={v => setF({ ...f, exitInterviewNotes: v })} />
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <Btn variant="outline" onClick={() => act(() => hr.adjustDues(id, {
              otherEarnings: Number(f.otherEarnings ?? s.otherEarnings),
              advanceRecovery: Number(f.advanceRecovery ?? s.advanceRecovery),
              otherDeductions: Number(f.otherDeductions ?? s.otherDeductions),
              exitInterviewNotes: f.exitInterviewNotes ?? null,
            }), 'Could not adjust.')} disabled={busy}>Recalculate</Btn>
            <Btn onClick={() => act(() => hr.submitSeparation(id), 'Could not submit.')} disabled={busy}>
              Send to the MD
            </Btn>
          </div>
        </Card>
      )}

      {s.status === 'PendingMd' && (
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="green" onClick={() => act(() => hr.decideSeparation(id, { decision: 'Approve' }), 'Could not approve.')}
            disabled={busy}>Approve {money(s.netDues, s.currencyCode)}</Btn>
          <Btn variant="ghost" onClick={() => {
            const reason = window.prompt('Cancel this separation? Give a reason:')
            if (reason === null) return
            act(() => hr.decideSeparation(id, { decision: 'Cancel', reason }), 'Could not cancel.')
          }} disabled={busy}>Cancel separation</Btn>
        </div>
      )}

      {s.status === 'Approved' && (
        <Card style={{ padding: 14 }}>
          <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 14 }}>Record the payment</p>
          <p style={{ fontSize: 12, color: T.mgrey, margin: '4px 0 8px' }}>
            This is the step that takes {s.employeeName} off the payroll.
          </p>
          <Input label="Payment reference" value={f.paymentReference ?? ''} onChange={v => setF({ ...f, paymentReference: v })} />
          <Btn onClick={() => act(() => hr.paySeparation(id, {
            paymentReference: f.paymentReference || null, issueCertificate: true,
          }), 'Could not record the payment.')} disabled={busy}>Record payment and issue certificate</Btn>
        </Card>
      )}

      {s.status === 'Paid' && (
        <Alert type="success">
          Paid {fmtDate(s.paidAt)}{s.paymentReference && ` (ref ${s.paymentReference})`}.
          {s.certificateIssuedAt && ' Certificate of Service issued.'} Remember to revoke their system access
          in user administration — HR cannot do that from here.
        </Alert>
      )}
    </div>
  )
}
