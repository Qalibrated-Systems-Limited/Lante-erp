import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H3 — leave types, entitlements, applications with their tiered approval chain,
// and the year-end carry-forward cycle. REAL, wired to hr-service
// (/api/v1/hr/leave/*).
//
// Two things drive everything on these screens and are worth knowing before
// reading the code:
//   • The balance is DERIVED (entitled − taken) and days sitting in an approval
//     chain are shown separately as "pending", because a new request is measured
//     against balance − pending. That is why the tables show three numbers.
//   • The approval chain comes from the leave type's flags, so it varies per
//     request (study leave over 14 days picks up a Board step). The UI renders
//     whatever chain the server built rather than assuming a shape.
// Per HR-DEC-8 these are the HR-admin and line-manager screens; employee
// self-service is a later pass, so HR files requests on an employee's behalf.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const num = (n) => (n === null || n === undefined ? '—' : Number(n).toLocaleString('en-KE', { maximumFractionDigits: 2 }))

const STATUS_VARIANT = { Pending: 'amber', Approved: 'green', Rejected: 'red', Cancelled: 'default' }
const ACTION_VARIANT = { Pending: 'amber', Approved: 'green', Rejected: 'red', Skipped: 'default' }
const CARRY_VARIANT = { Active: 'green', Expired: 'red', FullyUsed: 'default' }
const ROLE_LABEL = { LineManager: 'Line Manager', Hr: 'HR', Board: 'Board' }
const DOC_TYPES = ['MedicalCertificate', 'AntenatalBooking', 'BirthCertificate', 'InstitutionLetter', 'Other']

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// The chain the server built for this request, with each step's outcome.
function ChainStrip({ steps }) {
  if (!steps?.length) return <span style={{ fontSize: 11, color: T.mgrey }}>—</span>
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'center' }}>
      {steps.map((s, i) => (
        <span key={s.id ?? i} style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
          {i > 0 && <span style={{ color: T.mgrey, fontSize: 11 }}>→</span>}
          <Badge variant={ACTION_VARIANT[s.action] ?? 'default'}>
            {ROLE_LABEL[s.role] ?? s.role}
          </Badge>
        </span>
      ))}
    </div>
  )
}

