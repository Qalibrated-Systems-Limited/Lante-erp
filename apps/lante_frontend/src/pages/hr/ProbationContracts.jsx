import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H2 — probation milestones (P3) and fixed-term contract renewal (P33). REAL,
// wired to hr-service (/api/v1/hr/{probation,contracts,milestones}/*).
//
// The daily milestone sweep in hr-service is the only thing that RAISES work
// here: a review row at day 90 and day 180 of probation, and a renewal alert
// with 30-day / 7-day warnings for fixed-term staff. Everything on these
// screens is the human decision that closes one of those rows — the UI never
// invents a milestone. "Run sweep now" just skips the wait for the daily tick
// and is idempotent, so pressing it twice changes nothing.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

const OUTCOME_VARIANT = {
  Pending: 'amber', Confirmed: 'green', Extended: 'blue', Terminated: 'red',
  Renewed: 'green', ConvertedToPermanent: 'green', Expired: 'default',
}
const REVIEW_LABEL = { ThreeMonth: '3-month review', SixMonth: '6-month confirmation', Extended: 'extended review' }

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// Shared header: the sweep feeds both tabs, so either can trigger it.
function SweepButton({ flash, onDone }) {
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const r = await hr.runMilestoneSweep()
      flash(r?.message ?? 'Sweep complete.')
      onDone()
    } catch (e) { flash(e.response?.data?.message ?? 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Sweeping…' : 'Run sweep now'}</Btn>
}

// ── Probation reviews (P3) ───────────────────────────────────────────────────
export function ProbationTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [outcome, setOutcome] = useState('Pending')
  const [acting, setActing] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    hr.listProbationReviews({ outcome: outcome || undefined })
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.probationSummary().then(setSum).catch(() => {})
  }, [outcome])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="On Probation" value={sum?.onProbation ?? 0} color={T.amber} />
        <Kpi label="Reviews Pending" value={sum?.reviewsPending ?? 0} color={T.blue} />
        <Kpi label="Overdue" value={sum?.reviewsOverdue ?? 0} color={T.red} sub="milestone date passed" />
        <Kpi label="Confirmed (YTD)" value={sum?.confirmedThisYear ?? 0} color={T.green} />
        <Kpi label="Extended (YTD)" value={sum?.extendedThisYear ?? 0} color={T.blue} />
        <Kpi label="Termination Recorded" value={sum?.terminationRecommended ?? 0} color={T.red} />
      </div>

      <SectionHeader
        title="Probation Reviews"
        sub="Probation is 6 months with a review at 3. The scheduler raises each milestone; HR and the line manager complete the 3-month review, and the 6-month confirmation is the MD's decision."
        action={<SweepButton flash={flash} onDone={load} />}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={outcome} onChange={e => setOutcome(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All outcomes</option>
          {['Pending', 'Confirmed', 'Extended', 'Terminated'].map(o => <option key={o} value={o}>{o}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Milestone', 'Due', 'Age', 'Outcome', 'Reviewer', 'Completed', 'Actions']}
          empty={outcome === 'Pending' ? 'No probation reviews outstanding.' : 'No reviews match this filter.'}
          rows={rows.map(r => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{r.employeeName}</span>
            </span>,
            REVIEW_LABEL[r.reviewType] ?? r.reviewType,
            fmtDate(r.scheduledDate),
            r.isOverdue
              ? <span style={{ color: T.red, fontSize: 12, fontWeight: 600 }}>{r.daysSinceScheduled}d overdue</span>
              : <span style={{ color: T.mgrey, fontSize: 12 }}>{r.daysSinceScheduled >= 0 ? 'due' : `in ${-r.daysSinceScheduled}d`}</span>,
            <Badge variant={OUTCOME_VARIANT[r.outcome] ?? 'default'}>{r.outcome}</Badge>,
            r.reviewerName ?? (r.reviewerId ? '—' : ''),
            fmtDate(r.completedDate),
            r.outcome === 'Pending'
              ? <Btn size="sm" onClick={() => setActing(r)}>Record outcome</Btn>
              : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
          ])}
        />
      )}

      {acting && (
        <OutcomeModal review={acting} flash={flash}
          onClose={() => setActing(null)}
          onSaved={() => { setActing(null); load() }} />
      )}
    </div>
  )
}

