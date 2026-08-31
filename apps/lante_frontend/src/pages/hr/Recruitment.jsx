import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H12 — recruitment (P1 entry point). REAL.
//
// Requisition → approval → vacancy → applicants → interviews → offer → hire.
// The order is the control, so each record here offers only the step it is
// actually on; the server refuses anything out of turn and its `nextStep` is
// what these screens show rather than a guess made client-side.
//
// Two people are needed at the sharp ends: whoever raises a requisition cannot
// approve it, and whoever prepares an offer cannot approve the salary on it.
// Both are refused on IDENTITY, not permission — holding hr.approve does not
// let you approve your own.
//
// Rejected and withdrawn candidates keep their stage and reason. That is what
// answers an unsuccessful applicant later, and what makes the source figures
// mean anything.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const fmtDateTime = (d) => (d ? new Date(d).toLocaleString('en-KE', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)
const today = () => new Date().toISOString().slice(0, 10)
const plusDays = (n) => new Date(Date.now() + n * 86400000).toISOString().slice(0, 10)

const REQ_VARIANT = { Draft: 'default', PendingApproval: 'amber', Approved: 'green', Rejected: 'red', Cancelled: 'default' }
const VAC_VARIANT = { Open: 'blue', Closed: 'default', Filled: 'green', Cancelled: 'red' }
const APP_VARIANT = {
  Applied: 'default', Shortlisted: 'blue', Interviewing: 'blue', Recommended: 'purple',
  OfferMade: 'amber', Hired: 'green', Rejected: 'red', Withdrawn: 'default',
}
const OFFER_VARIANT = {
  Draft: 'default', PendingApproval: 'amber', Approved: 'blue', Issued: 'amber',
  Accepted: 'green', Declined: 'red', Lapsed: 'red', Withdrawn: 'default',
}
const REC_VARIANT = { Pending: 'default', Proceed: 'green', Hold: 'amber', Reject: 'red' }

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 20, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// H12 says a great deal through `warnings` — the establishment arithmetic, an offer outside the
// advertised range, candidates left stranded on a filled vacancy. Dropping them would lose the point.
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
      const r = await hr.runRecruitmentSweep()
      const done = (r.vacanciesClosed ?? 0) + (r.offersLapsed ?? 0)
      flash(done === 0 && !r.interviewsOverdue
        ? 'Sweep complete — nothing to close or lapse.'
        : `Sweep: ${r.vacanciesClosed} vacancy(ies) closed, ${r.offersLapsed} offer(s) lapsed, ${r.interviewsOverdue} interview(s) past their slot unscored.`)
      onDone?.()
    } catch (e) { relayError(flash, e, 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Sweeping…' : 'Run sweep'}</Btn>
}

function Filter({ value, onChange, options, all = 'All' }) {
  return (
    <select value={value} onChange={e => onChange(e.target.value)}
      style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
      <option value="">{all}</option>
      {options.map(v => <option key={v} value={v}>{v}</option>)}
    </select>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Requisitions — the authority to hire
// ═════════════════════════════════════════════════════════════════════════════
export function RequisitionsTab({ flash }) {
  const [status, setStatus] = useState('')
  const [raising, setRaising] = useState(false)
  const [acting, setActing] = useState(null)

  const summary = useData(() => hr.recruitmentSummary(), [])
  const list = useData(() => hr.listRequisitions({ status: status || undefined }), [status])

  if (list.loading) return <Loading />
  if (list.denied) return <Alert type="warning">Recruitment is restricted — you do not have access to it.</Alert>
  const s = summary.data
  const reload = () => { summary.reload(); list.reload() }
  const rows = list.data ?? []

  return (
    <div>
      <Alert type="info">
        <strong>Nothing is advertised without an approved requisition.</strong> A vacancy is a commitment to pay
        somebody, and the approval is where that commitment is made — so the requisition is raised first, approved
        by a second person, and only then put to the market.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Draft" value={s?.draftRequisitions ?? 0} />
        <Kpi label="Awaiting approval" value={s?.requisitionsAwaitingApproval ?? 0} color={T.amber} />
        <Kpi label="Approved" value={s?.approvedRequisitions ?? 0} color={T.green} />
        <Kpi label="Approved, not advertised" value={s?.headcountApprovedNotPosted ?? 0}
          sub="heads" color={s?.headcountApprovedNotPosted ? T.amber : T.green} />
        <Kpi label="Hires this year" value={s?.hiresThisYear ?? 0} color={T.green} />
        <Kpi label="Average days to hire" value={s?.averageDaysToHire ?? '—'} sub="application → hire" />
      </div>

      <SectionHeader
        title="Job Requisitions"
        sub="The establishment arithmetic is captured when the requisition is raised, so an approver months later sees what the raiser saw."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            <SweepButton flash={flash} onDone={reload} />
            <Btn size="sm" onClick={() => setRaising(true)}>+ Requisition</Btn>
          </div>
        }
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <Filter value={status} onChange={setStatus}
          options={['Draft', 'PendingApproval', 'Approved', 'Rejected', 'Cancelled']} />
      </div>

      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Ref', 'Position', 'Type', 'Heads', 'Establishment', 'Status', 'Next step', '']}
          empty="No requisitions raised."
          rows={rows.map(r => [
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{r.requisitionNumber}</span>,
            <div>
              <strong>{r.positionTitle ?? '—'}</strong>
              <div style={{ fontSize: 11, color: T.mgrey }}>{r.departmentName ?? '—'}{r.jobGrade ? ` · ${r.jobGrade}` : ''}</div>
            </div>,
            <span style={{ fontSize: 12 }}>{r.requisitionType}</span>,
            <span>
              <strong>{r.headcountRequested}</strong>
              {r.headcountRemaining > 0 && r.status === 'Approved' &&
                <span style={{ fontSize: 11, color: T.amber, display: 'block' }}>{r.headcountRemaining} unadvertised</span>}
            </span>,
            <span style={{ fontSize: 11, color: r.exceedsEstablishment ? T.red : T.mgrey }}>
              {r.approvedHeadcount == null
                ? 'no establishment set'
                : `${r.approvedHeadcount} approved · ${r.currentHeadcount} in post`}
              {r.exceedsEstablishment && <strong style={{ display: 'block' }}>exceeds establishment</strong>}
            </span>,
            <Badge variant={REQ_VARIANT[r.status] ?? 'default'}>{r.status}</Badge>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{r.nextStep}</span>,
            <Btn size="sm" variant="outline" onClick={() => setActing(r)}>Open</Btn>,
          ])}
        />
      </Card>

      {raising && <RaiseRequisitionModal flash={flash} onClose={() => setRaising(false)}
        onSaved={() => { setRaising(false); reload() }} />}
      {acting && <RequisitionModal requisition={acting} flash={flash}
        onClose={() => setActing(null)} onSaved={reload} />}
    </div>
  )
}

function RaiseRequisitionModal({ flash, onClose, onSaved }) {
  const [positions, setPositions] = useState([])
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    positionId: '', requisitionType: 'Replacement', headcountRequested: 1,
    employmentType: 'Permanent', contractEndDate: '', replacingEmployeeId: '',
    justification: '', requiredBy: '', submitNow: false,
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listPositions().then(setPositions).catch(() => {})
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
  }, [])

  const needsEnd = f.employmentType === 'FixedTerm' || f.employmentType === 'Contract'
  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.raiseRequisition({
        positionId: f.positionId,
        requisitionType: f.requisitionType,
        headcountRequested: Number(f.headcountRequested) || 1,
        employmentType: f.employmentType,
        contractEndDate: needsEnd && f.contractEndDate ? `${f.contractEndDate}T00:00:00Z` : null,
        replacingEmployeeId: f.requisitionType === 'Replacement' && f.replacingEmployeeId ? f.replacingEmployeeId : null,
        justification: f.justification || null,
        requiredBy: f.requiredBy ? `${f.requiredBy}T00:00:00Z` : null,
        submitNow: f.submitNow,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not raise the requisition.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Raise a job requisition" onClose={onClose} width={640}>
      <Select label="Position" value={f.positionId} onChange={v => setF({ ...f, positionId: v })}
        options={[{ value: '', label: 'Select…' },
          ...positions.map(p => ({
            value: p.id,
            label: `${p.title}${p.approvedHeadcount == null ? ' (no establishment)' : ` — ${p.filledCount}/${p.approvedHeadcount} filled`}`,
          }))]}
        required note="An established post, never a free-text title — the establishment check counts against it" />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Why" value={f.requisitionType} onChange={v => setF({ ...f, requisitionType: v })}
          options={[
            { value: 'Replacement', label: 'Replacement — backfilling a leaver' },
            { value: 'NewRole', label: 'New role — an approved post never filled' },
            { value: 'Expansion', label: 'Expansion — growing the establishment' },
          ]} />
        <Input label="Heads" type="number" value={f.headcountRequested}
          onChange={v => setF({ ...f, headcountRequested: v })} required />
      </div>

      {f.requisitionType === 'Replacement' && (
        <Select label="Replacing" value={f.replacingEmployeeId} onChange={v => setF({ ...f, replacingEmployeeId: v })}
          options={[{ value: '', label: 'Not saying / not a specific person' },
            ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]}
          note="Points at the leaver; it does not copy them" />
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Employment type" value={f.employmentType} onChange={v => setF({ ...f, employmentType: v })}
          options={['Permanent', 'FixedTerm', 'Contract', 'Casual', 'Intern'].map(v => ({ value: v, label: v }))} />
        {needsEnd && (
          <Input label="Contract end date" type="date" value={f.contractEndDate}
            onChange={v => setF({ ...f, contractEndDate: v })} required
            note="Required — the employee record will need it too" />
        )}
      </div>

      <Input label="Justification" value={f.justification} onChange={v => setF({ ...f, justification: v })}
        note="What the approver is being asked to agree to" />
      <Input label="Needed by" type="date" value={f.requiredBy} onChange={v => setF({ ...f, requiredBy: v })} />

      <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, marginTop: 8 }}>
        <input type="checkbox" checked={f.submitNow} onChange={e => setF({ ...f, submitNow: e.target.checked })} />
        Send for approval straight away
      </label>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.positionId || (needsEnd && !f.contractEndDate)}>
          {busy ? 'Raising…' : 'Raise requisition'}
        </Btn>
      </div>
    </Modal>
  )
}