function SweepButton({ flash, onDone }) {
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const r = await hr.runLeaveSweep()
      flash(r?.message ?? 'Leave sweep complete.')
      onDone()
    } catch (e) { flash(e.response?.data?.message ?? 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Running…' : 'Run leave sweep'}</Btn>
}

// ── Requests (P5) ─────────────────────────────────────────────────────────────
export function LeaveRequestsTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState({ status: 'Pending', awaitingRole: '' })
  const [newOpen, setNewOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    hr.listLeaveRequests({
      status: filter.status || undefined,
      awaitingRole: filter.awaitingRole || undefined,
    }).then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.leaveSummary().then(setSum).catch(() => {})
  }, [filter])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Pending" value={sum?.pendingRequests ?? 0} color={T.amber} />
        <Kpi label="With Line Manager" value={sum?.awaitingLineManager ?? 0} color={T.blue} />
        <Kpi label="With HR" value={sum?.awaitingHr ?? 0} color={T.blue} />
        <Kpi label="With Board" value={sum?.awaitingBoard ?? 0} color={T.purple} />
        <Kpi label="Missing Document" value={sum?.missingRequiredDocument ?? 0} color={T.red} sub="blocks approval" />
        <Kpi label="On Leave Today" value={sum?.onLeaveToday ?? 0} color={T.green} />
        <Kpi label="Approved Upcoming" value={sum?.approvedUpcoming ?? 0} color={T.green} />
        <Kpi label="Days Taken (YTD)" value={num(sum?.daysTakenThisYear ?? 0)} />
      </div>

      <SectionHeader
        title="Leave Requests"
        sub="The chain depends on the leave type: annual and sick need the Line Manager, maternity and compassionate add HR, and study leave over 14 days also needs the Board. Days are reserved while a request is in its chain."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ Apply for Leave</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={filter.status} onChange={e => setFilter(f => ({ ...f, status: e.target.value }))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {Object.keys(STATUS_VARIANT).map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={filter.awaitingRole} onChange={e => setFilter(f => ({ ...f, awaitingRole: e.target.value }))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">Any approver</option>
          {Object.entries(ROLE_LABEL).map(([k, v]) => <option key={k} value={k}>Awaiting {v}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Request', 'Employee', 'Type', 'Dates', 'Days', 'Chain', 'Status', 'Document', 'Actions']}
          empty={filter.status === 'Pending' ? 'No leave awaiting approval.' : 'No requests match this filter.'}
          rows={rows.map(r => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.requestNumber}</span>,
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{r.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{r.employeeName}</span>
            </span>,
            r.leaveTypeName ?? r.leaveTypeCode,
            <span style={{ fontSize: 12 }}>
              {fmtDate(r.startDate)} → {fmtDate(r.endDate)}
              {r.isCurrentlyOnLeave && <span style={{ color: T.green, fontWeight: 600 }}> · away now</span>}
            </span>,
            num(r.daysRequested),
            <ChainStrip steps={r.approvalChain} />,
            <span>
              <Badge variant={STATUS_VARIANT[r.status] ?? 'default'}>{r.status}</Badge>
              {r.status === 'Pending' && r.awaitingRole &&
                <span style={{ fontSize: 10, color: T.mgrey, display: 'block', marginTop: 2 }}>
                  at {ROLE_LABEL[r.awaitingRole] ?? r.awaitingRole}
                </span>}
            </span>,
            r.documentRequired
              ? (r.documentAttached
                  ? <span style={{ color: T.green, fontSize: 11 }}>attached</span>
                  : <span style={{ color: T.red, fontSize: 11, fontWeight: 600 }}>required</span>)
              : <span style={{ color: T.mgrey, fontSize: 11 }}>—</span>,
            <Btn size="sm" variant="outline" onClick={() => setDetailId(r.id)}>Open</Btn>,
          ])}
        />
      )}

      {newOpen && <ApplyModal onClose={() => setNewOpen(false)} onSaved={() => { setNewOpen(false); load() }} flash={flash} />}
      {detailId && <RequestDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function ApplyModal({ onClose, onSaved, flash }) {
  const [employees, setEmployees] = useState([])
  const [types, setTypes] = useState([])
  const [preview, setPreview] = useState(null)
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    employeeId: '', leaveTypeId: '', startDate: '', endDate: '', returnDate: '',
    reason: '', handoverNotes: '', coverEmployeeId: '',
  })

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
    hr.listLeaveTypes().then(t => setTypes(t ?? [])).catch(() => {})
  }, [])

  // The server owns the day count, the balance check and the chain — ask it rather than
  // reimplementing weekend rules and thresholds here and drifting out of step.
  useEffect(() => {
    if (!f.employeeId || !f.leaveTypeId || !f.startDate || !f.endDate) { setPreview(null); return }
    let live = true
    hr.previewLeave({ employeeId: f.employeeId, leaveTypeId: f.leaveTypeId, startDate: f.startDate, endDate: f.endDate })
      .then(p => { if (live) setPreview(p) })
      .catch(() => { if (live) setPreview(null) })
    return () => { live = false }
  }, [f.employeeId, f.leaveTypeId, f.startDate, f.endDate])

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.createLeaveRequest({
        employeeId: f.employeeId, leaveTypeId: f.leaveTypeId,
        startDate: f.startDate, endDate: f.endDate,
        returnDate: f.returnDate || undefined,
        reason: f.reason || undefined,
        handoverNotes: f.handoverNotes || undefined,
        coverEmployeeId: f.coverEmployeeId || undefined,
      })
      flash(r?.message ?? 'Leave requested.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not submit the request.') }
    finally { setBusy(false) }
  }

  const ready = f.employeeId && f.leaveTypeId && f.startDate && f.endDate
  return (
    <Modal title="Apply for Leave" onClose={onClose} width={620}>
      <Select label="Employee" required value={f.employeeId} onChange={v => setF(s => ({ ...s, employeeId: v }))}
        options={[{ value: '', label: 'Select employee…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} />

      <Select label="Leave type" required value={f.leaveTypeId} onChange={v => setF(s => ({ ...s, leaveTypeId: v }))}
        options={[{ value: '', label: 'Select type…' },
          ...types.map(t => ({ value: t.id, label: `${t.name}${t.daysAllowed > 0 ? ` (${num(t.daysAllowed)} days)` : ' (no fixed entitlement)'}` }))]} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
        <Input label="First day" type="date" required value={f.startDate} onChange={v => setF(s => ({ ...s, startDate: v }))} />
        <Input label="Last day" type="date" required value={f.endDate} onChange={v => setF(s => ({ ...s, endDate: v }))} />
        <Input label="Back at work" type="date" value={f.returnDate} onChange={v => setF(s => ({ ...s, returnDate: v }))} />
      </div>

      {preview && (
        <Card style={{ padding: 12, marginBottom: 14, background: T.offwt }}>
          <p style={{ margin: 0, fontSize: 13, fontWeight: 700, color: T.navy }}>
            {num(preview.daysRequested)} {preview.countsWorkingDaysOnly ? 'working' : 'calendar'} day(s)
            {preview.daysRequested > 0 && preview.countsWorkingDaysOnly && ' — weekends excluded'}
          </p>
          <p style={{ margin: '4px 0 0', fontSize: 12, color: preview.sufficientBalance ? T.mgrey : T.red }}>
            {num(preview.daysAvailable)} day(s) available
            {!preview.sufficientBalance && ' — not enough for this request'}
          </p>
          <p style={{ margin: '4px 0 0', fontSize: 12, color: T.mgrey }}>
            Approval: {preview.approvalChain?.join(' → ') || '—'}
          </p>
          {preview.documentRequired && (
            <p style={{ margin: '4px 0 0', fontSize: 12, color: T.amber }}>
              A {String(preview.requiredDocumentType ?? 'document').replace(/([A-Z])/g, ' $1').trim().toLowerCase()} must be
              attached before it can be approved — upload it from the request once submitted.
            </p>
          )}
          {preview.warning && <p style={{ margin: '4px 0 0', fontSize: 11, color: T.mgrey }}>{preview.warning}</p>}
        </Card>
      )}

      <Input label="Reason" value={f.reason} onChange={v => setF(s => ({ ...s, reason: v }))} placeholder="Optional…" />
      <Select label="Cover while away" value={f.coverEmployeeId} onChange={v => setF(s => ({ ...s, coverEmployeeId: v }))}
        options={[{ value: '', label: 'No cover named' },
          ...employees.filter(e => e.id !== f.employeeId).map(e => ({ value: e.id, label: e.fullName }))]} />
      <Input label="Handover notes" value={f.handoverNotes} onChange={v => setF(s => ({ ...s, handoverNotes: v }))}
        placeholder="What the cover needs to pick up…" />

      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 6 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !ready || (preview && !preview.sufficientBalance)}>
          {busy ? 'Submitting…' : 'Submit request'}
        </Btn>
      </div>
    </Modal>
  )
}

function RequestDetailModal({ id, onClose, onChanged, flash }) {
  const [r, setR] = useState(null)
  const [busy, setBusy] = useState(false)
  const [comments, setComments] = useState('')
  const [docUrl, setDocUrl] = useState('')
  const [docType, setDocType] = useState('')

  const load = useCallback(() => { hr.getLeaveRequest(id).then(setR).catch(() => setR(null)) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, done) => {
    setBusy(true)
    try {
      const res = await fn()
      flash(res?.message ?? 'Done.')
      if (done) { onChanged(); onClose() } else { load(); onChanged() }
    } catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!r) return <Modal title="Leave request" onClose={onClose}><Loading /></Modal>

  const pendingStep = r.approvalChain?.find(s => s.step === r.currentStep)
  return (
    <Modal title={`${r.requestNumber} — ${r.employeeName}`} onClose={onClose} width={640}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 16 }}>
        <Field label="Leave type" value={r.leaveTypeName} />
        <Field label="Days" value={`${num(r.daysRequested)}`} />
        <Field label="From" value={fmtDate(r.startDate)} />
        <Field label="To" value={fmtDate(r.endDate)} />
        <Field label="Back at work" value={fmtDate(r.returnDate)} />
        <Field label="Status" value={r.status} />
        <Field label="Cover" value={r.coverEmployeeName ?? '—'} />
        <Field label="Submitted" value={fmtDate(r.submittedAt)} />
      </div>

      {r.reason && <Field label="Reason" value={r.reason} />}
      {r.handoverNotes && <Field label="Handover" value={r.handoverNotes} />}
      {r.rejectionReason && <Field label="Rejected because" value={r.rejectionReason} />}
      {r.cancellationReason && <Field label="Cancelled because" value={r.cancellationReason} />}

      <p style={{ fontSize: 12, fontWeight: 700, color: T.navy, marginTop: 16, marginBottom: 6 }}>Approval chain</p>
      <DataTable
        headers={['Step', 'Approver role', 'Outcome', 'By', 'When', 'Comments']}
        empty="No chain recorded."
        rows={(r.approvalChain ?? []).map(s => [
          s.step,
          ROLE_LABEL[s.role] ?? s.role,
          <Badge variant={ACTION_VARIANT[s.action] ?? 'default'}>{s.action}</Badge>,
          s.approverName ?? '—',
          fmtDate(s.actionedAt),
          <span style={{ fontSize: 11 }}>{s.comments ?? '—'}</span>,
        ])}
      />

      {r.documentRequired && (
        <div style={{ marginTop: 16, padding: 12, background: T.offwt, borderRadius: 8 }}>
          <p style={{ margin: 0, fontSize: 12, fontWeight: 700, color: r.documentAttached ? T.green : T.red }}>
            {r.documentAttached
              ? 'Required document attached.'
              : `A ${String(r.requiredDocumentType ?? 'document').replace(/([A-Z])/g, ' $1').trim().toLowerCase()} is required before approval.`}
          </p>
          {!r.documentAttached && r.status === 'Pending' && (
            <>
              <Input label="File URL" value={docUrl} onChange={setDocUrl} placeholder="https://…" />
              <Select label="Document type" value={docType || (r.requiredDocumentType ?? '')} onChange={setDocType}
                options={DOC_TYPES.map(d => ({ value: d, label: d.replace(/([A-Z])/g, ' $1').trim() }))} />
              <Btn size="sm" disabled={busy || !docUrl}
                onClick={() => run(() => hr.attachLeaveDocument(r.id, {
                  fileUrl: docUrl, documentType: docType || r.requiredDocumentType,
                }))}>Attach document</Btn>
            </>
          )}
        </div>
      )}

      {r.status === 'Pending' && (
        <div style={{ marginTop: 18 }}>
          <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 6 }}>
            Recording the decision for step {r.currentStep} of {r.totalSteps}
            {pendingStep && ` (${ROLE_LABEL[pendingStep.role] ?? pendingStep.role})`}.
            {r.totalSteps > r.currentStep && ' Approving passes it to the next tier rather than granting the leave.'}
          </p>
          <Input label="Comments" value={comments} onChange={setComments}
            placeholder="Required when rejecting…" />
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <Btn size="sm" variant="green" disabled={busy}
              onClick={() => run(() => hr.decideLeave(r.id, { decision: 'Approve', comments: comments || undefined }))}>
              Approve
            </Btn>
            <Btn size="sm" variant="danger" disabled={busy || !comments}
              onClick={() => run(() => hr.decideLeave(r.id, { decision: 'Reject', comments }), true)}>
              Reject
            </Btn>
            <Btn size="sm" variant="ghost" disabled={busy}
              onClick={() => run(() => hr.cancelLeave(r.id, { reason: comments || 'Withdrawn.' }), true)}>
              Cancel request
            </Btn>
          </div>
        </div>
      )}

      {r.status === 'Approved' && new Date(r.endDate) >= new Date() && (
        <div style={{ marginTop: 18 }}>
          <Input label="Cancellation reason" value={comments} onChange={setComments} placeholder="Why it is being withdrawn…" />
          <Btn size="sm" variant="ghost" disabled={busy}
            onClick={() => run(() => hr.cancelLeave(r.id, { reason: comments || 'Withdrawn.' }), true)}>
            Cancel approved leave (returns the days)
          </Btn>
        </div>
      )}
    </Modal>
  )
}