function OutcomeModal({ review, onClose, onSaved, flash }) {
  const [outcome, setOutcome] = useState('Confirmed')
  const [notes, setNotes] = useState('')
  const [extendedToDate, setExtendedToDate] = useState('')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.recordProbationOutcome(review.id, {
        outcome,
        notes: notes || undefined,
        extendedToDate: outcome === 'Extended' && extendedToDate ? extendedToDate : undefined,
      })
      flash(r?.message ?? 'Outcome recorded.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not record the outcome.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${REVIEW_LABEL[review.reviewType] ?? review.reviewType} — ${review.employeeName}`} onClose={onClose} width={520}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0, marginBottom: 16 }}>
        Milestone date {fmtDate(review.scheduledDate)}. Confirming sets the employee to Active and closes any
        other outstanding milestone for them.
      </p>

      <Select label="Outcome" required value={outcome} onChange={setOutcome}
        options={[
          { value: 'Confirmed', label: 'Confirmed — employee passes probation' },
          { value: 'Extended', label: 'Extended — schedule a follow-up review' },
          { value: 'Terminated', label: 'Terminated — probation not passed' },
        ]} />

      {outcome === 'Extended' && (
        <Input label="Follow-up review due" type="date" required value={extendedToDate} onChange={setExtendedToDate}
          note="Must be a future date. A new review record is created for it — this one stays as history." />
      )}

      <Input label={outcome === 'Terminated' ? 'Reason (required)' : 'Notes'} value={notes} onChange={setNotes}
        required={outcome === 'Terminated'}
        placeholder={outcome === 'Terminated' ? 'Why probation was not passed…' : 'Optional review notes…'} />

      {outcome === 'Terminated' && (
        <p style={{ fontSize: 11, color: T.amber, marginTop: -6, marginBottom: 14 }}>
          This records the decision only. Separation and final dues are a later phase, so the employee's status
          is left unchanged.
        </p>
      )}

      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 6 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Record outcome'}</Btn>
      </div>
    </Modal>
  )
}

