import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H7 — learning & development (P22, P23, P25, P26). REAL, wired to hr-service.
//
// The screen that matters most here is Compliance, and the thing it must never
// do is confuse "we could not check" with "they have not done it". HSE and
// anti-bribery evidence lives in hse-service and compliance-service; HR reads
// it. When a source is unreachable the requirement comes back Unknown, which is
// shown in grey as "could not check" and does NOT block a salary increment.
// Rendering that as a red failure would let one service outage look like a
// company-wide compliance breach — and would block everyone's pay rise.
//
// P24 (certificate vault) is not here: it was collapsed into the employee
// record in H1, and its 30-day expiry alerts belong to the H2 sweep.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n) => (n == null ? '—' : `KES ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)

const LDP_VARIANT = { Draft: 'default', Submitted: 'amber', Approved: 'green', Rejected: 'red' }
const OBJ_VARIANT = { Planned: 'default', InProgress: 'blue', Completed: 'green', Abandoned: 'default' }
// Unknown is deliberately NOT red — it is an absence of information, not a failure.
const STATE_VARIANT = {
  Valid: 'green', DueSoon: 'amber', Overdue: 'amber',
  Breached: 'red', NeverCompleted: 'red', Unknown: 'default',
}
const STATE_LABEL = {
  Valid: 'In date', DueSoon: 'Due soon', Overdue: 'Overdue (in grace)',
  Breached: 'Breached', NeverCompleted: 'Never done', Unknown: 'Could not check',
}
const SOURCES = ['External', 'Internal', 'KnowledgeSharing', 'OnTheJob']

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
  const [state, setState] = useState({ loading: true, data: null })
  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    loader().then(data => setState({ loading: false, data })).catch(() => setState({ loading: false, data: null }))
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
      const r = await hr.runLearningSweep()
      const raised = r.ldpRemindersSent + r.ldpEscalations + r.zeroHoursFlags
        + r.mandatoryBreachAlerts + r.knowledgeSharingShortfalls + r.budgetWarnings + r.budgetsExhausted
      flash(raised === 0
        ? 'Sweep complete — nothing new to raise.'
        : `Sweep raised ${r.ldpRemindersSent} LDP reminder(s), ${r.ldpEscalations} escalation(s), ${r.zeroHoursFlags} zero-hours flag(s), ${r.mandatoryBreachAlerts} compliance breach(es), ${r.knowledgeSharingShortfalls} knowledge-sharing shortfall(s).`)
      onDone?.()
    } catch (e) { relayError(flash, e, 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Sweeping…' : 'Run L&D sweep'}</Btn>
}

// ═════════════════════════════════════════════════════════════════════════════
// Learning plans (P22)
// ═════════════════════════════════════════════════════════════════════════════
export function LearningPlansTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [status, setStatus] = useState('')
  const [editing, setEditing] = useState(null)

  const summary = useData(() => hr.learningSummary(year), [year])
  const plans = useData(() => hr.listLdps({ year, status: status || undefined }), [year, status])

  const s = summary.data
  const reload = () => { summary.reload(); plans.reload() }

  const decide = async (p, decision) => {
    const reason = decision === 'Reject' ? window.prompt(`Send ${p.employeeName}'s plan back? Give a reason:`) : null
    if (decision === 'Reject' && reason === null) return
    try { relay(flash, await hr.decideLdp(p.id, { decision, reason })); reload() }
    catch (e) { relayError(flash, e, 'Could not record the decision.') }
  }

  if (plans.loading) return <Loading />

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Plans expected" value={s?.plansExpected ?? 0} />
        <Kpi label="Approved" value={s?.plansApproved ?? 0} color={T.green} />
        <Kpi label="Awaiting manager" value={s?.plansAwaitingApproval ?? 0} color={T.amber} />
        <Kpi label="Missing" value={s?.plansMissing ?? 0} color={s?.plansMissing ? T.red : T.green} />
        <Kpi label="Training hours" value={s?.totalTrainingHours ?? 0} sub={`${s?.employeesOnTarget ?? 0} on target`} />
        <Kpi label="Blocked from increment" value={s?.blockedFromIncrement ?? 0} color={s?.blockedFromIncrement ? T.red : T.green} />
      </div>

      <SectionHeader
        title="Learning & Development Plans"
        sub="Due by 15 January. The daily sweep chases anyone without an approved plan and escalates to the MD from 15 February (HR-036) — each person once, not every morning."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            <SweepButton flash={flash} onDone={reload} />
            <Btn size="sm" onClick={() => setEditing({})}>+ Plan</Btn>
          </div>
        }
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {['Draft', 'Submitted', 'Approved', 'Rejected'].map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Employee', 'Year', 'Objectives', 'Status', 'Submitted', 'Chased', 'Actions']}
        empty={`No plans for ${year}.`}
        rows={(plans.data ?? []).map(p => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{p.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{p.employeeName}</span>
          </span>,
          p.planYear,
          <span>{p.objectivesCompleted}/{p.objectives?.length ?? 0} complete</span>,
          <span>
            <Badge variant={LDP_VARIANT[p.status] ?? 'default'}>{p.status}</Badge>
            {p.rejectionReason && <span style={{ display: 'block', fontSize: 11, color: T.red, marginTop: 3 }}>{p.rejectionReason}</span>}
          </span>,
          fmtDate(p.submittedAt),
          p.escalatedAt
            ? <Badge variant="red">escalated</Badge>
            : p.reminderSentAt ? <Badge variant="amber">reminded</Badge> : <span style={{ color: T.mgrey }}>—</span>,
          <div style={{ display: 'flex', gap: 6 }}>
            <Btn size="sm" variant="outline" onClick={() => setEditing(p)}>Open</Btn>
            {p.status === 'Draft' && <Btn size="sm" onClick={async () => {
              try { relay(flash, await hr.submitLdp(p.id)); reload() }
              catch (e) { relayError(flash, e, 'Could not submit.') }
            }}>Submit</Btn>}
            {p.status === 'Submitted' && <>
              <Btn size="sm" variant="green" onClick={() => decide(p, 'Approve')}>Approve</Btn>
              <Btn size="sm" variant="ghost" onClick={() => decide(p, 'Reject')}>Send back</Btn>
            </>}
          </div>,
        ])}
      />

      {editing && <LdpModal plan={editing.id ? editing : null} year={year} flash={flash}
        onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function LdpModal({ plan, year, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    employeeId: plan?.employeeId ?? '',
    planYear: plan?.planYear ?? year,
    notes: plan?.notes ?? '',
    objectives: plan?.objectives?.length
      ? plan.objectives.map(o => ({ id: o.id, objective: o.objective, activity: o.activity ?? '', targetDate: o.targetDate?.slice(0, 10) ?? '', status: o.status }))
      : [{ objective: '', activity: '', targetDate: '' }],
  })
  const [busy, setBusy] = useState(false)
  const locked = plan?.status === 'Approved'

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const setObj = (i, key, v) => setF({ ...f, objectives: f.objectives.map((o, j) => j === i ? { ...o, [key]: v } : o) })
  const addObj = () => setF({ ...f, objectives: [...f.objectives, { objective: '', activity: '', targetDate: '' }] })
  const dropObj = (i) => setF({ ...f, objectives: f.objectives.filter((_, j) => j !== i) })

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.saveLdp({
        employeeId: f.employeeId,
        planYear: Number(f.planYear),
        notes: f.notes || null,
        objectives: f.objectives.filter(o => o.objective.trim()).map(o => ({
          id: o.id, objective: o.objective, activity: o.activity || null,
          targetDate: o.targetDate ? `${o.targetDate}T00:00:00Z` : null,
        })),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the plan.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={plan ? `${plan.employeeName} — ${plan.planYear} plan` : 'New learning plan'} onClose={onClose} width={680}>
      {locked && <Alert type="info">This plan is approved, so it can no longer be edited. Objectives close on their own when a matching training event is logged.</Alert>}

      {!plan && (
        <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
          options={[{ value: '', label: 'Select…' },
            ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      )}
      <Input label="Plan year" type="number" value={f.planYear} onChange={v => setF({ ...f, planYear: v })} readOnly={!!plan} />

      <p style={{ fontWeight: 700, color: T.navy, fontSize: 13, marginTop: 14, marginBottom: 6 }}>Objectives</p>
      {f.objectives.map((o, i) => (
        <Card key={i} style={{ padding: 10, marginBottom: 8 }}>
          {o.status === 'Completed' && <Badge variant="green">completed by a logged training event</Badge>}
          <Input label={`Objective ${i + 1}`} value={o.objective} onChange={v => setObj(i, 'objective', v)}
            readOnly={locked || o.status === 'Completed'} />
          <Input label="Activity" value={o.activity} onChange={v => setObj(i, 'activity', v)}
            readOnly={locked || o.status === 'Completed'}
            note="Naming the course helps — a training event with matching words closes this objective automatically." />
          <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end' }}>
            <div style={{ flex: 1 }}>
              <Input label="Target date" type="date" value={o.targetDate} onChange={v => setObj(i, 'targetDate', v)}
                readOnly={locked || o.status === 'Completed'} />
            </div>
            {!locked && f.objectives.length > 1 && o.status !== 'Completed' &&
              <Btn size="sm" variant="ghost" onClick={() => dropObj(i)} style={{ marginBottom: 12 }}>Remove</Btn>}
          </div>
        </Card>
      ))}
      {!locked && <Btn size="sm" variant="outline" onClick={addObj}>+ Objective</Btn>}

      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} readOnly={locked} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Close</Btn>
        {!locked && <Btn onClick={save} disabled={busy || !f.employeeId || !f.objectives.some(o => o.objective.trim())}>
          {busy ? 'Saving…' : 'Save plan'}
        </Btn>}
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Training log and hours (P23)
// ═════════════════════════════════════════════════════════════════════════════
export function TrainingTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [logging, setLogging] = useState(false)

  const events = useData(() => hr.listTraining({ year }), [year])
  const hours = useData(() => hr.listTrainingHours({ year }), [year])

  if (events.loading) return <Loading />

  return (
    <div>
      <SectionHeader
        title="Training Hours"
        sub="The annual target is 40 hours, or 60 where a position sets its own. Knowledge-sharing sessions count through the same ledger as external courses."
        action={<Btn size="sm" onClick={() => setLogging(true)}>+ Log training</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {hours.loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Position', 'Hours YTD', 'Target', 'Progress', 'Events', 'Last training', '']}
          empty="No employees."
          rows={(hours.data ?? []).map(h => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{h.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{h.employeeName}</span>
            </span>,
            h.positionTitle ?? '—',
            <strong>{h.hoursYtd}</strong>,
            h.targetHours,
            <span style={{ color: h.percentOfTarget >= 100 ? T.green : h.percentOfTarget >= 50 ? T.amber : T.red, fontWeight: 600 }}>
              {h.percentOfTarget}%
            </span>,
            h.eventsAttended,
            fmtDate(h.lastTrainingDate),
            h.zeroHoursRedFlag ? <Badge variant="red">no hours by 30 Jun</Badge> : '',
          ])}
        />
      )}

      <div style={{ marginTop: 26 }}>
        <SectionHeader title="Training Events" sub="Each event carries its cost against the department's L&D budget and closes any matching plan objective." />
      </div>
      <DataTable
        headers={['Date', 'Title', 'Provider', 'Source', 'Hours', 'Cost', 'Attendees', 'Mandatory']}
        empty={`No training logged in ${year}.`}
        rows={(events.data ?? []).map(t => [
          fmtDate(t.trainingDate),
          t.title,
          t.provider ?? '—',
          <Badge variant={t.source === 'KnowledgeSharing' ? 'blue' : 'default'}>{t.source}</Badge>,
          t.durationHours,
          t.cost > 0 ? money(t.cost) : '—',
          t.attendeeCount,
          t.mandatoryTrainingCode ? <Badge variant="green">{t.mandatoryTrainingCode}</Badge> : '',
        ])}
      />

      {logging && <LogTrainingModal flash={flash}
        onClose={() => setLogging(false)} onSaved={() => { setLogging(false); events.reload(); hours.reload() }} />}
    </div>
  )
}

function LogTrainingModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [requirements, setRequirements] = useState([])
  const [f, setF] = useState({
    title: '', provider: '', source: 'External',
    trainingDate: new Date().toISOString().slice(0, 10),
    durationHours: '', cost: '', mandatoryTrainingCode: '', attendees: [],
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
    hr.listRequirements().then(r => setRequirements(r ?? [])).catch(() => {})
  }, [])

  // Only requirements whose evidence HR actually owns can be satisfied here (HR-DEC-5). The rest are
  // recorded in the service that owns them, and the server refuses anything else anyway.
  const hrOwned = requirements.filter(r => r.evidenceSource === 'Hr')

  const toggle = (id) => setF({
    ...f, attendees: f.attendees.includes(id) ? f.attendees.filter(a => a !== id) : [...f.attendees, id],
  })

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.logTraining({
        title: f.title, provider: f.provider || null, source: f.source,
        trainingDate: `${f.trainingDate}T00:00:00Z`,
        durationHours: Number(f.durationHours), cost: Number(f.cost || 0),
        mandatoryTrainingCode: f.mandatoryTrainingCode || null,
        attendees: f.attendees.map(id => ({ employeeId: id })),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not log the training.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Log a training event" onClose={onClose} width={620}>
      <Input label="Title" value={f.title} onChange={v => setF({ ...f, title: v })} required
        note="Words here are matched against open plan objectives to close them automatically." />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Provider" value={f.provider} onChange={v => setF({ ...f, provider: v })} />
        <Select label="Source" value={f.source} onChange={v => setF({ ...f, source: v })}
          options={SOURCES.map(v => ({ value: v, label: v }))} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="Date" type="date" value={f.trainingDate} onChange={v => setF({ ...f, trainingDate: v })} required />
        <Input label="Hours" type="number" value={f.durationHours} onChange={v => setF({ ...f, durationHours: v })} required />
        <Input label="Cost" type="number" value={f.cost} onChange={v => setF({ ...f, cost: v })} />
      </div>
      <Select label="Satisfies a mandatory requirement" value={f.mandatoryTrainingCode}
        onChange={v => setF({ ...f, mandatoryTrainingCode: v })}
        options={[{ value: '', label: 'None' }, ...hrOwned.map(r => ({ value: r.code, label: r.name }))]} />
      {requirements.some(r => r.evidenceSource !== 'Hr') && (
        <p style={{ fontSize: 12, color: T.mgrey, marginTop: -4 }}>
          {requirements.filter(r => r.evidenceSource !== 'Hr').map(r => r.name).join(' and ')} are recorded in
          the service that owns them — HR reads those, it does not log them.
        </p>
      )}

      <p style={{ fontWeight: 700, color: T.navy, fontSize: 13, marginTop: 12, marginBottom: 6 }}>
        Attendees {f.attendees.length > 0 && <span style={{ fontWeight: 500, color: T.mgrey }}>({f.attendees.length} selected)</span>}
      </p>
      <div style={{ maxHeight: 190, overflowY: 'auto', border: `1px solid ${T.lgrey}`, borderRadius: 7, padding: 8 }}>
        {employees.map(e => (
          <label key={e.id} style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, padding: '3px 0' }}>
            <input type="checkbox" checked={f.attendees.includes(e.id)} onChange={() => toggle(e.id)} />
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{e.employeeNumber}</span> {e.fullName}
          </label>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.title.trim() || !f.durationHours || f.attendees.length === 0}>
          {busy ? 'Logging…' : 'Log training'}
        </Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Mandatory compliance (P23, HR-029/034)
// ═════════════════════════════════════════════════════════════════════════════
export function ComplianceTab({ flash }) {
  const [editing, setEditing] = useState(null)
  const requirements = useData(() => hr.listRequirements(true), [])
  const compliance = useData(() => hr.listCompliance(), [])

  if (requirements.loading) return <Loading />
  const reload = () => { requirements.reload(); compliance.reload() }

  const unknown = (compliance.data ?? []).reduce((n, c) => n + c.unknown, 0)

  return (
    <div>
      {unknown > 0 && (
        <Alert type="info">
          <strong>{unknown} check(s) could not be completed.</strong> HSE and anti-bribery records live in
          hse-service and compliance-service; when one cannot be reached the requirement shows as
          <em> could not check</em>, not as a failure, and does not block anybody's salary increment. Treating
          an outage as non-compliance would freeze every pay review in the company.
        </Alert>
      )}

      <SectionHeader
        title="Mandatory Training Matrix"
        sub="HR owns the rule — what is required, for how long, and whether lapsing blocks an increment. The completion record stays with the service that owns it."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            {(requirements.data ?? []).length === 0 && <Btn size="sm" variant="outline" onClick={async () => {
              try { relay(flash, await hr.seedRequirements()); reload() }
              catch (e) { relayError(flash, e, 'Could not install the matrix.') }
            }}>Install defaults</Btn>}
            <Btn size="sm" onClick={() => setEditing({})}>+ Requirement</Btn>
          </div>
        }
      />
      <DataTable
        headers={['Code', 'Requirement', 'Evidence from', 'Valid for', 'Grace', 'Blocks increment', 'Status', '']}
        empty="No mandatory requirements configured."
        rows={(requirements.data ?? []).map(r => [
          <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.code}</span>,
          r.name,
          <Badge variant={r.evidenceSource === 'Hr' ? 'navy' : 'blue'}>
            {r.evidenceSource === 'Hr' ? 'HR' : `${r.evidenceSource}-service`}
          </Badge>,
          r.validityMonths ? `${r.validityMonths} months` : 'never expires',
          `${r.graceDays} days`,
          r.blocksIncrement ? 'Yes' : 'No',
          <Badge variant={r.isActive ? 'green' : 'default'}>{r.isActive ? 'Active' : 'Inactive'}</Badge>,
          <Btn size="sm" variant="outline" onClick={() => setEditing(r)}>Edit</Btn>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader title="Where Everyone Stands" sub="Read live. A lapse beyond the grace period blocks a salary increment (HR-029/HR-034)." />
      </div>
      {compliance.loading ? <Loading /> : (
        <DataTable
          headers={['Employee', ...(requirements.data ?? []).filter(r => r.isActive).map(r => r.code), 'Increment']}
          empty="No employees."
          rows={(compliance.data ?? []).map(c => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{c.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{c.employeeName}</span>
              {!c.hasLoginAccount && <span style={{ display: 'block', fontSize: 10, color: T.mgrey }}>no login account — nothing to look up</span>}
            </span>,
            ...(requirements.data ?? []).filter(r => r.isActive).map(r => {
              const s = c.requirements.find(x => x.requirementCode === r.code)
              if (!s) return <span style={{ color: T.mgrey }}>n/a</span>
              return (
                <span title={s.detail}>
                  <Badge variant={STATE_VARIANT[s.state] ?? 'default'}>{STATE_LABEL[s.state] ?? s.state}</Badge>
                  {s.daysOverdue > 0 && <span style={{ display: 'block', fontSize: 10, color: T.red }}>{s.daysOverdue}d over</span>}
                </span>
              )
            }),
            c.anyBlocksIncrement
              ? <Badge variant="red">blocked</Badge>
              : <Badge variant="green">clear</Badge>,
          ])}
        />
      )}

      {editing && <RequirementModal req={editing.id ? editing : null} flash={flash}
        onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function RequirementModal({ req, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    code: req?.code ?? '', name: req?.name ?? '', description: req?.description ?? '',
    evidenceSource: req?.evidenceSource ?? 'Hr',
    validityMonths: req?.validityMonths ?? 12,
    blocksIncrement: req?.blocksIncrement ?? true,
    graceDays: req?.graceDays ?? 30,
    isActive: req?.isActive ?? true,
    displayOrder: req?.displayOrder ?? 0,
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      code: f.code, name: f.name, description: f.description || null,
      evidenceSource: f.evidenceSource,
      validityMonths: f.validityMonths === '' ? null : Number(f.validityMonths),
      blocksIncrement: f.blocksIncrement, graceDays: Number(f.graceDays) || 0,
      isActive: f.isActive, displayOrder: Number(f.displayOrder) || 0,
    }
    try {
      relay(flash, req ? await hr.updateRequirement(req.id, dto) : await hr.createRequirement(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the requirement.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={req ? `Edit ${req.code}` : 'New mandatory requirement'} onClose={onClose}>
      <Input label="Code" value={f.code} onChange={v => setF({ ...f, code: v })} required readOnly={!!req} />
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <Input label="Description" value={f.description} onChange={v => setF({ ...f, description: v })} />
      <Select label="Evidence source" value={f.evidenceSource} onChange={v => setF({ ...f, evidenceSource: v })}
        options={[
          { value: 'Hr', label: 'HR — recorded in this module' },
          { value: 'Hse', label: 'hse-service — read from there' },
          { value: 'Compliance', label: 'compliance-service — read from there' },
        ]} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        HR never copies another module's record. Pointing a requirement at hse or compliance means HR reads it —
        and reports "could not check" rather than a failure if that service is unreachable.
      </p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Valid for (months)" type="number" value={f.validityMonths}
          onChange={v => setF({ ...f, validityMonths: v })} note="Blank = never expires" />
        <Input label="Grace period (days)" type="number" value={f.graceDays} onChange={v => setF({ ...f, graceDays: v })}
          note="Past this, the lapse is a red flag" />
      </div>
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 8 }}>
        <input type="checkbox" checked={f.blocksIncrement} onChange={e => setF({ ...f, blocksIncrement: e.target.checked })} />
        A lapse blocks a salary increment (HR-029)
      </label>
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 6 }}>
        <input type="checkbox" checked={f.isActive} onChange={e => setF({ ...f, isActive: e.target.checked })} /> Active
      </label>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code.trim() || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Knowledge sharing (P25) and L&D budgets (P26)
// ═════════════════════════════════════════════════════════════════════════════
export function KnowledgeSharingTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [logging, setLogging] = useState(false)
  const [budgetOpen, setBudgetOpen] = useState(false)

  const sessions = useData(() => hr.listKnowledgeSessions({ year }), [year])
  const budgetRows = useData(() => hr.listLdBudgets(year), [year])
  const summary = useData(() => hr.learningSummary(year), [year])

  if (sessions.loading) return <Loading />
  const s = summary.data

  return (
    <div>
      {s && !s.monthlySessionTargetMet && (
        <Alert type="warning">
          {s.knowledgeSessionsThisMonth} knowledge-sharing session(s) this month against a target of 2 (HR-030).
        </Alert>
      )}

      <SectionHeader
        title="Knowledge Sharing"
        sub="At least two sessions a month, company-wide. Attendee hours go through the same ledger as external courses, so internal sharing counts towards everyone's annual target."
        action={<Btn size="sm" onClick={() => setLogging(true)}>+ Log session</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Date', 'Topic', 'Facilitator', 'Hours', 'Attendees', 'Counted']}
        empty={`No sessions logged in ${year}.`}
        rows={(sessions.data ?? []).map(k => [
          fmtDate(k.sessionDate),
          k.topic,
          k.facilitatorName ?? '—',
          k.durationHours,
          k.attendeeCount,
          k.trainingEventId ? <Badge variant="green">hours credited</Badge> : <Badge variant="amber">not credited</Badge>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="L&D Budgets"
          sub="Set per department per year. Each training cost is recomputed against it, so a corrected cost corrects the spend. Warnings fire once at 80% and once when it is fully spent."
          action={<Btn size="sm" onClick={() => setBudgetOpen(true)}>+ Budget</Btn>}
        />
      </div>
      {budgetRows.loading ? <Loading /> : (
        <DataTable
          headers={['Department', 'Year', 'Budget', 'Spent', 'Remaining', 'Used', 'Alerts']}
          empty={`No L&D budgets set for ${year}.`}
          rows={(budgetRows.data ?? []).map(b => [
            b.departmentName ?? <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{b.departmentId}</span>,
            b.year,
            money(b.budgetedAmount),
            money(b.actualSpend),
            <span style={{ color: b.remaining < 0 ? T.red : T.dgrey, fontWeight: 600 }}>{money(b.remaining)}</span>,
            <span style={{ color: b.percentUsed >= 100 ? T.red : b.percentUsed >= 80 ? T.amber : T.green, fontWeight: 600 }}>
              {b.percentUsed}%
            </span>,
            <span style={{ display: 'flex', gap: 4 }}>
              {b.warning80SentAt && <Badge variant="amber">80%</Badge>}
              {b.exhausted100SentAt && <Badge variant="red">exhausted</Badge>}
            </span>,
          ])}
        />
      )}

      {logging && <SessionModal flash={flash}
        onClose={() => setLogging(false)} onSaved={() => { setLogging(false); sessions.reload(); summary.reload() }} />}
      {budgetOpen && <BudgetModal year={year} flash={flash}
        onClose={() => setBudgetOpen(false)} onSaved={() => { setBudgetOpen(false); budgetRows.reload() }} />}
    </div>
  )
}

function SessionModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    topic: '', description: '', facilitatorEmployeeId: '',
    sessionDate: new Date().toISOString().slice(0, 10), durationHours: '1', attendees: [],
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const toggle = (id) => setF({
    ...f, attendees: f.attendees.includes(id) ? f.attendees.filter(a => a !== id) : [...f.attendees, id],
  })

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.logKnowledgeSession({
        topic: f.topic, description: f.description || null,
        facilitatorEmployeeId: f.facilitatorEmployeeId,
        sessionDate: `${f.sessionDate}T00:00:00Z`,
        durationHours: Number(f.durationHours),
        attendeeEmployeeIds: f.attendees,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not log the session.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Log a knowledge-sharing session" onClose={onClose} width={600}>
      <Input label="Topic" value={f.topic} onChange={v => setF({ ...f, topic: v })} required />
      <Input label="Description" value={f.description} onChange={v => setF({ ...f, description: v })} />
      <Select label="Facilitator" value={f.facilitatorEmployeeId} onChange={v => setF({ ...f, facilitatorEmployeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Date" type="date" value={f.sessionDate} onChange={v => setF({ ...f, sessionDate: v })} required />
        <Input label="Duration (hours)" type="number" value={f.durationHours} onChange={v => setF({ ...f, durationHours: v })} required />
      </div>

      <p style={{ fontWeight: 700, color: T.navy, fontSize: 13, marginTop: 12, marginBottom: 6 }}>
        Attendees {f.attendees.length > 0 && <span style={{ fontWeight: 500, color: T.mgrey }}>({f.attendees.length})</span>}
      </p>
      <div style={{ maxHeight: 190, overflowY: 'auto', border: `1px solid ${T.lgrey}`, borderRadius: 7, padding: 8 }}>
        {employees.map(e => (
          <label key={e.id} style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, padding: '3px 0' }}>
            <input type="checkbox" checked={f.attendees.includes(e.id)} onChange={() => toggle(e.id)} />
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{e.employeeNumber}</span> {e.fullName}
          </label>
        ))}
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>Every attendee is credited with the session's hours.</p>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.topic.trim() || !f.facilitatorEmployeeId || f.attendees.length === 0}>
          {busy ? 'Logging…' : 'Log session'}
        </Btn>
      </div>
    </Modal>
  )
}

function BudgetModal({ year, flash, onClose, onSaved }) {
  const [departments, setDepartments] = useState([])
  const [f, setF] = useState({ departmentId: '', year, budgetedAmount: '', notes: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listDepartments().then(d => setDepartments(d ?? [])).catch(() => setDepartments([])) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.saveLdBudget({
        departmentId: f.departmentId, year: Number(f.year),
        budgetedAmount: Number(f.budgetedAmount), notes: f.notes || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the budget.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Set an L&D budget" onClose={onClose}>
      {departments.length === 0 && (
        <Alert type="warning">
          No departments could be read from user-service, so there is nothing to budget against yet.
        </Alert>
      )}
      <Select label="Department" value={f.departmentId} onChange={v => setF({ ...f, departmentId: v })}
        options={[{ value: '', label: 'Select…' }, ...departments.map(d => ({ value: d.id, label: d.name }))]} required />
      <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} required />
      <Input label="Annual budget" type="number" value={f.budgetedAmount} onChange={v => setF({ ...f, budgetedAmount: v })} required />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Raising a budget re-opens its thresholds, so the 80% warning fires again against the new figure rather
        than staying silent because the old one had already tripped.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.departmentId || !f.budgetedAmount}>{busy ? 'Saving…' : 'Save budget'}</Btn>
      </div>
    </Modal>
  )
}