function Field({ label, value }) {
  return (
    <div style={{ marginBottom: 8 }}>
      <p style={{ fontSize: 10, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, margin: 0 }}>{label}</p>
      <p style={{ fontSize: 13, color: T.dgrey, margin: '2px 0 0' }}>{value ?? '—'}</p>
    </div>
  )
}

// ── Entitlements (P4 step 4.2) ───────────────────────────────────────────────
export function LeaveEntitlementsTab({ flash }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [year, setYear] = useState(new Date().getFullYear())
  const [typeFilter, setTypeFilter] = useState('')
  const [types, setTypes] = useState([])
  const [adjusting, setAdjusting] = useState(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    setLoading(true)
    hr.listEntitlements({ year, leaveTypeId: typeFilter || undefined })
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [year, typeFilter])
  useEffect(() => { load() }, [load])
  useEffect(() => { hr.listLeaveTypes().then(t => setTypes(t ?? [])).catch(() => {}) }, [])

  const assign = async () => {
    setBusy(true)
    try {
      const r = await hr.assignEntitlements(year)
      flash(r?.message ?? 'Assigned.')
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not assign entitlements.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      <SectionHeader
        title="Leave Entitlements"
        sub="Each employee's allowance per leave type per year. A mid-year joiner's first year is pro-rated by months served. Balance is entitled minus taken; days sitting in an approval chain show as pending and are not available to spend twice."
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn size="sm" variant="outline" onClick={assign} disabled={busy}>
            {busy ? 'Assigning…' : `Assign ${year} entitlements`}
          </Btn>
        </div>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <input type="number" value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ width: 110, height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
        <select value={typeFilter} onChange={e => setTypeFilter(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All leave types</option>
          {types.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Type', 'Entitled', 'Taken', 'Pending', 'Balance', 'Available', 'Carried In', 'Forfeited', 'Actions']}
          empty={`No entitlements for ${year} — assign them.`}
          rows={rows.map(e => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{e.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{e.employeeName}</span>
            </span>,
            <span>
              {e.leaveTypeName ?? e.leaveTypeCode}
              {e.wasProRated && <span style={{ fontSize: 10, color: T.blue, display: 'block' }}>pro-rated</span>}
            </span>,
            num(e.daysEntitled),
            num(e.daysTaken),
            e.daysPending > 0 ? <span style={{ color: T.amber, fontWeight: 600 }}>{num(e.daysPending)}</span> : '—',
            <strong>{num(e.daysBalance)}</strong>,
            <span style={{ color: e.daysAvailable <= 0 ? T.red : T.green, fontWeight: 600 }}>{num(e.daysAvailable)}</span>,
            e.carriedForwardDays > 0 ? num(e.carriedForwardDays) : '—',
            e.forfeitedDays > 0 ? <span style={{ color: T.red }}>{num(e.forfeitedDays)}</span> : '—',
            <Btn size="sm" variant="outline" onClick={() => setAdjusting(e)}>Adjust</Btn>,
          ])}
        />
      )}

      {adjusting && (
        <AdjustModal row={adjusting} flash={flash}
          onClose={() => setAdjusting(null)}
          onSaved={() => { setAdjusting(null); load() }} />
      )}
    </div>
  )
}

function AdjustModal({ row, onClose, onSaved, flash }) {
  const [days, setDays] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.adjustEntitlement(row.id, { days: Number(days), reason })
      flash(r?.message ?? 'Adjusted.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not adjust.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Adjust ${row.leaveTypeName} — ${row.employeeName}`} onClose={onClose} width={480}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        {row.year}: entitled {num(row.daysEntitled)}, taken {num(row.daysTaken)}, balance {num(row.daysBalance)}.
        A negative adjustment cannot take the allowance below the days already taken.
      </p>
      <Input label="Days (negative to claw back)" type="number" required value={days} onChange={setDays} placeholder="e.g. 3 or -2" />
      <Input label="Reason" required value={reason} onChange={setReason} placeholder="Why the allowance is changing…" />
      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !days || !reason}>{busy ? 'Saving…' : 'Apply adjustment'}</Btn>
      </div>
    </Modal>
  )
}

// ── Leave types (P4 step 4.1) ────────────────────────────────────────────────
export function LeaveTypesTab({ flash }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState(null)   // row | 'new'
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    setLoading(true)
    hr.listLeaveTypes(true).then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])

  const seed = async () => {
    setBusy(true)
    try {
      const r = await hr.seedLeaveTypes()
      flash(r?.message ?? 'Seeded.')
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not install the defaults.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      <SectionHeader
        title="Leave Types"
        sub="The rules per kind of leave. The approval chain is derived from these flags rather than the name, so a request picks up HR or the Board because of what the type requires and how long the request is."
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn size="sm" variant="outline" onClick={seed} disabled={busy}>Install Lante defaults</Btn>
          <Btn size="sm" onClick={() => setEditing('new')}>+ New type</Btn>
        </div>}
      />

      {loading ? <Loading /> : (
        <DataTable
          headers={['Code', 'Name', 'Days', 'Counting', 'Carry-forward', 'Document', 'Approval chain', 'Pay', 'Status', 'Actions']}
          empty="No leave types yet — install the Lante defaults."
          rows={rows.map(t => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{t.code}</span>,
            t.name,
            t.daysAllowed > 0 ? num(t.daysAllowed) : <span style={{ fontSize: 11, color: T.mgrey }}>no fixed</span>,
            <span style={{ fontSize: 11 }}>{t.countsWorkingDaysOnly ? 'working days' : 'calendar days'}</span>,
            t.carriesForward
              ? <span style={{ fontSize: 11 }}>max {num(t.maxCarryForwardDays)}, expires 31 Mar</span>
              : <span style={{ fontSize: 11, color: T.mgrey }}>no</span>,
            t.requiresDocumentAfterDays !== null && t.requiresDocumentAfterDays !== undefined
              ? <span style={{ fontSize: 11 }}>
                  {String(t.documentTypeRequired ?? '').replace(/([A-Z])/g, ' $1').trim()}
                  {t.requiresDocumentAfterDays > 0 ? ` over ${t.requiresDocumentAfterDays}d` : ' always'}
                </span>
              : <span style={{ fontSize: 11, color: T.mgrey }}>—</span>,
            <span style={{ fontSize: 11 }}>{t.approvalChain}</span>,
            t.isPaid
              ? (t.fullPayDays ? <span style={{ fontSize: 11 }}>{num(t.fullPayDays)} full then half</span> : <span style={{ fontSize: 11 }}>full</span>)
              : <Badge variant="red">unpaid</Badge>,
            <Badge variant={t.isActive ? 'green' : 'default'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>,
            <Btn size="sm" variant="outline" onClick={() => setEditing(t)}>Edit</Btn>,
          ])}
        />
      )}

      {editing && (
        <TypeModal row={editing === 'new' ? null : editing} flash={flash}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); load() }} />
      )}
    </div>
  )
}

