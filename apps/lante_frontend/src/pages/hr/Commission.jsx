import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H11 — sales commission (P29–P31). REAL, wired to hr-service.
//
// H11-DEC-1: CRM owns the revenue target AND the attainment. HR owns only the
// overlay — the bands, the MD's approval that someone is on commission, and the
// quarterly split. Nothing on this screen writes a target; where one is shown
// it was read live from CRM and is labelled as such.
//
// The behaviour that matters most: when CRM cannot be read, the API refuses to
// compute rather than returning zeros, and these screens say so plainly. A
// commission run against a silently-zero target pays nobody anything and looks
// like a real result — which is the worst kind of wrong answer.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)
const pct = (n) => (n == null ? '—' : `${Number(n).toFixed(1)}%`)

const PLAN_VARIANT = { Draft: 'default', PendingMd: 'amber', Approved: 'green', Rejected: 'red' }
const STATEMENT_VARIANT = { Computed: 'amber', Approved: 'blue', Paid: 'green', Disputed: 'red', Cancelled: 'default' }
const DISPUTE_VARIANT = { Open: 'amber', UnderReview: 'blue', Upheld: 'green', Rejected: 'default' }

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 20, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

function NoAccess() {
  return (
    <Card style={{ padding: 28, textAlign: 'center' }}>
      <p style={{ fontSize: 32, margin: 0 }}>🔒</p>
      <p style={{ fontWeight: 700, color: T.navy, marginTop: 10 }}>Commission is restricted</p>
      <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6 }}>Commission is pay data and needs the payroll permissions.</p>
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