function RequisitionModal({ requisition, flash, onClose, onSaved }) {
  const [f, setF] = useState({})
  const [busy, setBusy] = useState(false)
  const detail = useData(() => hr.getRequisition(requisition.id), [requisition.id])

  if (detail.loading) return <Modal title="Requisition" onClose={onClose}><Loading /></Modal>
  const r = detail.data
  if (!r) return <Modal title="Requisition" onClose={onClose}><Alert type="error">Requisition not found.</Alert></Modal>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload(); onSaved() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${r.requisitionNumber} — ${r.positionTitle ?? 'position'}`} onClose={onClose} width={680}>
      <Alert type="info"><strong>{r.nextStep}</strong></Alert>

      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{r.headcountRequested} × {r.positionTitle}</strong> · {r.requisitionType} · {r.employmentType}
          {r.contractEndDate && ` to ${fmtDate(r.contractEndDate)}`}
        </p>
        {r.replacingEmployeeName && <p style={{ margin: '4px 0 0', fontSize: 12 }}>Replacing {r.replacingEmployeeName}</p>}
        {r.justification && <p style={{ margin: '4px 0 0', fontSize: 12, fontStyle: 'italic' }}>“{r.justification}”</p>}
        {r.requiredBy && <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>Needed by {fmtDate(r.requiredBy)}</p>}

        <p style={{ margin: '8px 0 0', fontSize: 12, color: r.exceedsEstablishment ? T.red : T.mgrey }}>
          <strong>Establishment as it stood when this was raised:</strong> {r.establishmentNotes}
        </p>

        {r.decidedAt && (
          <p style={{ margin: '8px 0 0', fontSize: 12 }}>
            {r.status} by {r.decidedByName ?? r.decidedBy} on {fmtDate(r.decidedAt)}
            {r.decisionReason && ` — ${r.decisionReason}`}
          </p>
        )}
        {r.status === 'Approved' && (
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            {r.headcountPosted} of {r.headcountRequested} head(s) advertised.
          </p>
        )}
      </Card>

      {r.status === 'Draft' && (
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn onClick={() => act(() => hr.submitRequisition(r.id), 'Could not submit.')} disabled={busy}>
            Send for approval
          </Btn>
          <Btn variant="outline" onClick={() => act(() => hr.cancelRequisition(r.id, { reason: f.reason || 'No longer needed.' }), 'Could not cancel.')}
            disabled={busy}>Cancel requisition</Btn>
        </div>
      )}

      {r.status === 'PendingApproval' && (
        <>
          <p style={{ fontSize: 12, color: T.mgrey }}>
            Approval needs <code>hr.approve</code>, and the person who raised this cannot approve it — holding the
            permission is not enough.
          </p>
          <Input label="Heads approved" type="number" value={f.approvedHeadcount ?? r.headcountRequested}
            onChange={v => setF({ ...f, approvedHeadcount: v })}
            note={`Up to the ${r.headcountRequested} requested; fewer is allowed`} />
          <Input label="Reason / conditions" value={f.reason ?? ''} onChange={v => setF({ ...f, reason: v })}
            note="Required to reject" />
          {r.exceedsEstablishment && (
            <Alert type="warning">
              Approving this takes {r.positionTitle} past its approved establishment. That is a legitimate
              decision — but it is one you are making here.
            </Alert>
          )}
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <Btn onClick={() => act(() => hr.decideRequisition(r.id, {
              decision: 'Approve',
              approvedHeadcount: Number(f.approvedHeadcount ?? r.headcountRequested) || r.headcountRequested,
              reason: f.reason || null,
            }), 'Could not approve.')} disabled={busy}>Approve</Btn>
            <Btn variant="outline" onClick={() => act(() => hr.decideRequisition(r.id, {
              decision: 'Reject', reason: f.reason,
            }), 'Could not reject.')} disabled={busy || !f.reason?.trim()}>Reject</Btn>
          </div>
        </>
      )}

      {r.status === 'Approved' && r.headcountRemaining > 0 && (
        <Alert type="info">
          Post a vacancy for the remaining {r.headcountRemaining} head(s) from the <strong>Vacancies</strong> tab.
        </Alert>
      )}
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Vacancies — the advert
// ═════════════════════════════════════════════════════════════════════════════
export function VacanciesTab({ flash }) {
  const [status, setStatus] = useState('')
  const [posting, setPosting] = useState(false)
  const [acting, setActing] = useState(null)

  const summary = useData(() => hr.recruitmentSummary(), [])
  const list = useData(() => hr.listVacancies({ status: status || undefined }), [status])

  if (list.loading) return <Loading />
  if (list.denied) return <Alert type="warning">Recruitment is restricted — you do not have access to it.</Alert>
  const s = summary.data
  const reload = () => { summary.reload(); list.reload() }

  return (
    <div>
      <Alert type="info">
        <strong>The requisition is the authority; the vacancy is the advert.</strong> Keeping them apart means an
        advert can be reposted, closed early or cancelled without touching the approval behind it — and no vacancy
        can ever carry more heads than were approved.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Open vacancies" value={s?.openVacancies ?? 0} color={T.blue} />
        <Kpi label="Seats to fill" value={s?.openHeadcount ?? 0} />
        <Kpi label="Closing within 7 days" value={s?.vacanciesClosingIn7Days ?? 0} color={T.amber} />
        <Kpi label="Applicants" value={s?.totalApplicants ?? 0} />
      </div>

      <SectionHeader title="Vacancies" action={
        <div style={{ display: 'flex', gap: 8 }}>
          <SweepButton flash={flash} onDone={reload} />
          <Btn size="sm" onClick={() => setPosting(true)}>+ Post Vacancy</Btn>
        </div>
      } />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <Filter value={status} onChange={setStatus} options={['Open', 'Closed', 'Filled', 'Cancelled']} />
      </div>

      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Ref', 'Position', 'Channel', 'Seats', 'Pipeline', 'Closing', 'Status', '']}
          empty="No vacancies posted."
          rows={(list.data ?? []).map(v => [
            <div>
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{v.vacancyNumber}</span>
              <div style={{ fontSize: 10, color: T.mgrey }}>from {v.requisitionNumber}</div>
            </div>,
            <div>
              <strong>{v.positionTitle ?? '—'}</strong>
              <div style={{ fontSize: 11, color: T.mgrey }}>
                {v.departmentName ?? '—'}
                {v.salaryRangeMin != null && ` · ${money(v.salaryRangeMin, v.currencyCode)}–${money(v.salaryRangeMax, v.currencyCode)}`}
              </div>
            </div>,
            <span style={{ fontSize: 12 }}>{v.postingChannel}</span>,
            <span><strong>{v.hiredCount}</strong> / {v.headcount}</span>,
            <span style={{ fontSize: 11, color: T.mgrey }}>
              {v.applicants} applied · {v.shortlisted} shortlisted · {v.interviewing} interviewing
              {v.offersOut > 0 && ` · ${v.offersOut} offer(s) out`}
              {v.rejected > 0 && <span style={{ display: 'block' }}>{v.rejected} closed out</span>}
            </span>,
            <span style={{ fontSize: 12, color: v.daysToClose != null && v.daysToClose <= 7 ? T.amber : T.dgrey }}>
              {fmtDate(v.closingDate)}
              {v.daysToClose != null && <span style={{ display: 'block', fontSize: 11 }}>{v.daysToClose} day(s)</span>}
            </span>,
            <Badge variant={VAC_VARIANT[v.status] ?? 'default'}>{v.status}</Badge>,
            <Btn size="sm" variant="outline" onClick={() => setActing(v)}>Open</Btn>,
          ])}
        />
      </Card>

      {posting && <PostVacancyModal flash={flash} onClose={() => setPosting(false)}
        onSaved={() => { setPosting(false); reload() }} />}
      {acting && <VacancyModal vacancy={acting} flash={flash} onClose={() => setActing(null)} onSaved={reload} />}
    </div>
  )
}

function PostVacancyModal({ flash, onClose, onSaved }) {
  const [approved, setApproved] = useState([])
  const [f, setF] = useState({
    jobRequisitionId: '', headcount: '', postingChannel: 'Both', jobDescription: '',
    minimumQualifications: '', responsibilities: '', salaryRangeMin: '', salaryRangeMax: '',
    closingDate: plusDays(21),
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listRequisitions({ status: 'Approved' })
      .then(rs => setApproved((rs ?? []).filter(r => r.headcountRemaining > 0)))
      .catch(() => {})
  }, [])

  const chosen = approved.find(r => r.id === f.jobRequisitionId)
  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.postVacancy({
        jobRequisitionId: f.jobRequisitionId,
        headcount: f.headcount === '' ? null : Number(f.headcount),
        postingChannel: f.postingChannel,
        jobDescription: f.jobDescription || null,
        minimumQualifications: f.minimumQualifications || null,
        responsibilities: f.responsibilities || null,
        salaryRangeMin: f.salaryRangeMin === '' ? null : Number(f.salaryRangeMin),
        salaryRangeMax: f.salaryRangeMax === '' ? null : Number(f.salaryRangeMax),
        closingDate: f.closingDate ? `${f.closingDate}T00:00:00Z` : null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not post the vacancy.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Post a vacancy" onClose={onClose} width={660}>
      {approved.length === 0 ? (
        <Alert type="warning">
          There is no approved requisition with heads left to advertise. Raise one and have it approved first —
          that is where the authority to hire comes from.
        </Alert>
      ) : (
        <>
          <Select label="Approved requisition" value={f.jobRequisitionId}
            onChange={v => setF({ ...f, jobRequisitionId: v })}
            options={[{ value: '', label: 'Select…' },
              ...approved.map(r => ({
                value: r.id,
                label: `${r.requisitionNumber} — ${r.positionTitle} (${r.headcountRemaining} head(s) left)`,
              }))]} required />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Heads to advertise" type="number" value={f.headcount}
              onChange={v => setF({ ...f, headcount: v })}
              note={chosen ? `Blank advertises all ${chosen.headcountRemaining} remaining` : 'Blank advertises all remaining'} />
            <Select label="Where" value={f.postingChannel} onChange={v => setF({ ...f, postingChannel: v })}
              options={[
                { value: 'Both', label: 'Internal and external' },
                { value: 'Internal', label: 'Internal only — the promotion path' },
                { value: 'External', label: 'External only' },
              ]} />
          </div>

          <Input label="Job description" value={f.jobDescription} onChange={v => setF({ ...f, jobDescription: v })} />
          <Input label="Minimum qualifications" value={f.minimumQualifications}
            onChange={v => setF({ ...f, minimumQualifications: v })} />
          <Input label="Responsibilities" value={f.responsibilities} onChange={v => setF({ ...f, responsibilities: v })} />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
            <Input label="Salary from" type="number" value={f.salaryRangeMin}
              onChange={v => setF({ ...f, salaryRangeMin: v })} />
            <Input label="Salary to" type="number" value={f.salaryRangeMax}
              onChange={v => setF({ ...f, salaryRangeMax: v })} />
            <Input label="Closing date" type="date" value={f.closingDate}
              onChange={v => setF({ ...f, closingDate: v })}
              note="Applications refused after this" />
          </div>

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
            <Btn onClick={save} disabled={busy || !f.jobRequisitionId}>{busy ? 'Posting…' : 'Post vacancy'}</Btn>
          </div>
        </>
      )}
    </Modal>
  )
}

function VacancyModal({ vacancy, flash, onClose, onSaved }) {
  const [f, setF] = useState({ reason: '' })
  const [busy, setBusy] = useState(false)
  const detail = useData(() => hr.getVacancy(vacancy.id), [vacancy.id])

  if (detail.loading) return <Modal title="Vacancy" onClose={onClose}><Loading /></Modal>
  const v = detail.data
  if (!v) return <Modal title="Vacancy" onClose={onClose}><Alert type="error">Vacancy not found.</Alert></Modal>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload(); onSaved() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${v.vacancyNumber} — ${v.positionTitle ?? 'position'}`} onClose={onClose} width={680}>
      <Alert type="info"><strong>{v.nextStep}</strong></Alert>

      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{v.hiredCount} of {v.headcount} hired</strong> · {v.postingChannel} · posted {fmtDate(v.postedAt)}
          {v.closingDate && ` · closes ${fmtDate(v.closingDate)}`}
        </p>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>
          Raised from {v.requisitionNumber}
          {v.salaryRangeMin != null && ` · ${money(v.salaryRangeMin, v.currencyCode)} – ${money(v.salaryRangeMax, v.currencyCode)}`}
        </p>
        {v.jobDescription && <p style={{ margin: '8px 0 0', fontSize: 12 }}>{v.jobDescription}</p>}
        {v.minimumQualifications && <p style={{ margin: '4px 0 0', fontSize: 12 }}><strong>Minimum:</strong> {v.minimumQualifications}</p>}
        {v.responsibilities && <p style={{ margin: '4px 0 0', fontSize: 12 }}><strong>Responsibilities:</strong> {v.responsibilities}</p>}
        <p style={{ margin: '8px 0 0', fontSize: 12 }}>
          {v.applicants} applied · {v.shortlisted} shortlisted · {v.interviewing} interviewing ·
          {' '}{v.offersOut} offer(s) out · {v.rejected} closed out
        </p>
        {v.closedAt && (
          <p style={{ margin: '6px 0 0', fontSize: 12, color: T.mgrey }}>
            {v.status} on {fmtDate(v.closedAt)}{v.closureReason && ` — ${v.closureReason}`}
          </p>
        )}
      </Card>

      {(v.status === 'Open' || v.status === 'Closed') && v.status !== 'Filled' && (
        <>
          <Input label="Reason" value={f.reason} onChange={v2 => setF({ ...f, reason: v2 })} />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            {v.status === 'Open' && (
              <Btn variant="outline" onClick={() => act(() => hr.closeVacancy(v.id, { reason: f.reason || null, cancel: false }), 'Could not close.')}
                disabled={busy}>Close to applications</Btn>
            )}
            {v.hiredCount === 0 && (
              <Btn variant="outline" onClick={() => act(() => hr.closeVacancy(v.id, { reason: f.reason || null, cancel: true }), 'Could not cancel.')}
                disabled={busy}>Cancel vacancy</Btn>
            )}
          </div>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: 8 }}>
            Closing stops new applications; it does not reject the candidates already in the pipeline. A vacancy
            that has produced a hire can be closed but never cancelled.
          </p>
        </>
      )}
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Applicants — the pipeline, including everyone who did not get the job
// ═════════════════════════════════════════════════════════════════════════════
export function ApplicantsTab({ flash }) {
  const [vacancyId, setVacancyId] = useState('')
  const [status, setStatus] = useState('')
  const [adding, setAdding] = useState(false)
  const [acting, setActing] = useState(null)
  const [vacancies, setVacancies] = useState([])

  const summary = useData(() => hr.recruitmentSummary(), [])
  const sources = useData(() => hr.sourceEffectiveness(new Date().getFullYear()), [])
  const list = useData(() => hr.listApplicants({
    vacancyId: vacancyId || undefined, status: status || undefined,
  }), [vacancyId, status])

  useEffect(() => { hr.listVacancies().then(v => setVacancies(v ?? [])).catch(() => {}) }, [])

  if (list.loading) return <Loading />
  if (list.denied) return <Alert type="warning">Recruitment is restricted — you do not have access to it.</Alert>
  const s = summary.data
  const reload = () => { summary.reload(); sources.reload(); list.reload() }

  return (
    <div>
      <Alert type="info">
        <strong>Screen → interview → recommend → offer.</strong> Each gate stands behind the one before it: nobody
        is interviewed off an unscreened pile and no offer goes out without a panel recommendation. Candidates who
        do not get the job keep their stage and reason — that is what answers them later.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Awaiting screening" value={s?.awaitingScreening ?? 0} color={s?.awaitingScreening ? T.amber : T.green} />
        <Kpi label="Shortlisted" value={s?.shortlisted ?? 0} color={T.blue} />
        <Kpi label="Interviewing" value={s?.interviewing ?? 0} color={T.blue} />
        <Kpi label="Interviews booked" value={s?.interviewsScheduled ?? 0} />
        <Kpi label="Past slot, unscored" value={s?.interviewsAwaitingScore ?? 0}
          color={s?.interviewsAwaitingScore ? T.red : T.green} sub="only the panel can settle these" />
      </div>

      <SectionHeader title="Applicants" action={<Btn size="sm" onClick={() => setAdding(true)}>+ Application</Btn>} />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={vacancyId} onChange={e => setVacancyId(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All vacancies</option>
          {vacancies.map(v => <option key={v.id} value={v.id}>{v.vacancyNumber} — {v.positionTitle}</option>)}
        </select>
        <Filter value={status} onChange={setStatus}
          options={['Applied', 'Shortlisted', 'Interviewing', 'Recommended', 'OfferMade', 'Hired', 'Rejected', 'Withdrawn']} />
      </div>

      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Ref', 'Candidate', 'Vacancy', 'Source', 'Interviews', 'Status', 'Next step', '']}
          empty="No applications received."
          rows={(list.data ?? []).map(a => [
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{a.applicantNumber}</span>,
            <div>
              <strong>{a.fullName}</strong>
              <div style={{ fontSize: 11, color: T.mgrey }}>
                {a.email}{a.yearsExperience != null && ` · ${a.yearsExperience} yr(s)`}
              </div>
            </div>,
            <span style={{ fontSize: 12 }}>{a.vacancyNumber}<div style={{ fontSize: 11, color: T.mgrey }}>{a.positionTitle}</div></span>,
            <span style={{ fontSize: 12 }}>
              {a.source}
              {a.referredByName && <div style={{ fontSize: 10, color: T.mgrey }}>by {a.referredByName}</div>}
              {a.internalEmployeeId && <div style={{ fontSize: 10, color: T.blue }}>internal</div>}
            </span>,
            <span style={{ fontSize: 12 }}>
              {a.interviewsHeld} held
              {a.averageInterviewScore != null && <strong style={{ display: 'block' }}>{a.averageInterviewScore}/10</strong>}
              {a.interviewsScheduled > 0 && <span style={{ fontSize: 10, color: T.amber }}>{a.interviewsScheduled} booked</span>}
            </span>,
            <div>
              <Badge variant={APP_VARIANT[a.status] ?? 'default'}>{a.status}</Badge>
              {a.rejectedAtStage && a.status !== 'Hired' &&
                <div style={{ fontSize: 10, color: T.mgrey, marginTop: 2 }}>at {a.rejectedAtStage}</div>}
            </div>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{a.nextStep}</span>,
            <Btn size="sm" variant="outline" onClick={() => setActing(a)}>Open</Btn>,
          ])}
        />
      </Card>

      <SectionHeader title="Where our hires actually come from"
        sub="Counts candidates who got past screening, including those who then fell — a channel whose people reach the panel and lose there is not a channel that produced nothing." />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Source', 'Applicants', 'Past screening', 'Hired', 'Shortlist rate', 'Hire rate']}
          empty="No applications yet this year."
          rows={(sources.data ?? []).map(x => [
            <strong>{x.source}</strong>, x.applicants, x.shortlisted,
            <strong style={{ color: x.hired ? T.green : T.mgrey }}>{x.hired}</strong>,
            `${x.shortlistRate}%`,
            <span style={{ color: x.hireRate > 0 ? T.green : T.mgrey }}>{x.hireRate}%</span>,
          ])}
        />
      </Card>

      {adding && <ReceiveApplicationModal vacancies={vacancies} flash={flash} onClose={() => setAdding(false)}
        onSaved={() => { setAdding(false); reload() }} />}
      {acting && <ApplicantModal applicantId={acting.id} flash={flash}
        onClose={() => setActing(null)} onSaved={reload} />}
    </div>
  )
}

function ReceiveApplicationModal({ vacancies, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    vacancyId: '', fullName: '', email: '', phone: '', nationalId: '', source: 'Website',
    referredByEmployeeId: '', internalEmployeeId: '', yearsExperience: '',
    highestQualification: '', currentEmployer: '', expectedSalary: '', cvDocumentPath: '', coverNote: '',
  })
  const [busy, setBusy] = useState(false)
  const open = vacancies.filter(v => v.status === 'Open')

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.receiveApplication({
        vacancyId: f.vacancyId, fullName: f.fullName, email: f.email,
        phone: f.phone || null, nationalId: f.nationalId || null, source: f.source,
        referredByEmployeeId: f.source === 'Referral' && f.referredByEmployeeId ? f.referredByEmployeeId : null,
        internalEmployeeId: f.source === 'Internal' && f.internalEmployeeId ? f.internalEmployeeId : null,
        yearsExperience: f.yearsExperience === '' ? null : Number(f.yearsExperience),
        highestQualification: f.highestQualification || null,
        currentEmployer: f.currentEmployer || null,
        expectedSalary: f.expectedSalary === '' ? null : Number(f.expectedSalary),
        cvDocumentPath: f.cvDocumentPath || null, coverNote: f.coverNote || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not record the application.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Record an application" onClose={onClose} width={660}>
      {open.length === 0 ? (
        <Alert type="warning">No vacancy is open for applications.</Alert>
      ) : (
        <>
          <Select label="Vacancy" value={f.vacancyId} onChange={v => setF({ ...f, vacancyId: v })}
            options={[{ value: '', label: 'Select…' },
              ...open.map(v => ({ value: v.id, label: `${v.vacancyNumber} — ${v.positionTitle}` }))]} required />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Full name" value={f.fullName} onChange={v => setF({ ...f, fullName: v })} required />
            <Input label="Email" type="email" value={f.email} onChange={v => setF({ ...f, email: v })} required
              note="One application per email per vacancy" />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Phone" value={f.phone} onChange={v => setF({ ...f, phone: v })} />
            <Input label="National ID" value={f.nationalId} onChange={v => setF({ ...f, nationalId: v })} />
          </div>

          <Select label="How they reached us" value={f.source} onChange={v => setF({ ...f, source: v })}
            options={[
              { value: 'Website', label: 'Website' }, { value: 'JobBoard', label: 'Job board' },
              { value: 'Referral', label: 'Referred by a member of staff' },
              { value: 'Internal', label: 'Internal — an existing employee applying' },
              { value: 'Agency', label: 'Agency' }, { value: 'WalkIn', label: 'Walk-in' },
            ]} />

          {f.source === 'Referral' && (
            <Select label="Referred by" value={f.referredByEmployeeId}
              onChange={v => setF({ ...f, referredByEmployeeId: v })}
              options={[{ value: '', label: 'Select…' },
                ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} />
          )}
          {f.source === 'Internal' && (
            <Select label="Which employee" value={f.internalEmployeeId}
              onChange={v => setF({ ...f, internalEmployeeId: v })}
              options={[{ value: '', label: 'Select…' },
                ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]}
              required note="Links to their record — an internal move is a transfer, not a new person" />
          )}

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
            <Input label="Years of experience" type="number" value={f.yearsExperience}
              onChange={v => setF({ ...f, yearsExperience: v })} />
            <Input label="Highest qualification" value={f.highestQualification}
              onChange={v => setF({ ...f, highestQualification: v })} />
            <Input label="Expected salary" type="number" value={f.expectedSalary}
              onChange={v => setF({ ...f, expectedSalary: v })} />
          </div>
          <Input label="Current employer" value={f.currentEmployer} onChange={v => setF({ ...f, currentEmployer: v })} />
          <Input label="CV (document path)" value={f.cvDocumentPath} onChange={v => setF({ ...f, cvDocumentPath: v })} />
          <Input label="Cover note" value={f.coverNote} onChange={v => setF({ ...f, coverNote: v })} />

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
            <Btn onClick={save} disabled={busy || !f.vacancyId || !f.fullName.trim() || !f.email.trim()}>
              {busy ? 'Recording…' : 'Record application'}
            </Btn>
          </div>
        </>
      )}
    </Modal>
  )
}

function ApplicantModal({ applicantId, flash, onClose, onSaved }) {
  const [f, setF] = useState({})
  const [busy, setBusy] = useState(false)
  const detail = useData(() => hr.getApplicant(applicantId), [applicantId])
  const interviews = useData(() => hr.listInterviews({ applicantId }), [applicantId])

  if (detail.loading) return <Modal title="Applicant" onClose={onClose}><Loading /></Modal>
  const a = detail.data
  if (!a) return <Modal title="Applicant" onClose={onClose}><Alert type="error">Applicant not found.</Alert></Modal>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload(); interviews.reload(); onSaved() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  const closed = a.status === 'Rejected' || a.status === 'Withdrawn' || a.status === 'Hired'
  const unscored = (interviews.data ?? []).filter(i => !i.held && !i.cancelledAt)

  return (
    <Modal title={`${a.applicantNumber} — ${a.fullName}`} onClose={onClose} width={720}>
      <Alert type="info"><strong>{a.nextStep}</strong></Alert>

      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{a.positionTitle}</strong> ({a.vacancyNumber}) · applied {fmtDate(a.appliedAt)} via {a.source}
        </p>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>
          {a.email}{a.phone && ` · ${a.phone}`}
          {a.yearsExperience != null && ` · ${a.yearsExperience} year(s) experience`}
          {a.highestQualification && ` · ${a.highestQualification}`}
          {a.currentEmployer && ` · currently at ${a.currentEmployer}`}
        </p>
        {a.expectedSalary != null && <p style={{ margin: '4px 0 0', fontSize: 12 }}>Asking {money(a.expectedSalary)}</p>}
        {a.coverNote && <p style={{ margin: '6px 0 0', fontSize: 12, fontStyle: 'italic' }}>“{a.coverNote}”</p>}
        {a.screenedAt && (
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            Screened {fmtDate(a.screenedAt)}{a.screeningNotes && ` — ${a.screeningNotes}`}
          </p>
        )}
        {a.averageInterviewScore != null && (
          <p style={{ margin: '4px 0 0', fontSize: 12 }}>
            <strong>{a.averageInterviewScore}/10</strong> across {a.interviewsHeld} interview(s)
          </p>
        )}
        {a.rejectionReason && (
          <p style={{ margin: '6px 0 0', fontSize: 12, color: T.red }}>
            {a.status} at {a.rejectedAtStage} — {a.rejectionReason}
          </p>
        )}
        {a.resultingEmployeeNumber && (
          <p style={{ margin: '6px 0 0', fontSize: 12, color: T.green }}>
            <strong>Hired {fmtDate(a.hiredAt)} as {a.resultingEmployeeNumber}.</strong> Onboarding continues on the
            employee record.
          </p>
        )}
        {a.offerNumber && (
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            Offer {a.offerNumber} — <Badge variant={OFFER_VARIANT[a.offerStatus] ?? 'default'}>{a.offerStatus}</Badge>
          </p>
        )}
      </Card>

      {(interviews.data ?? []).length > 0 && (
        <DataTable
          headers={['Stage', 'When', 'Panel', 'Score', 'Verdict', '']}
          empty=""
          rows={(interviews.data ?? []).map(i => [
            <span style={{ fontSize: 12 }}>{i.stage}</span>,
            <span style={{ fontSize: 12, color: i.overdue ? T.red : T.dgrey }}>
              {fmtDateTime(i.scheduledAt)}{i.overdue && <div style={{ fontSize: 10 }}>past its slot, unscored</div>}
            </span>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{i.panelMembers ?? '—'}</span>,
            <span style={{ fontSize: 12 }}>
              {i.overallScore != null ? <strong>{i.overallScore}/10</strong> : '—'}
              {i.held && (
                <div style={{ fontSize: 10, color: T.mgrey }}>
                  {[['T', i.technicalScore], ['E', i.experienceScore], ['C', i.communicationScore], ['F', i.culturalFitScore]]
                    .filter(([, v]) => v != null).map(([k, v]) => `${k} ${v}`).join(' · ')}
                </div>
              )}
            </span>,
            <Badge variant={REC_VARIANT[i.recommendation] ?? 'default'}>
              {i.cancelledAt ? 'Cancelled' : i.recommendation}
            </Badge>,
            !i.held && !i.cancelledAt
              ? <Btn size="sm" variant="outline" onClick={() => setF({ ...f, scoring: i.id })}>Score</Btn>
              : <span />,
          ])}
        />
      )}

      {f.scoring && <ScoreInterviewModal interviewId={f.scoring} flash={flash}
        onClose={() => setF({ ...f, scoring: null })}
        onSaved={() => { setF({ ...f, scoring: null }); detail.reload(); interviews.reload(); onSaved() }} />}

      {a.status === 'Applied' && (
        <>
          <Input label="Screening notes" value={f.notes ?? ''} onChange={v => setF({ ...f, notes: v })} />
          <Input label="Reason (required to reject)" value={f.reason ?? ''} onChange={v => setF({ ...f, reason: v })}
            note="“Not selected” is not a reason — this may have to be explained later" />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <Btn onClick={() => act(() => hr.screenApplicant(a.id, { decision: 'Shortlist', notes: f.notes || null }), 'Could not shortlist.')}
              disabled={busy}>Shortlist</Btn>
            <Btn variant="outline" onClick={() => act(() => hr.screenApplicant(a.id, { decision: 'Reject', reason: f.reason, notes: f.notes || null }), 'Could not reject.')}
              disabled={busy || !f.reason?.trim()}>Reject at screening</Btn>
          </div>
        </>
      )}

      {['Shortlisted', 'Interviewing', 'Recommended'].includes(a.status) && (
        <>
          <SectionHeader title="Schedule an interview" />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Select label="Stage" value={f.stage ?? 'Panel'} onChange={v => setF({ ...f, stage: v })}
              options={['Screening', 'Technical', 'Panel', 'Final'].map(v => ({ value: v, label: v }))} />
            <Input label="When" type="datetime-local" value={f.when ?? ''} onChange={v => setF({ ...f, when: v })} />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Where" value={f.location ?? ''} onChange={v => setF({ ...f, location: v })}
              note="A room, a link or a phone call" />
            <Input label="Panel" value={f.panel ?? ''} onChange={v => setF({ ...f, panel: v })}
              note="Panellists need not be system users" />
          </div>
          <Btn onClick={() => act(() => hr.scheduleInterview({
            applicantId: a.id, stage: f.stage ?? 'Panel',
            scheduledAt: new Date(f.when).toISOString(),
            location: f.location || null, panelMembers: f.panel || null,
          }), 'Could not schedule.')} disabled={busy || !f.when}>Schedule</Btn>
        </>
      )}

      {a.status === 'Recommended' && unscored.length === 0 && (
        <Alert type="info">
          A panel has recommended this candidate — prepare an offer from the <strong>Offers</strong> tab.
        </Alert>
      )}

      {!closed && (
        <>
          <SectionHeader title="Close this candidate out" />
          <Input label="Reason" value={f.closeReason ?? ''} onChange={v => setF({ ...f, closeReason: v })} />
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn variant="outline" onClick={() => act(() => hr.rejectApplicant(a.id, { reason: f.closeReason }), 'Could not reject.')}
              disabled={busy || !f.closeReason?.trim()}>Reject</Btn>
            <Btn variant="outline" onClick={() => act(() => hr.withdrawApplicant(a.id, { reason: f.closeReason || null }), 'Could not record.')}
              disabled={busy}>They withdrew</Btn>
          </div>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: 8 }}>
            Both keep the stage they had reached. A candidate holding a live offer cannot be rejected behind it —
            withdraw the offer first, deliberately.
          </p>
        </>
      )}
    </Modal>
  )
}

function ScoreInterviewModal({ interviewId, flash, onClose, onSaved }) {
  const [f, setF] = useState({ recommendation: '', notes: '' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.scoreInterview(interviewId, {
        technicalScore: f.technical === '' || f.technical == null ? null : Number(f.technical),
        experienceScore: f.experience === '' || f.experience == null ? null : Number(f.experience),
        communicationScore: f.communication === '' || f.communication == null ? null : Number(f.communication),
        culturalFitScore: f.cultural === '' || f.cultural == null ? null : Number(f.cultural),
        recommendation: f.recommendation, notes: f.notes || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not record the score.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Record what the panel concluded" onClose={onClose}>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Each criterion is out of ten and every one is optional — the overall is the mean of the ones actually
        scored, so a screening call that judged experience and communication is not marked down for a technical
        score nobody gave. Score at least one.
      </p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Technical" type="number" value={f.technical ?? ''} onChange={v => setF({ ...f, technical: v })} />
        <Input label="Experience" type="number" value={f.experience ?? ''} onChange={v => setF({ ...f, experience: v })} />
        <Input label="Communication" type="number" value={f.communication ?? ''} onChange={v => setF({ ...f, communication: v })} />
        <Input label="Cultural fit" type="number" value={f.cultural ?? ''} onChange={v => setF({ ...f, cultural: v })} />
      </div>
      <Select label="Verdict" value={f.recommendation} onChange={v => setF({ ...f, recommendation: v })}
        options={[{ value: '', label: 'Select…' },
          { value: 'Proceed', label: 'Proceed — an offer may follow' },
          { value: 'Hold', label: 'Hold — acceptable, but not ahead of others' },
          { value: 'Reject', label: 'Reject — this closes the candidate out' }]} required />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })}
        note="On a Reject this becomes the reason the candidate is given" />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.recommendation}>{busy ? 'Saving…' : 'Record'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Offers — and the hand-off to the employee record
// ═════════════════════════════════════════════════════════════════════════════
export function OffersTab({ flash }) {
  const [status, setStatus] = useState('')
  const [preparing, setPreparing] = useState(false)
  const [acting, setActing] = useState(null)

  const summary = useData(() => hr.recruitmentSummary(), [])
  const list = useData(() => hr.listOffers({ status: status || undefined }), [status])

  if (list.loading) return <Loading />
  if (list.denied) return <Alert type="warning">Recruitment is restricted — you do not have access to it.</Alert>
  const s = summary.data
  const reload = () => { summary.reload(); list.reload() }

  return (
    <div>
      <Alert type="info">
        <strong>The salary is approved before the offer goes out</strong>, by someone other than whoever prepared
        it. An offer issued first and approved afterwards is not an approval — it is a ratification of a promise
        already made. Only an <em>accepted</em> offer can become an employee record.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Awaiting approval" value={s?.offersAwaitingApproval ?? 0} color={T.amber} />
        <Kpi label="Out with candidates" value={s?.offersOutstanding ?? 0} color={T.blue} />
        <Kpi label="Response overdue" value={s?.offersOverdue ?? 0} color={s?.offersOverdue ? T.red : T.green}
          sub="the sweep lapses these" />
        <Kpi label="Answered this year" value={s?.offersAnsweredThisYear ?? 0} />
        <Kpi label="Accepted" value={s?.offersAcceptedThisYear ?? 0} color={T.green}
          sub={s?.offersAnsweredThisYear ? `${Math.round((s.offersAcceptedThisYear / s.offersAnsweredThisYear) * 100)}% of answers` : undefined} />
      </div>

      <SectionHeader title="Job Offers" action={
        <div style={{ display: 'flex', gap: 8 }}>
          <SweepButton flash={flash} onDone={reload} />
          <Btn size="sm" onClick={() => setPreparing(true)}>+ Offer</Btn>
        </div>
      } />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <Filter value={status} onChange={setStatus}
          options={['Draft', 'PendingApproval', 'Approved', 'Issued', 'Accepted', 'Declined', 'Lapsed', 'Withdrawn']} />
      </div>

      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Ref', 'Candidate', 'Position', 'Salary', 'Start', 'Response due', 'Status', '']}
          empty="No offers prepared."
          rows={(list.data ?? []).map(o => [
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{o.offerNumber}</span>,
            <div>
              <strong>{o.applicantName}</strong>
              <div style={{ fontSize: 11, color: T.mgrey }}>{o.vacancyNumber}</div>
            </div>,
            <span style={{ fontSize: 12 }}>{o.positionTitle}<div style={{ fontSize: 11, color: T.mgrey }}>{o.employmentType}</div></span>,
            <div>
              <strong>{money(o.offeredSalary, o.currencyCode)}</strong>
              {o.salaryRangeNote && o.salaryRangeNote !== 'Within the advertised range.' &&
                <div style={{ fontSize: 10, color: T.amber }}>{o.salaryRangeNote}</div>}
            </div>,
            <span style={{ fontSize: 12 }}>{fmtDate(o.proposedStartDate)}</span>,
            <span style={{ fontSize: 12, color: o.responseOverdue ? T.red : T.dgrey }}>
              {fmtDate(o.responseDeadline)}{o.responseOverdue && <div style={{ fontSize: 10 }}>overdue</div>}
            </span>,
            <Badge variant={OFFER_VARIANT[o.status] ?? 'default'}>{o.status}</Badge>,
            <Btn size="sm" variant="outline" onClick={() => setActing(o)}>Open</Btn>,
          ])}
        />
      </Card>

      {preparing && <PrepareOfferModal flash={flash} onClose={() => setPreparing(false)}
        onSaved={() => { setPreparing(false); reload() }} />}
      {acting && <OfferModal offerId={acting.id} flash={flash} onClose={() => setActing(null)} onSaved={reload} />}
    </div>
  )
}

function PrepareOfferModal({ flash, onClose, onSaved }) {
  const [candidates, setCandidates] = useState([])
  const [structures, setStructures] = useState([])
  const [f, setF] = useState({
    applicantId: '', offeredSalary: '', proposedStartDate: plusDays(30), employmentType: '',
    contractEndDate: '', probationMonths: 3, responseDays: 7, terms: '', salaryStructureId: '', submitNow: false,
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listApplicants({ status: 'Recommended' }).then(a => setCandidates(a ?? [])).catch(() => {})
    // Structures sit behind the ring-fenced payroll tier, which a recruiter need not hold — a 403 here just
    // means the picker is not offered and the structure is chosen at onboarding instead.
    hr.listStructures().then(s => setStructures(s ?? [])).catch(() => {})
  }, [])

  const needsEnd = f.employmentType === 'FixedTerm' || f.employmentType === 'Contract'
  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.prepareOffer({
        applicantId: f.applicantId,
        offeredSalary: Number(f.offeredSalary),
        proposedStartDate: `${f.proposedStartDate}T00:00:00Z`,
        employmentType: f.employmentType || null,
        contractEndDate: needsEnd && f.contractEndDate ? `${f.contractEndDate}T00:00:00Z` : null,
        probationMonths: Number(f.probationMonths) || 3,
        responseDays: Number(f.responseDays) || 7,
        terms: f.terms || null,
        salaryStructureId: f.salaryStructureId || null,
        submitNow: f.submitNow,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not prepare the offer.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Prepare a job offer" onClose={onClose} width={640}>
      {candidates.length === 0 ? (
        <Alert type="warning">
          No candidate has a panel recommendation. An offer needs one behind it — score the interview first.
        </Alert>
      ) : (
        <>
          <Select label="Candidate" value={f.applicantId} onChange={v => setF({ ...f, applicantId: v })}
            options={[{ value: '', label: 'Select…' },
              ...candidates.map(c => ({
                value: c.id,
                label: `${c.fullName} — ${c.positionTitle}${c.averageInterviewScore != null ? ` (${c.averageInterviewScore}/10)` : ''}`,
              }))]} required />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Salary offered" type="number" value={f.offeredSalary}
              onChange={v => setF({ ...f, offeredSalary: v })} required
              note="Outside the advertised range is allowed, but it is flagged" />
            <Input label="Proposed start" type="date" value={f.proposedStartDate}
              onChange={v => setF({ ...f, proposedStartDate: v })} required />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Select label="Employment type" value={f.employmentType} onChange={v => setF({ ...f, employmentType: v })}
              options={[{ value: '', label: 'As the requisition said' },
                ...['Permanent', 'FixedTerm', 'Contract', 'Casual', 'Intern'].map(v => ({ value: v, label: v }))]} />
            {needsEnd && (
              <Input label="Contract end" type="date" value={f.contractEndDate}
                onChange={v => setF({ ...f, contractEndDate: v })} required />
            )}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Input label="Probation (months)" type="number" value={f.probationMonths}
              onChange={v => setF({ ...f, probationMonths: v })} />
            <Input label="Days to answer" type="number" value={f.responseDays}
              onChange={v => setF({ ...f, responseDays: v })}
              note="Counted from ISSUE, so waiting on approval does not eat the window" />
          </div>

          {structures.length > 0 && (
            <Select label="Salary structure" value={f.salaryStructureId}
              onChange={v => setF({ ...f, salaryStructureId: v })}
              options={[{ value: '', label: 'Decide at onboarding' },
                ...structures.map(s => ({ value: s.id, label: s.name }))]} />
          )}

          <Input label="Terms" value={f.terms} onChange={v => setF({ ...f, terms: v })} />

          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, marginTop: 8 }}>
            <input type="checkbox" checked={f.submitNow} onChange={e => setF({ ...f, submitNow: e.target.checked })} />
            Send for salary approval straight away
          </label>

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
            <Btn onClick={save} disabled={busy || !f.applicantId || !f.offeredSalary || (needsEnd && !f.contractEndDate)}>
              {busy ? 'Preparing…' : 'Prepare offer'}
            </Btn>
          </div>
        </>
      )}
    </Modal>
  )
}

function OfferModal({ offerId, flash, onClose, onSaved }) {
  const [f, setF] = useState({})
  const [busy, setBusy] = useState(false)
  const [hiring, setHiring] = useState(false)
  const detail = useData(() => hr.getOffer(offerId), [offerId])

  if (detail.loading) return <Modal title="Offer" onClose={onClose}><Loading /></Modal>
  const o = detail.data
  if (!o) return <Modal title="Offer" onClose={onClose}><Alert type="error">Offer not found.</Alert></Modal>

  const act = async (fn, fallback) => {
    setBusy(true)
    try { relay(flash, await fn()); detail.reload(); onSaved() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${o.offerNumber} — ${o.applicantName}`} onClose={onClose} width={680}>
      <Alert type="info"><strong>{o.nextStep}</strong></Alert>

      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{money(o.offeredSalary, o.currencyCode)}</strong> · {o.positionTitle} · {o.employmentType}
          {o.contractEndDate && ` to ${fmtDate(o.contractEndDate)}`}
        </p>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>
          Starting {fmtDate(o.proposedStartDate)} · {o.probationMonths} month(s) probation · {o.vacancyNumber}
        </p>
        {o.salaryRangeNote && (
          <p style={{ margin: '4px 0 0', fontSize: 12, color: o.salaryRangeNote.startsWith('Within') ? T.mgrey : T.amber }}>
            {o.salaryRangeNote}
          </p>
        )}
        {o.terms && <p style={{ margin: '6px 0 0', fontSize: 12 }}>{o.terms}</p>}
        {o.approvedAt && (
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            Salary approved by {o.approvedByName} on {fmtDate(o.approvedAt)}
          </p>
        )}
        {o.rejectionReason && <p style={{ margin: '4px 0 0', fontSize: 12, color: T.red }}>Sent back — {o.rejectionReason}</p>}
        {o.issuedAt && (
          <p style={{ margin: '4px 0 0', fontSize: 12 }}>
            Issued {fmtDate(o.issuedAt)}, answer due {fmtDate(o.responseDeadline)}
            {o.responseOverdue && <strong style={{ color: T.red }}> — overdue</strong>}
          </p>
        )}
        {o.respondedAt && <p style={{ margin: '4px 0 0', fontSize: 12 }}>Answered {fmtDate(o.respondedAt)}</p>}
        {o.declineReason && <p style={{ margin: '4px 0 0', fontSize: 12, color: T.red }}>Declined — {o.declineReason}</p>}
        {o.withdrawalReason && <p style={{ margin: '4px 0 0', fontSize: 12, color: T.red }}>Withdrawn — {o.withdrawalReason}</p>}
      </Card>

      {o.status === 'Draft' && (
        <Btn onClick={() => act(() => hr.submitOffer(o.id), 'Could not submit.')} disabled={busy}>
          Send for salary approval
        </Btn>
      )}

      {o.status === 'PendingApproval' && (
        <>
          <p style={{ fontSize: 12, color: T.mgrey }}>
            Approval needs <code>hr.approve</code>, and whoever prepared this offer cannot approve it.
          </p>
          <Input label="Reason" value={f.reason ?? ''} onChange={v => setF({ ...f, reason: v })}
            note="Required to send it back" />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <Btn onClick={() => act(() => hr.decideOffer(o.id, { decision: 'Approve' }), 'Could not approve.')}
              disabled={busy}>Approve the salary</Btn>
            <Btn variant="outline" onClick={() => act(() => hr.decideOffer(o.id, { decision: 'Reject', reason: f.reason }), 'Could not send back.')}
              disabled={busy || !f.reason?.trim()}>Send back to draft</Btn>
          </div>
        </>
      )}

      {o.status === 'Approved' && (
        <Btn onClick={() => act(() => hr.issueOffer(o.id), 'Could not issue.')} disabled={busy}>
          Issue to the candidate
        </Btn>
      )}

      {o.status === 'Issued' && (
        <>
          <SectionHeader title="Record the candidate's answer" />
          <Input label="Agreed start date" type="date" value={f.startDate ?? ''}
            onChange={v => setF({ ...f, startDate: v })}
            note={`Leave blank to keep the proposed ${fmtDate(o.proposedStartDate)}`} />
          <Input label="Reason (required to decline)" value={f.reason ?? ''} onChange={v => setF({ ...f, reason: v })} />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <Btn onClick={() => act(() => hr.respondToOffer(o.id, {
              response: 'Accept', startDate: f.startDate ? `${f.startDate}T00:00:00Z` : null,
            }), 'Could not record.')} disabled={busy}>They accepted</Btn>
            <Btn variant="outline" onClick={() => act(() => hr.respondToOffer(o.id, { response: 'Decline', reason: f.reason }), 'Could not record.')}
              disabled={busy}>They declined</Btn>
          </div>

          <SectionHeader title="Withdraw the offer" />
          <Input label="Why" value={f.withdrawReason ?? ''} onChange={v => setF({ ...f, withdrawReason: v })} />
          <Btn variant="outline" onClick={() => act(() => hr.withdrawOffer(o.id, { reason: f.withdrawReason }), 'Could not withdraw.')}
            disabled={busy || !f.withdrawReason?.trim()}>Withdraw</Btn>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: 6 }}>
            Withdrawing takes back a promise, so it needs a reason. The candidate returns to recommended — the
            panel's verdict has not changed.
          </p>
        </>
      )}

      {o.status === 'Accepted' && !o.resultingEmployeeId && (
        <>
          <Alert type="warning">
            Accepted — but there is no employee record yet. Recording the hire creates one through the same path
            as any other onboarding, so this person gets the normal employee number, org-chart node and
            onboarding checks.
          </Alert>
          <Btn onClick={() => setHiring(true)} disabled={busy}>Record the hire</Btn>
        </>
      )}

      {o.status === 'Accepted' && o.resultingEmployeeId && (
        <Alert type="info">Hired — the employee record exists. Onboarding continues there.</Alert>
      )}

      {hiring && <HireModal offer={o} flash={flash} onClose={() => setHiring(false)}
        onSaved={() => { setHiring(false); detail.reload(); onSaved() }} />}
    </Modal>
  )
}

function HireModal({ offer, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({
    firstName: '', lastName: '', otherNames: '', workEmail: '', workPhone: '',
    dateOfBirth: '', gender: '', maritalStatus: '', physicalAddress: '',
    kraPin: '', nssfNumber: '', shaNumber: '', helbNumber: '',
    reportsToId: '', workMode: 'Office', hireDate: offer.proposedStartDate?.slice(0, 10) ?? today(),
    createUserAccount: false,
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.hireApplicant(offer.applicantId, {
        firstName: f.firstName || null, lastName: f.lastName || null, otherNames: f.otherNames || null,
        workEmail: f.workEmail || null, workPhone: f.workPhone || null,
        dateOfBirth: f.dateOfBirth ? `${f.dateOfBirth}T00:00:00Z` : null,
        gender: f.gender || null, maritalStatus: f.maritalStatus || null,
        physicalAddress: f.physicalAddress || null,
        kraPin: f.kraPin || null, nssfNumber: f.nssfNumber || null,
        shaNumber: f.shaNumber || null, helbNumber: f.helbNumber || null,
        reportsToId: f.reportsToId || null, workMode: f.workMode || null,
        hireDate: f.hireDate ? `${f.hireDate}T00:00:00Z` : null,
        createUserAccount: f.createUserAccount,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not record the hire.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Record the hire — ${offer.applicantName}`} onClose={onClose} width={680}>
      <Alert type="info">
        The salary, start date and employment type come from the accepted offer. What is asked for here is what an
        application never carried: the statutory numbers payroll will need and who they report to.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="First name" value={f.firstName} onChange={v => setF({ ...f, firstName: v })}
          note="Blank splits the applicant's name" />
        <Input label="Other names" value={f.otherNames} onChange={v => setF({ ...f, otherNames: v })} />
        <Input label="Last name" value={f.lastName} onChange={v => setF({ ...f, lastName: v })} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Work email" type="email" value={f.workEmail} onChange={v => setF({ ...f, workEmail: v })}
          note="Blank uses the address they applied from" />
        <Input label="Work phone" value={f.workPhone} onChange={v => setF({ ...f, workPhone: v })} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="Date of birth" type="date" value={f.dateOfBirth} onChange={v => setF({ ...f, dateOfBirth: v })} />
        <Select label="Gender" value={f.gender} onChange={v => setF({ ...f, gender: v })}
          options={[{ value: '', label: '—' }, { value: 'Male', label: 'Male' },
            { value: 'Female', label: 'Female' }, { value: 'Other', label: 'Other' }]} />
        <Select label="Marital status" value={f.maritalStatus} onChange={v => setF({ ...f, maritalStatus: v })}
          options={[{ value: '', label: '—' }, ...['Single', 'Married', 'Divorced', 'Widowed'].map(v => ({ value: v, label: v }))]} />
      </div>

      <Input label="Physical address" value={f.physicalAddress} onChange={v => setF({ ...f, physicalAddress: v })} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 10 }}>
        <Input label="KRA PIN" value={f.kraPin} onChange={v => setF({ ...f, kraPin: v })} />
        <Input label="NSSF no." value={f.nssfNumber} onChange={v => setF({ ...f, nssfNumber: v })} />
        <Input label="SHA no." value={f.shaNumber} onChange={v => setF({ ...f, shaNumber: v })} />
        <Input label="HELB no." value={f.helbNumber} onChange={v => setF({ ...f, helbNumber: v })} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Select label="Reports to" value={f.reportsToId} onChange={v => setF({ ...f, reportsToId: v })}
          options={[{ value: '', label: 'Not set yet' },
            ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} />
        <Select label="Work mode" value={f.workMode} onChange={v => setF({ ...f, workMode: v })}
          options={['Office', 'Field', 'Hybrid'].map(v => ({ value: v, label: v }))} />
        <Input label="Hire date" type="date" value={f.hireDate} onChange={v => setF({ ...f, hireDate: v })} />
      </div>

      <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, marginTop: 10 }}>
        <input type="checkbox" checked={f.createUserAccount}
          onChange={e => setF({ ...f, createUserAccount: e.target.checked })} />
        Create their login account now
      </label>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 4 }}>
        Normally left unticked: an account that exists before the signed contract is on file is an account nobody
        has agreed to. It can be created from the employee record at any time.
      </p>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Hiring…' : 'Create the employee record'}</Btn>
      </div>
    </Modal>
  )
}