function TypeModal({ row, onClose, onSaved, flash }) {
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    code: row?.code ?? '', name: row?.name ?? '', description: row?.description ?? '',
    daysAllowed: row?.daysAllowed ?? 0, fullPayDays: row?.fullPayDays ?? '',
    isPaid: row?.isPaid ?? true,
    carriesForward: row?.carriesForward ?? false, maxCarryForwardDays: row?.maxCarryForwardDays ?? 0,
    requiresDocumentAfterDays: row?.requiresDocumentAfterDays ?? '',
    documentTypeRequired: row?.documentTypeRequired ?? '',
    requiresHrApproval: row?.requiresHrApproval ?? false,
    requiresBoardApproval: row?.requiresBoardApproval ?? false,
    boardApprovalAfterDays: row?.boardApprovalAfterDays ?? 14,
    countsWorkingDaysOnly: row?.countsWorkingDaysOnly ?? true,
    proRateFirstYear: row?.proRateFirstYear ?? false,
    isActive: row?.isActive ?? true,
    displayOrder: row?.displayOrder ?? 0,
  })
  const set = (k) => (v) => setF(s => ({ ...s, [k]: v }))

  const save = async () => {
    setBusy(true)
    const dto = {
      ...f,
      daysAllowed: Number(f.daysAllowed) || 0,
      fullPayDays: f.fullPayDays === '' ? null : Number(f.fullPayDays),
      maxCarryForwardDays: Number(f.maxCarryForwardDays) || 0,
      requiresDocumentAfterDays: f.requiresDocumentAfterDays === '' ? null : Number(f.requiresDocumentAfterDays),
      documentTypeRequired: f.documentTypeRequired || null,
      boardApprovalAfterDays: Number(f.boardApprovalAfterDays) || 0,
      displayOrder: Number(f.displayOrder) || 0,
    }
    try {
      const r = row ? await hr.updateLeaveType(row.id, dto) : await hr.createLeaveType(dto)
      flash(r?.message ?? 'Saved.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not save the leave type.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={row ? `Edit ${row.name}` : 'New leave type'} onClose={onClose} width={620}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 12 }}>
        <Input label="Code" required readOnly={!!row} value={f.code} onChange={set('code')} placeholder="ANNUAL"
          note={row ? 'Fixed once created.' : 'Stable key — cannot change later.'} />
        <Input label="Name" required value={f.name} onChange={set('name')} />
      </div>
      <Input label="Description" value={f.description} onChange={set('description')} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
        <Input label="Days allowed" type="number" value={f.daysAllowed} onChange={set('daysAllowed')}
          note="0 = granted on merit" />
        <Input label="Full-pay days" type="number" value={f.fullPayDays} onChange={set('fullPayDays')}
          note="Rest at half pay" />
        <Input label="Display order" type="number" value={f.displayOrder} onChange={set('displayOrder')} />
      </div>

      <Select label="Day counting" value={String(f.countsWorkingDaysOnly)} onChange={v => set('countsWorkingDaysOnly')(v === 'true')}
        options={[{ value: 'true', label: 'Working days (weekends excluded)' }, { value: 'false', label: 'Calendar days' }]} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Select label="Carries forward" value={String(f.carriesForward)} onChange={v => set('carriesForward')(v === 'true')}
          options={[{ value: 'false', label: 'No' }, { value: 'true', label: 'Yes — expires 31 March' }]} />
        <Input label="Carry-forward cap (days)" type="number" value={f.maxCarryForwardDays} onChange={set('maxCarryForwardDays')} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Document required over (days)" type="number" value={f.requiresDocumentAfterDays}
          onChange={set('requiresDocumentAfterDays')} note="blank = never, 0 = always" />
        <Select label="Document type" value={f.documentTypeRequired} onChange={set('documentTypeRequired')}
          options={[{ value: '', label: 'None' }, ...DOC_TYPES.map(d => ({ value: d, label: d.replace(/([A-Z])/g, ' $1').trim() }))]} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
        <Select label="HR approval" value={String(f.requiresHrApproval)} onChange={v => set('requiresHrApproval')(v === 'true')}
          options={[{ value: 'false', label: 'Not needed' }, { value: 'true', label: 'Required' }]} />
        <Select label="Board approval" value={String(f.requiresBoardApproval)} onChange={v => set('requiresBoardApproval')(v === 'true')}
          options={[{ value: 'false', label: 'Not needed' }, { value: 'true', label: 'Over a threshold' }]} />
        <Input label="Board threshold (days)" type="number" value={f.boardApprovalAfterDays} onChange={set('boardApprovalAfterDays')} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
        <Select label="Paid" value={String(f.isPaid)} onChange={v => set('isPaid')(v === 'true')}
          options={[{ value: 'true', label: 'Paid' }, { value: 'false', label: 'Unpaid' }]} />
        <Select label="Pro-rate first year" value={String(f.proRateFirstYear)} onChange={v => set('proRateFirstYear')(v === 'true')}
          options={[{ value: 'false', label: 'No' }, { value: 'true', label: 'By months served' }]} />
        <Select label="Status" value={String(f.isActive)} onChange={v => set('isActive')(v === 'true')}
          options={[{ value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }]} />
      </div>

      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 6 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code || !f.name}>{busy ? 'Saving…' : 'Save type'}</Btn>
      </div>
    </Modal>
  )
}