// COM-004 — the quarterly issue. The daily worker makes this same pass, so on almost every day of the year it
// finds the quarter already issued and reports that rather than recomputing anything.
function SweepButton({ flash, onDone }) {
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const r = await hr.runCommissionSweep()
      flash(r.statementsIssued > 0
        ? `${r.year} Q${r.quarter}: ${r.statementsIssued} statement(s) issued, ${r.redFlagsRaised} red flag(s).`
        : (r.notes?.[0] ?? `Nothing to issue for ${r.year} Q${r.quarter}.`),
        r.statementsIssued > 0 ? 'success' : 'warning')
      onDone?.()
    } catch (e) { relayError(flash, e, 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Sweeping…' : 'Run quarterly issue'}</Btn>
}

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

// ═════════════════════════════════════════════════════════════════════════════
// Plans and bands (P29)
// ═════════════════════════════════════════════════════════════════════════════
export function CommissionPlansTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [editing, setEditing] = useState(null)
  const [bandEditing, setBandEditing] = useState(null)

  const summary = useData(() => hr.commissionSummary(year), [year])
  const plans = useData(() => hr.listCommissionPlans({ year }), [year])
  const bands = useData(() => hr.listCommissionBands(true), [])

  if (plans.loading) return <Loading />
  if (plans.denied) return <NoAccess />
  const s = summary.data
  const reload = () => { summary.reload(); plans.reload(); bands.reload() }

  const act = async (fn, fallback) => {
    try { relay(flash, await fn()); reload() }
    catch (e) { relayError(flash, e, fallback) }
  }

  return (
    <div>
      <Alert type="info">
        <strong>CRM owns the revenue target and the attainment; HR owns the commission overlay.</strong> The
        annual target is set in CRM, not here — HR holds the bands, the MD's approval that someone is on
        commission, and the quarterly split. One number, one owner.
      </Alert>
      {s?.sourceNote && <Alert type="warning">{s.sourceNote}</Alert>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Bands in force" value={s?.bandsInForce ?? 0} />
        <Kpi label="Plans approved" value={s?.plansApproved ?? 0} color={T.green} />
        <Kpi label="Awaiting MD" value={s?.plansAwaitingMd ?? 0} color={T.amber} />
        <Kpi label="Earned YTD" value={money(s?.commissionEarnedYtd)} />
        <Kpi label="Paid YTD" value={money(s?.commissionPaidYtd)} color={T.green} />
        <Kpi label="Below 50%" value={s?.redFlagsBelow50 ?? 0} color={s?.redFlagsBelow50 ? T.red : T.green} sub="COM-007" />
      </div>

      <SectionHeader
        title="Commission Bands"
        sub="Year-to-date attainment against the ANNUAL target decides the rate (the pro-rated figure is what the COM-007/008 performance flags test, not the band). Bounds are half-open, so no attainment percentage falls between two bands."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            {(bands.data ?? []).length === 0 &&
              <Btn size="sm" variant="outline" onClick={() => act(hr.seedCommissionBands, 'Could not install the bands.')}>
                Install policy bands
              </Btn>}
            <Btn size="sm" onClick={() => setBandEditing({})}>+ Band</Btn>
          </div>
        }
      />
      <DataTable
        headers={['#', 'Band', 'Attainment', 'Rate', 'Effective', 'Status', '']}
        empty="No commission bands configured."
        rows={(bands.data ?? []).map(b => [
          b.displayOrder, b.label,
          b.maxPercent == null ? `above ${b.minPercent}%` : `${b.minPercent}% – ${b.maxPercent}%`,
          <strong>{b.commissionRatePercent}%</strong>,
          fmtDate(b.effectiveFrom),
          <Badge variant={b.isActive ? 'green' : 'default'}>{b.isActive ? 'Active' : 'Inactive'}</Badge>,
          <Btn size="sm" variant="outline" onClick={() => setBandEditing(b)}>Edit</Btn>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="Commission Plans"
          sub="COM-002 — the MD approves that a Sales Engineer is on commission. The target itself comes from CRM."
          action={<Btn size="sm" onClick={() => setEditing({})}>+ Plan</Btn>}
        />
      </div>
      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>
      <DataTable
        headers={['Employee', 'Year', 'Status', 'Target (from CRM)', 'Achieved', 'Attainment', 'Actions']}
        empty={`No commission plans for ${year}.`}
        rows={(plans.data ?? []).map(p => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{p.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{p.employeeName}</span>
          </span>,
          p.year,
          <span>
            <Badge variant={PLAN_VARIANT[p.status] ?? 'default'}>{p.status}</Badge>
            {p.rejectionReason && <span style={{ display: 'block', fontSize: 11, color: T.red }}>{p.rejectionReason}</span>}
          </span>,
          p.annualTargetFromCrm == null
            ? <span style={{ fontSize: 12, color: T.amber }} title={p.targetSourceNote}>not available</span>
            : money(p.annualTargetFromCrm),
          p.revenueAchievedFromCrm == null ? '—' : money(p.revenueAchievedFromCrm),
          p.attainmentPercent == null ? '—' : <strong>{pct(p.attainmentPercent)}</strong>,
          <div style={{ display: 'flex', gap: 6 }}>
            {p.status === 'Draft' && <>
              <Btn size="sm" variant="outline" onClick={() => setEditing(p)}>Edit</Btn>
              <Btn size="sm" onClick={() => act(() => hr.submitCommissionPlan(p.id), 'Could not submit.')}>Submit</Btn>
            </>}
            {p.status === 'PendingMd' && <>
              <Btn size="sm" variant="green"
                onClick={() => act(() => hr.decideCommissionPlan(p.id, { decision: 'Approve' }), 'Could not approve.')}>Approve</Btn>
              <Btn size="sm" variant="ghost" onClick={() => {
                const reason = window.prompt('Send this plan back? Give a reason:')
                if (reason === null) return
                act(() => hr.decideCommissionPlan(p.id, { decision: 'Reject', reason }), 'Could not reject.')
              }}>Reject</Btn>
            </>}
            {p.status === 'Rejected' && <Btn size="sm" variant="outline" onClick={() => setEditing(p)}>Revise</Btn>}
          </div>,
        ])}
      />
      {(plans.data ?? []).some(p => p.targetSourceNote && p.annualTargetFromCrm == null) && (
        <Alert type="warning">
          Some people have no annual sales target in CRM, or CRM could not be read. Targets are set in CRM —
          without one, no commission can be computed for them.
        </Alert>
      )}

      {editing && <PlanModal plan={editing.id ? editing : null} year={year} flash={flash}
        onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
      {bandEditing && <BandModal band={bandEditing.id ? bandEditing : null} flash={flash}
        onClose={() => setBandEditing(null)} onSaved={() => { setBandEditing(null); reload() }} />}
    </div>
  )
}

function PlanModal({ plan, year, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    employeeId: plan?.employeeId ?? '', year: plan?.year ?? year,
    basis: plan?.basis ?? '', notes: plan?.notes ?? '',
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.saveCommissionPlan({
        employeeId: f.employeeId, year: Number(f.year),
        basis: f.basis || null, notes: f.notes || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the plan.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={plan ? `${plan.employeeName} — ${plan.year} plan` : 'New commission plan'} onClose={onClose}>
      {!plan && (
        <Select label="Sales Engineer" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
          options={[{ value: '', label: 'Select…' },
            ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      )}
      <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} readOnly={!!plan} />
      <Input label="Basis" value={f.basis} onChange={v => setF({ ...f, basis: v })}
        note="What the MD is approving — e.g. 'Standard SE scheme, commission on collected revenue'" />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <Alert type="info">
        There is no revenue target field here on purpose. The target lives in CRM and is read from there — a
        second copy in HR is exactly how two numbers for the same target come to disagree.
      </Alert>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId}>{busy ? 'Saving…' : 'Save plan'}</Btn>
      </div>
    </Modal>
  )
}

function BandModal({ band, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    label: band?.label ?? '', minPercent: band?.minPercent ?? 0,
    maxPercent: band?.maxPercent ?? '', commissionRatePercent: band?.commissionRatePercent ?? 0,
    effectiveFrom: (band?.effectiveFrom ?? new Date().toISOString()).slice(0, 10),
    displayOrder: band?.displayOrder ?? 1,
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      label: f.label, minPercent: Number(f.minPercent),
      maxPercent: f.maxPercent === '' ? null : Number(f.maxPercent),
      commissionRatePercent: Number(f.commissionRatePercent),
      effectiveFrom: `${f.effectiveFrom}T00:00:00Z`,
      displayOrder: Number(f.displayOrder) || 1, isActive: true,
    }
    try {
      relay(flash, band ? await hr.updateCommissionBand(band.id, dto) : await hr.createCommissionBand(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the band.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={band ? `Edit ${band.label}` : 'New commission band'} onClose={onClose}>
      <Input label="Label" value={f.label} onChange={v => setF({ ...f, label: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="From %" type="number" value={f.minPercent} onChange={v => setF({ ...f, minPercent: v })}
          note="Exclusive" />
        <Input label="To %" type="number" value={f.maxPercent} onChange={v => setF({ ...f, maxPercent: v })}
          note="Inclusive; blank = top band" />
        <Input label="Rate %" type="number" value={f.commissionRatePercent}
          onChange={v => setF({ ...f, commissionRatePercent: v })} note="Of revenue" />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Effective from" type="date" value={f.effectiveFrom} onChange={v => setF({ ...f, effectiveFrom: v })} />
        <Input label="Order" type="number" value={f.displayOrder} onChange={v => setF({ ...f, displayOrder: v })} />
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        The scale must cover 0% upwards with no gap or overlap — saving reports any it finds.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.label.trim()}>{busy ? 'Saving…' : 'Save band'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Statements and disputes (P30 + P31)
// ═════════════════════════════════════════════════════════════════════════════
export function CommissionStatementsTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [status, setStatus] = useState('')
  const [computing, setComputing] = useState(false)
  const [disputing, setDisputing] = useState(null)
  const [resolving, setResolving] = useState(null)

  const statements = useData(() => hr.listCommissionStatements({ year, status: status || undefined }), [year, status])
  const disputes = useData(() => hr.listCommissionDisputes(), [])

  if (statements.loading) return <Loading />
  if (statements.denied) return <NoAccess />
  const reload = () => { statements.reload(); disputes.reload() }

  const act = async (fn, fallback) => {
    try { relay(flash, await fn()); reload() }
    catch (e) { relayError(flash, e, fallback) }
  }

  return (
    <div>
      <Alert type="info">
        <strong>Commission is paid through payroll</strong> so PAYE is deducted correctly (COM-005) — an
        approved statement is picked up automatically by the run for its period, exactly as approved overtime
        is, and cannot be paid twice. A statement under dispute is frozen until it is settled.
        <span style={{ display: 'block', marginTop: 6 }}>
          CRM reports revenue cumulatively for the year and the band follows attainment against the annual
          target, so each quarter is <strong>settled year-to-date</strong>: what the year has earned so far,
          less what earlier quarters already paid. Reaching a higher band later tops up the earlier quarters
          automatically.
        </span>
      </Alert>

      <SectionHeader
        title="Quarterly Statements"
        sub="Computed from CRM's target and attainment. If CRM cannot be read, computing is refused rather than producing zero-commission statements that look deliberate."
        action={<span style={{ display: 'flex', gap: 8 }}>
          <SweepButton flash={flash} onDone={reload} />
          <Btn size="sm" onClick={() => setComputing(true)}>Compute a quarter</Btn>
        </span>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Computed', 'Approved', 'Paid', 'Disputed', 'Cancelled'].map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Statement', 'Employee', 'Quarter', 'Target (annual)', 'Achieved YTD', 'Attainment', 'Band', 'This quarter', 'Status', 'Actions']}
        empty={`No statements for ${year}.`}
        rows={(statements.data ?? []).map(s => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700, fontSize: 12 }}>{s.statementNumber}</span>,
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{s.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{s.employeeName}</span>
          </span>,
          `Q${s.quarter}`,
          <span>{money(s.annualTarget, s.currencyCode)}
            <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>quarter {money(s.quarterTarget, s.currencyCode)}</span></span>,
          money(s.revenueAchieved, s.currencyCode),
          // Two figures, because they answer different questions and are read for different reasons: the
          // annual one sets the band, the pro-rated one is what the red flags test. Colouring the annual
          // figure red would call a Q1 performer who is exactly on plan a failure.
          <span style={{ fontWeight: 600 }}>
            <span style={{ color: T.dgrey }}>{pct(s.attainmentPercent)}</span>
            <span style={{ display: 'block', fontSize: 10, color: T.mgrey, fontWeight: 400 }}>of annual — sets the band</span>
            <span style={{
              display: 'block', marginTop: 3,
              color: s.proRatedAttainmentPercent < 50 ? T.red : s.proRatedAttainmentPercent < 70 ? T.amber : T.green,
            }}>{pct(s.proRatedAttainmentPercent)}</span>
            <span style={{ display: 'block', fontSize: 10, color: T.mgrey, fontWeight: 400 }}>
              of {money(s.ytdTarget, s.currencyCode)} pro-rated to Q1–Q{s.quarter}
            </span>
            {s.redFlag && <span style={{ display: 'block', fontSize: 10, color: T.red, fontWeight: 400 }}>{s.redFlag}</span>}
          </span>,
          <span style={{ fontSize: 12 }}>{s.bandLabel ?? '—'}<span style={{ display: 'block', color: T.mgrey }}>{s.commissionRatePercent}%</span></span>,
          <span>
            <strong>{money(s.commissionAmount, s.currencyCode)}</strong>
            {s.priorCommissionThisYear > 0 && (
              <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>
                {money(s.commissionEarnedToDate, s.currencyCode)} YTD less {money(s.priorCommissionThisYear, s.currencyCode)} already settled
              </span>
            )}
            {s.unrecoveredOverpayment > 0 && (
              <span style={{ display: 'block', fontSize: 11, color: T.red }}>
                earlier quarters settled {money(s.unrecoveredOverpayment, s.currencyCode)} more than the year has earned — not deducted
              </span>
            )}
          </span>,
          <span>
            <Badge variant={STATEMENT_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>
            <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 2 }}>{s.nextStep}</span>
          </span>,
          <div style={{ display: 'flex', gap: 6 }}>
            {s.status === 'Computed' && <>
              <Btn size="sm" variant="green"
                onClick={() => act(() => hr.decideCommissionStatement(s.id, { decision: 'Approve' }), 'Could not approve.')}>Approve</Btn>
              <Btn size="sm" variant="ghost" onClick={() => setDisputing(s)}>Dispute</Btn>
            </>}
            {s.status === 'Approved' && <Btn size="sm" variant="ghost" onClick={() => setDisputing(s)}>Dispute</Btn>}
            {['Paid', 'Disputed', 'Cancelled'].includes(s.status) &&
              <span style={{ fontSize: 11, color: T.mgrey }}>{s.status === 'Paid' ? `paid ${s.payrollPeriodCode ?? ''}` : 'closed'}</span>}
          </div>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader title="Disputes"
          sub="COM-006 — a dispute routes to HR and freezes the payment. Whoever raised it cannot settle it." />
      </div>
      {disputes.loading ? <Loading /> : (
        <DataTable
          headers={['Statement', 'Employee', 'Raised', 'What is contested', 'Status', 'Resolution', 'Actions']}
          empty="No commission disputes."
          rows={(disputes.data ?? []).map(d => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{d.statementNumber ?? '—'}</span>,
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{d.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{d.employeeName}</span>
            </span>,
            fmtDate(d.raisedAt),
            <span style={{ fontSize: 12 }}>{d.description}{d.disputedAmount != null && <span style={{ display: 'block', color: T.mgrey }}>claims {money(d.disputedAmount)}</span>}</span>,
            <Badge variant={DISPUTE_VARIANT[d.status] ?? 'default'}>{d.status}</Badge>,
            <span style={{ fontSize: 12, color: T.mgrey }}>
              {d.resolution ?? '—'}
              {d.adjustedAmount != null && <span style={{ display: 'block', color: T.green }}>adjusted to {money(d.adjustedAmount)}</span>}
            </span>,
            ['Open', 'UnderReview'].includes(d.status)
              ? <Btn size="sm" onClick={() => setResolving(d)}>Settle</Btn>
              : <span style={{ fontSize: 11, color: T.mgrey }}>settled</span>,
          ])}
        />
      )}

      {computing && <ComputeModal year={year} flash={flash}
        onClose={() => setComputing(false)} onSaved={() => { setComputing(false); reload() }} />}
      {disputing && <DisputeModal statement={disputing} flash={flash}
        onClose={() => setDisputing(null)} onSaved={() => { setDisputing(null); reload() }} />}
      {resolving && <ResolveDisputeModal dispute={resolving} flash={flash}
        onClose={() => setResolving(null)} onSaved={() => { setResolving(null); reload() }} />}
    </div>
  )
}

function ComputeModal({ year, flash, onClose, onSaved }) {
  const [f, setF] = useState({ year, quarter: '' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.computeCommissionStatements({
        year: Number(f.year), quarter: f.quarter === '' ? null : Number(f.quarter),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not compute.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Compute quarterly commission" onClose={onClose}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} />
        <Select label="Quarter" value={f.quarter} onChange={v => setF({ ...f, quarter: v })}
          options={[{ value: '', label: 'The quarter just ended' },
            ...[1, 2, 3, 4].map(q => ({ value: String(q), label: `Q${q}` }))]} />
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Only employees with an MD-approved plan are computed. The target and collected revenue come from CRM.
        The <strong>band</strong> follows attainment against the annual target, and the quarter is settled
        year-to-date — what the year has earned so far, less what earlier quarters already settled. The
        <strong> performance red flags</strong> are measured against the target pro-rated to the quarters
        elapsed, so somebody on plan in Q1 reads 100%, not 25%.
      </p>
      <Alert type="info">
        Safe to re-run — a statement already approved, paid or disputed is left alone, and only ones still
        awaiting approval are recomputed.
      </Alert>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Computing…' : 'Compute'}</Btn>
      </div>
    </Modal>
  )
}

function DisputeModal({ statement, flash, onClose, onSaved }) {
  const [f, setF] = useState({ description: '', disputedAmount: '' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.raiseCommissionDispute(statement.id, {
        description: f.description,
        disputedAmount: f.disputedAmount === '' ? null : Number(f.disputedAmount),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not raise the dispute.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Dispute ${statement.statementNumber}`} onClose={onClose}>
      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          {statement.employeeName} · Q{statement.quarter} · achieved {money(statement.revenueAchieved, statement.currencyCode)}
          {' '}of {money(statement.annualTarget, statement.currencyCode)} for the year ({pct(statement.attainmentPercent)}) ·
          this quarter <strong>{money(statement.commissionAmount, statement.currencyCode)}</strong>
        </p>
      </Card>
      <Input label="What is contested" value={f.description} onChange={v => setF({ ...f, description: v })} required />
      <Input label="Amount claimed" type="number" value={f.disputedAmount} onChange={v => setF({ ...f, disputedAmount: v })} />
      <Alert type="warning">
        Raising a dispute freezes the payment until it is settled. Whoever raises it cannot be the one who
        settles it.
      </Alert>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.description.trim()}>{busy ? 'Raising…' : 'Raise dispute'}</Btn>
      </div>
    </Modal>
  )
}

function ResolveDisputeModal({ dispute, flash, onClose, onSaved }) {
  const [f, setF] = useState({ outcome: '', findings: '', resolution: '', adjustedAmount: '' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.resolveCommissionDispute(dispute.id, {
        outcome: f.outcome, findings: f.findings || null, resolution: f.resolution,
        adjustedAmount: f.adjustedAmount === '' ? null : Number(f.adjustedAmount),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not settle the dispute.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Settle the dispute on ${dispute.statementNumber}`} onClose={onClose}>
      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{dispute.employeeName}</strong> — {dispute.description}
          {dispute.disputedAmount != null && <span style={{ display: 'block', color: T.mgrey }}>claims {money(dispute.disputedAmount)}</span>}
        </p>
      </Card>
      <Select label="Outcome" value={f.outcome} onChange={v => setF({ ...f, outcome: v })}
        options={[{ value: '', label: 'Select…' },
          { value: 'Upheld', label: 'Upheld — the figure was wrong' },
          { value: 'Rejected', label: 'Rejected — the figure stands' }]} required />
      <Input label="Findings" value={f.findings} onChange={v => setF({ ...f, findings: v })} />
      <Input label="What was decided" value={f.resolution} onChange={v => setF({ ...f, resolution: v })} required />
      {f.outcome === 'Upheld' && (
        <>
          <Input label="Corrected commission" type="number" value={f.adjustedAmount}
            onChange={v => setF({ ...f, adjustedAmount: v })} required />
          <p style={{ fontSize: 12, color: T.mgrey }}>
            The statement goes back for approval — an adjusted figure nobody re-approved is exactly what the
            approval step exists to prevent.
          </p>
        </>
      )}
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.outcome || !f.resolution.trim() || (f.outcome === 'Upheld' && f.adjustedAmount === '')}>
          {busy ? 'Settling…' : 'Settle'}
        </Btn>
      </div>
    </Modal>
  )
}