// ── Contract renewal (P33) ───────────────────────────────────────────────────
export function ContractsTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [outcome, setOutcome] = useState('Pending')
  const [acting, setActing] = useState(null)   // { alert, mode }

  const load = useCallback(() => {
    setLoading(true)
    hr.listContractAlerts(outcome)
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.probationSummary().then(setSum).catch(() => {})
  }, [outcome])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Fixed-Term Staff" value={sum?.fixedTermStaff ?? 0} />
        <Kpi label="Expiring ≤30 Days" value={sum?.contractsExpiringIn30Days ?? 0} color={T.amber} />
        <Kpi label="Expiring ≤7 Days" value={sum?.contractsExpiringIn7Days ?? 0} color={T.red} sub="needs the MD" />
        <Kpi label="Awaiting Decision" value={sum?.awaitingRenewalDecision ?? 0} color={T.blue} />
        <Kpi label="Lapsed, Unactioned" value={sum?.contractsExpiredUnactioned ?? 0} color={T.red} />
      </div>

      <SectionHeader
        title="Contract Renewals"
        sub="An alert opens 30 days before a fixed-term contract ends (HR + line manager) and escalates at 7 days (HR + MD). Renewing starts the new term where the old one ended."
        action={<SweepButton flash={flash} onDone={load} />}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={outcome} onChange={e => setOutcome(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All outcomes</option>
          {['Pending', 'Renewed', 'ConvertedToPermanent', 'Expired'].map(o => <option key={o} value={o}>{o}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Contract Ends', 'Time Left', 'Warnings Sent', 'Outcome', 'Actioned', 'Actions']}
          empty={outcome === 'Pending' ? 'No contracts awaiting a renewal decision.' : 'No alerts match this filter.'}
          rows={rows.map(a => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{a.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{a.employeeName}</span>
            </span>,
            fmtDate(a.contractEndDate),
            a.isExpired
              ? <span style={{ color: T.red, fontSize: 12, fontWeight: 600 }}>lapsed {-a.daysToExpiry}d ago</span>
              : <span style={{ color: a.daysToExpiry <= 7 ? T.red : T.amber, fontSize: 12, fontWeight: 600 }}>{a.daysToExpiry}d</span>,
            <span style={{ fontSize: 11, color: T.mgrey }}>
              {[a.alert30SentAt && '30d', a.alert7SentAt && '7d', a.expiredAlertSentAt && 'lapsed'].filter(Boolean).join(' · ') || '—'}
            </span>,
            <Badge variant={OUTCOME_VARIANT[a.outcome] ?? 'default'}>{a.outcome}</Badge>,
            fmtDate(a.actionedAt),
            a.outcome === 'Pending'
              ? <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <Btn size="sm" onClick={() => setActing({ alert: a, mode: 'renew' })}>Renew</Btn>
                  <Btn size="sm" variant="outline" onClick={() => setActing({ alert: a, mode: 'convert' })}>Make permanent</Btn>
                  <Btn size="sm" variant="ghost" onClick={() => setActing({ alert: a, mode: 'expire' })}>Let expire</Btn>
                </div>
              : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
          ])}
        />
      )}

      {acting && (
        <ContractDecisionModal {...acting} flash={flash}
          onClose={() => setActing(null)}
          onSaved={() => { setActing(null); load() }} />
      )}
    </div>
  )
}

function ContractDecisionModal({ alert, mode, onClose, onSaved, flash }) {
  const [newContractEndDate, setNewContractEndDate] = useState('')
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)

  const titles = {
    renew: `Renew contract — ${alert.employeeName}`,
    convert: `Convert to permanent — ${alert.employeeName}`,
    expire: `Let contract expire — ${alert.employeeName}`,
  }

  const save = async () => {
    setBusy(true)
    try {
      const r = mode === 'renew'
        ? await hr.renewContract(alert.id, { newContractEndDate, notes: notes || undefined })
        : mode === 'convert'
          ? await hr.convertToPermanent(alert.id, { notes: notes || undefined })
          : await hr.letContractExpire(alert.id, { notes: notes || undefined })
      flash(r?.message ?? 'Decision recorded.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not record the decision.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={titles[mode]} onClose={onClose} width={520}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0, marginBottom: 16 }}>
        Current term ends {fmtDate(alert.contractEndDate)}.
        {mode === 'renew' && ' The new term starts the day the current one ends.'}
        {mode === 'convert' && ' The employee becomes Permanent with no expiry, and every open renewal alert for them is closed.'}
        {mode === 'expire' && ' The contract lapses on its end date. Final dues are handled by the separation process, which is a later phase.'}
      </p>

      {mode === 'renew' && (
        <Input label="New contract end date" type="date" required value={newContractEndDate} onChange={setNewContractEndDate}
          note={`Must be later than ${fmtDate(alert.contractEndDate)}.`} />
      )}

      <Input label="Notes" value={notes} onChange={setNotes}
        placeholder={mode === 'expire' ? 'Reason the contract is not being renewed…' : 'Optional notes…'} />

      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 6 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant={mode === 'expire' ? 'danger' : 'primary'} onClick={save} disabled={busy}>
          {busy ? 'Saving…' : mode === 'renew' ? 'Renew contract' : mode === 'convert' ? 'Make permanent' : 'Record expiry'}
        </Btn>
      </div>
    </Modal>
  )
}