// ── Carry-forward (P4 steps 4.3–4.5 / P6) ───────────────────────────────────
export function CarryForwardTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [status, setStatus] = useState('')

  const load = useCallback(() => {
    setLoading(true)
    hr.listCarryForward({ status: status || undefined })
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.leaveSummary().then(setSum).catch(() => {})
  }, [status])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Active Carry-Forward" value={sum?.carryForwardActive ?? 0} color={T.green} />
        <Kpi label="Days Still Carried" value={num(sum?.daysCarriedActive ?? 0)} color={T.green} />
        <Kpi label="Expiring ≤30 Days" value={sum?.carryForwardExpiringIn30Days ?? 0} color={T.amber} sub="use or lose" />
      </div>

      <SectionHeader
        title="Leave Carry-Forward"
        sub="Only annual leave carries. At year end a balance above the cap is capped and the excess forfeited; carried days must be used by 31 March or they expire. A zero-day record is still written so every employee's year end is accounted for."
        action={<SweepButton flash={flash} onDone={load} />}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {Object.keys(CARRY_VARIANT).map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Type', 'Year', 'Carried', 'Forfeited', 'Expired', 'Expires', 'Status', 'Note']}
          empty="No carry-forward records — they are written by the year-end sweep."
          rows={rows.map(c => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{c.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{c.employeeName}</span>
            </span>,
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{c.leaveTypeCode}</span>,
            `${c.fromYear} → ${c.toYear}`,
            <strong>{num(c.daysCarried)}</strong>,
            c.daysForfeited > 0 ? <span style={{ color: T.red }}>{num(c.daysForfeited)}</span> : '—',
            c.daysExpired > 0 ? <span style={{ color: T.red }}>{num(c.daysExpired)}</span> : '—',
            <span style={{ fontSize: 12 }}>
              {fmtDate(c.expiryDate)}
              {c.status === 'Active' && c.daysToExpiry >= 0 &&
                <span style={{ color: c.daysToExpiry <= 30 ? T.amber : T.mgrey, display: 'block', fontSize: 10 }}>
                  in {c.daysToExpiry}d
                </span>}
            </span>,
            <Badge variant={CARRY_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{c.notes ?? '—'}</span>,
          ])}
        />
      )}
    </div>
  )
}
