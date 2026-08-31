import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H6 — the monthly payroll run (P9), overtime pre-approval (P12), payslips
// (P10) and bank payment files (P11). REAL, wired to hr-service.
//
// The screens deliberately show the SHAPE of the control, not just the buttons:
//   • a run is Computed → Approved, and the person who computed it cannot
//     approve it. The UI says so before anyone tries.
//   • recomputing an unapproved run is safe and expected — it releases the
//     overtime and unpaid days it had claimed and rebuilds from scratch.
//   • the journal can be inspected BEFORE approval commits it, because
//     approval is the point of no return.
//   • a bank file names everyone it leaves out. A short payment file that
//     looks complete is how somebody goes unpaid for a month.
//
// Payroll is permission-gated separately from the rest of HR, so a 403 is a
// normal state here and gets an explicit panel rather than an empty table.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)

const RUN_VARIANT = { Draft: 'default', Computed: 'amber', Approved: 'green', Cancelled: 'red' }
const OT_VARIANT = { Pending: 'amber', Approved: 'green', Rejected: 'red', Paid: 'blue' }
const BANK_FORMATS = ['Generic', 'Kcb', 'Equity', 'Ncba', 'CoOp']

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
      <p style={{ fontWeight: 700, color: T.navy, marginTop: 10 }}>Payroll data is restricted</p>
      <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6, maxWidth: 460, marginInline: 'auto' }}>
        Payroll runs and payslips need the payroll permissions (<code>hr.payroll.read</code> and above),
        which are held separately from general HR access.
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

function usePayrollData(loader, deps = []) {
  const [state, setState] = useState({ loading: true, denied: false, data: null })
  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    loader()
      .then(data => setState({ loading: false, denied: false, data }))
      .catch(e => setState({ loading: false, denied: isForbidden(e), data: null }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
  useEffect(() => { load() }, [load])
  return { ...state, reload: load }
}

// ═════════════════════════════════════════════════════════════════════════════
// Overtime (P12)
// ═════════════════════════════════════════════════════════════════════════════
export function OvertimeTab({ flash }) {
  const [status, setStatus] = useState('Pending')
  const [asking, setAsking] = useState(false)
  const rows = usePayrollData(() => hr.listOvertime({ status: status || undefined }), [status])

  const decide = async (o, decision) => {
    const reason = decision === 'Reject' ? window.prompt(`Reject ${o.hours} h for ${o.employeeName}? Give a reason:`) : null
    if (decision === 'Reject' && reason === null) return
    try { relay(flash, await hr.decideOvertime(o.id, { decision, reason })); rows.reload() }
    catch (e) { relayError(flash, e, 'Could not record the decision.') }
  }

  if (rows.loading) return <Loading />

  return (
    <div>
      <Alert type="info">
        <strong>Overtime is approved before it is worked</strong> (ATT-007) — a claim for a day already past is
        refused, because the point of pre-approval is that the manager can still decide the cost is not worth it.
        The rate follows the calendar, not the request: 1.5× on a working day, 2× on a rest day or gazetted holiday.
      </Alert>

      <SectionHeader
        title="Overtime Requests"
        sub="Approved hours are picked up automatically by the next payroll run — there is no manual entry at run time."
        action={<Btn size="sm" onClick={() => setAsking(true)}>+ Request overtime</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Pending', 'Approved', 'Rejected', 'Paid'].map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Employee', 'Date', 'Hours', 'Rate', 'Reason', 'Status', 'Paid', 'Actions']}
        empty={status === 'Pending' ? 'No overtime is awaiting approval.' : 'No overtime matches this filter.'}
        rows={(rows.data ?? []).map(o => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{o.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{o.employeeName}</span>
          </span>,
          fmtDate(o.date),
          `${o.hours}`,
          <Badge variant={o.multiplier >= 2 ? 'amber' : 'default'}>{o.multiplier}×</Badge>,
          <span style={{ fontSize: 12, color: T.mgrey }}>{o.reason ?? '—'}</span>,
          <Badge variant={OT_VARIANT[o.status] ?? 'default'}>{o.status}</Badge>,
          o.paidAmount == null ? '—' : money(o.paidAmount),
          o.status === 'Pending'
            ? <div style={{ display: 'flex', gap: 6 }}>
                <Btn size="sm" onClick={() => decide(o, 'Approve')}>Approve</Btn>
                <Btn size="sm" variant="ghost" onClick={() => decide(o, 'Reject')}>Reject</Btn>
              </div>
            : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
        ])}
      />

      {asking && <RequestOvertimeModal flash={flash}
        onClose={() => setAsking(false)} onSaved={() => { setAsking(false); rows.reload() }} />}
    </div>
  )
}

function RequestOvertimeModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [f, setF] = useState({ employeeId: '', date: '', hours: '', reason: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.requestOvertime({
        employeeId: f.employeeId, date: `${f.date}T00:00:00Z`,
        hours: Number(f.hours), reason: f.reason || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not raise the request.') }
    finally { setBusy(false) }
  }

  const today = new Date().toISOString().slice(0, 10)
  return (
    <Modal title="Request overtime" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Input label="Date" type="date" value={f.date} onChange={v => setF({ ...f, date: v })} required
        note="Must be today or later — overtime cannot be claimed after the fact." />
      <Input label="Hours" type="number" value={f.hours} onChange={v => setF({ ...f, hours: v })} required />
      <Input label="Reason" value={f.reason} onChange={v => setF({ ...f, reason: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        The multiplier is worked out from the date against the holiday calendar — you do not choose it.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.date || !f.hours || f.date < today}>
          {busy ? 'Sending…' : 'Request'}
        </Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Payroll runs (P9) — list, then the detail view where the work happens
// ═════════════════════════════════════════════════════════════════════════════
export function PayrollRunsTab({ flash }) {
  const [openRunId, setOpenRunId] = useState(null)
  const [creating, setCreating] = useState(false)
  const runs = usePayrollData(() => hr.listPayrollRuns(), [])

  if (runs.loading) return <Loading />
  if (runs.denied) return <NoAccess />

  if (openRunId) return <RunDetail runId={openRunId} flash={flash}
    onBack={() => { setOpenRunId(null); runs.reload() }} />

  return (
    <div>
      <SectionHeader
        title="Payroll Runs"
        sub="One live run per period. Compute builds every payslip from the salary assignments, rate tables, approved overtime and unpaid days; approval posts the journal to finance."
        action={<Btn size="sm" onClick={() => setCreating(true)}>+ New run</Btn>}
      />

      <DataTable
        headers={['Run', 'Period', 'Staff', 'Gross', 'Deductions', 'Net', 'Status', 'Journal', '']}
        empty="No payroll runs yet."
        rows={(runs.data ?? []).map(r => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{r.runNumber}</span>,
          `${fmtDate(r.periodStart)} – ${fmtDate(r.periodEnd)}`,
          r.employeeCount,
          money(r.totalGross, r.currencyCode),
          money(r.totalDeductions, r.currencyCode),
          <strong>{money(r.totalNet, r.currencyCode)}</strong>,
          <Badge variant={RUN_VARIANT[r.status] ?? 'default'}>{r.status}</Badge>,
          r.journalEntryNo
            ? <span style={{ fontSize: 12 }}>{r.journalEntryNo}</span>
            : r.journalError
              ? <Badge variant="red">not posted</Badge>
              : <span style={{ color: T.mgrey }}>—</span>,
          <Btn size="sm" variant="outline" onClick={() => setOpenRunId(r.id)}>Open</Btn>,
        ])}
      />

      {creating && <CreateRunModal flash={flash}
        onClose={() => setCreating(false)} onSaved={id => { setCreating(false); runs.reload(); setOpenRunId(id) }} />}
    </div>
  )
}

function CreateRunModal({ flash, onClose, onSaved }) {
  const [periods, setPeriods] = useState([])
  const [f, setF] = useState({ payrollPeriodId: '', notes: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listPayrollPeriods(new Date().getFullYear()).then(p => setPeriods(p ?? [])).catch(() => {})
  }, [])

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.createPayrollRun({ payrollPeriodId: f.payrollPeriodId || null, notes: f.notes || null })
      relay(flash, r)
      onSaved(r?.id)
    } catch (e) { relayError(flash, e, 'Could not open the run.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="New payroll run" onClose={onClose}>
      <Select label="Period" value={f.payrollPeriodId} onChange={v => setF({ ...f, payrollPeriodId: v })}
        options={[{ value: '', label: 'Current period' },
          ...periods.filter(p => p.status !== 'Closed').map(p => ({ value: p.id, label: `${p.code} (${p.status})` }))]} />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Only one live run may exist per period. Computing a run locks the period so salary and deduction changes
        cannot shift the figures under an approval.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Opening…' : 'Open run'}</Btn>
      </div>
    </Modal>
  )
}

// ── Run detail ───────────────────────────────────────────────────────────────
function RunDetail({ runId, flash, onBack }) {
  const [busy, setBusy] = useState('')
  const [journal, setJournal] = useState(null)
  const [bankOpen, setBankOpen] = useState(false)
  const [confirming, setConfirming] = useState(null)

  const run = usePayrollData(() => hr.getPayrollRun(runId), [runId])
  const files = usePayrollData(() => hr.listBankFiles(runId), [runId])

  if (run.loading) return <Loading />
  if (run.denied) return <NoAccess />
  const r = run.data
  if (!r) return <Alert type="error">That payroll run no longer exists.</Alert>

  const reload = () => { run.reload(); files.reload() }

  const act = async (key, fn, fallback) => {
    setBusy(key)
    try { relay(flash, await fn()); reload() }
    catch (e) { relayError(flash, e, fallback) }
    finally { setBusy('') }
  }

  const showJournal = async () => {
    try { setJournal(await hr.previewPayrollJournal(runId)) }
    catch (e) { relayError(flash, e, 'Could not build the journal preview.') }
  }

  // Approval is refused when the run used statutory rates nobody has verified, and the refusal carries
  // code 'UnconfirmedRates'. We branch on the CODE, not the message, so rewording the refusal cannot
  // silently turn this back into a dead end. The acknowledgement is recorded against the approver's name,
  // so the dialog says exactly that rather than being a bare "are you sure?".
  const approve = async () => {
    setBusy('approve')
    try {
      relay(flash, await hr.decidePayrollRun(runId, { decision: 'Approve' }))
      reload()
    } catch (e) {
      if (e.response?.data?.code !== 'UnconfirmedRates') {
        relayError(flash, e, 'Approval failed.')
        return
      }
      const detail = e.response?.data?.message ?? ''
      const ok = window.confirm(
        `${detail}\n\nApprove anyway? This is recorded against your name as accepting unverified statutory figures.`)
      if (!ok) return
      try {
        relay(flash, await hr.decidePayrollRun(runId, { decision: 'Approve', acknowledgeUnconfirmedRates: true }))
        reload()
      } catch (e2) { relayError(flash, e2, 'Approval failed.') }
    } finally { setBusy('') }
  }

  const cancel = () => {
    const reason = window.prompt(`Cancel ${r.runNumber}? Every payslip is discarded and all overtime released. Give a reason:`)
    if (reason === null) return
    act('cancel', () => hr.decidePayrollRun(runId, { decision: 'Cancel', reason }), 'Could not cancel the run.')
  }

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14, flexWrap: 'wrap' }}>
        <Btn size="sm" variant="ghost" onClick={onBack}>← All runs</Btn>
        <span style={{ fontFamily: 'monospace', fontWeight: 800, fontSize: 17, color: T.navy }}>{r.runNumber}</span>
        <Badge variant={RUN_VARIANT[r.status] ?? 'default'}>{r.status}</Badge>
        {r.journalEntryNo && <Badge variant="green">{r.journalEntryNo}</Badge>}
      </div>

      {r.journalError && (
        <Alert type="error">
          <strong>The journal has not reached finance.</strong> {r.journalError} The run itself stands — staff are
          still owed exactly what the payslips say. Fix the cause, then post the journal again.
        </Alert>
      )}
      {r.exclusions?.length > 0 && (
        <Alert type="warning">
          <strong>{r.exclusions.length} employee(s) are not in this run:</strong> {r.exclusions.join('; ')}
        </Alert>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Employees" value={r.employeeCount} />
        <Kpi label="Gross" value={money(r.totalGross, r.currencyCode)} />
        <Kpi label="PAYE" value={money(r.totalPaye, r.currencyCode)} color={T.amber} />
        <Kpi label="Statutory" value={money(r.totalStatutory, r.currencyCode)} />
        <Kpi label="Other deductions" value={money(r.totalOtherDeductions, r.currencyCode)} />
        <Kpi label="Net pay" value={money(r.totalNet, r.currencyCode)} color={T.green} />
        <Kpi label="Employer cost" value={money(r.totalEmployerCost, r.currencyCode)} sub="not deducted from staff" />
      </div>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 18 }}>
        {r.status !== 'Approved' && r.status !== 'Cancelled' && (
          <Btn size="sm" onClick={() => act('compute', () => hr.computePayrollRun(runId), 'Compute failed.')} disabled={!!busy}>
            {busy === 'compute' ? 'Computing…' : r.status === 'Computed' ? 'Recompute' : 'Compute payslips'}
          </Btn>
        )}
        <Btn size="sm" variant="outline" onClick={showJournal}>Preview journal</Btn>
        {r.status === 'Computed' && (
          <>
            <Btn size="sm" variant="green" onClick={approve}
              disabled={!!busy}>{busy === 'approve' ? 'Approving…' : 'Approve run'}</Btn>
            <Btn size="sm" variant="ghost" onClick={cancel} disabled={!!busy}>Cancel run</Btn>
          </>
        )}
        {r.status === 'Approved' && !r.journalPostedAt && (
          <Btn size="sm" onClick={() => act('post', () => hr.retryPayrollJournal(runId), 'Posting failed.')} disabled={!!busy}>
            {busy === 'post' ? 'Posting…' : 'Post journal again'}
          </Btn>
        )}
        {r.status === 'Approved' && <Btn size="sm" variant="outline" onClick={() => setBankOpen(true)}>Generate bank file</Btn>}
      </div>

      {r.status === 'Computed' && (
        <Alert type="info">
          Recomputing is safe — it releases the overtime and unpaid days this run had claimed and rebuilds from
          the current configuration. <strong>Approval needs a second officer:</strong> whoever computed the run
          cannot be the one who approves it.
        </Alert>
      )}

      <SectionHeader title="Payslips" sub="Every figure is derived from the inputs — nothing here is typed in." />
      <DataTable
        headers={['Employee', 'Basic', 'Gross', 'Taxable', 'PAYE', 'Statutory', 'Other', 'Net', '']}
        empty="No payslips — compute the run."
        rows={(r.payslips ?? []).map(p => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{p.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{p.employeeName}</span>
            {p.unpaidDays > 0 && <span style={{ display: 'block', fontSize: 11, color: T.amber }}>{p.unpaidDays} unpaid day(s)</span>}
            {p.overtimeHours > 0 && <span style={{ display: 'block', fontSize: 11, color: T.blue }}>{p.overtimeHours} OT hour(s)</span>}
          </span>,
          money(p.basicSalary, p.currencyCode),
          money(p.grossPay, p.currencyCode),
          money(p.taxableIncome, p.currencyCode),
          money(p.paye, p.currencyCode),
          money(p.statutoryDeductions, p.currencyCode),
          money(p.otherDeductions, p.currencyCode),
          <strong>{money(p.netPay, p.currencyCode)}</strong>,
          <Btn size="sm" variant="outline"
            onClick={() => hr.downloadFile(hr.payslipPdfUrl(p.id), `payslip-${p.payrollPeriodCode}-${p.employeeNumber}.pdf`)
              .catch(e => relayError(flash, e, 'Could not download the payslip.'))}>PDF</Btn>,
        ])}
      />

      {(files.data ?? []).length > 0 && (
        <div style={{ marginTop: 24 }}>
          <SectionHeader title="Bank Payment Files"
            sub="The stored CSV is exactly what was generated — confirming one posts the journal that moves net pay out of the holding account and into the bank." />
          <DataTable
            headers={['File', 'Bank', 'Payments', 'Amount', 'Excluded', 'Confirmed', 'Journal', 'Actions']}
            empty="No bank files generated."
            rows={(files.data ?? []).map(f => [
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{f.fileName}</span>,
              <span>{f.bankName}{f.needsFormatConfirmation && <Badge variant="amber">layout unverified</Badge>}</span>,
              f.employeeCount,
              money(f.totalAmount),
              f.missingCount > 0
                ? <span style={{ color: T.red, fontSize: 12 }} title={f.missingBankDetails.join('; ')}>{f.missingCount} left out</span>
                : <span style={{ color: T.mgrey }}>none</span>,
              f.confirmedAt ? fmtDate(f.confirmedAt) : <span style={{ color: T.mgrey }}>—</span>,
              f.journalEntryNo ?? (f.journalError ? <Badge variant="red">failed</Badge> : '—'),
              <div style={{ display: 'flex', gap: 6 }}>
                <Btn size="sm" variant="outline"
                  onClick={() => hr.downloadFile(hr.bankFileUrl(f.id), f.fileName)
                    .catch(e => relayError(flash, e, 'Could not download the file.'))}>Download</Btn>
                {!f.journalEntryNo && <Btn size="sm" onClick={() => setConfirming(f)}>Confirm</Btn>}
              </div>,
            ])}
          />
        </div>
      )}

      {journal && <JournalModal preview={journal} onClose={() => setJournal(null)} />}
      {bankOpen && <BankFileModal runId={runId} flash={flash}
        onClose={() => setBankOpen(false)} onSaved={() => { setBankOpen(false); reload() }} />}
      {confirming && <ConfirmBankFileModal file={confirming} flash={flash}
        onClose={() => setConfirming(null)} onSaved={() => { setConfirming(null); reload() }} />}
    </div>
  )
}

function JournalModal({ preview, onClose }) {
  return (
    <Modal title={`Journal — ${preview.runNumber}`} onClose={onClose} width={720}>
      {preview.problems?.length > 0 && (
        <Alert type="error">
          This journal cannot be posted yet: {preview.problems.join(' ')}
        </Alert>
      )}
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        Summed from the payslip lines, so the ledger cannot disagree with the payslips it represents. Net pay is
        credited to the holding account and only reaches the bank when a payment file is confirmed.
      </p>
      <DataTable
        headers={['Account', 'Description', 'Debit', 'Credit']}
        empty="Nothing to post."
        rows={(preview.lines ?? []).map(l => [
          <span><strong style={{ fontFamily: 'monospace' }}>{l.accountCode}</strong> {l.accountName}</span>,
          <span style={{ fontSize: 12, color: T.mgrey }}>{l.description}</span>,
          l.debit ? money(l.debit) : '',
          l.credit ? money(l.credit) : '',
        ])}
      />
      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 10, fontWeight: 700, fontSize: 13 }}>
        <span>{preview.balances ? '✅ Balances' : '⚠️ Does not balance'}</span>
        <span>Dr {money(preview.totalDebit)} · Cr {money(preview.totalCredit)}</span>
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Close</Btn>
      </div>
    </Modal>
  )
}

function BankFileModal({ runId, flash, onClose, onSaved }) {
  const [format, setFormat] = useState('Kcb')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try { relay(flash, await hr.generateBankFile(runId, { format })); onSaved() }
    catch (e) { relayError(flash, e, 'Could not generate the file.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Generate bank payment file" onClose={onClose}>
      <Select label="Bank format" value={format} onChange={setFormat}
        options={BANK_FORMATS.map(v => ({ value: v, label: v === 'CoOp' ? 'Co-operative Bank' : v === 'Kcb' ? 'KCB' : v === 'Ncba' ? 'NCBA' : v }))} />
      <Alert type="warning">
        These column layouts were written from the general shape of Kenyan bulk-salary uploads, not from the
        banks' own template files. <strong>Check the file against your bank's template before a live upload.</strong>
      </Alert>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Anyone without a primary bank account cannot be in the file. They are named on it and an alert is raised —
        they are still owed, and their net pay stays in the holding account until they are paid.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Generating…' : 'Generate'}</Btn>
      </div>
    </Modal>
  )
}

function ConfirmBankFileModal({ file, flash, onClose, onSaved }) {
  const [accounts, setAccounts] = useState(null)
  const [f, setF] = useState({ bankGlAccountId: '', reference: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listGlAccounts().then(a => setAccounts(a ?? [])).catch(() => setAccounts([])) }, [])

  const save = async () => {
    setBusy(true)
    try { relay(flash, await hr.confirmBankFile(file.id, f)); onSaved() }
    catch (e) { relayError(flash, e, 'Could not confirm the file.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Confirm ${file.fileName}`} onClose={onClose}>
      <p style={{ fontSize: 13, marginTop: 0 }}>
        Confirm only once the file really has been uploaded to the bank. This posts the journal that moves
        <strong> {money(file.totalAmount)}</strong> out of the net-pay holding account.
      </p>
      {accounts === null ? <Loading /> : (
        <Select label="Bank account the salaries left" value={f.bankGlAccountId}
          onChange={v => setF({ ...f, bankGlAccountId: v })}
          options={[{ value: '', label: 'Select…' },
            ...accounts.map(a => ({ value: a.id, label: `${a.code} — ${a.name}` }))]} required />
      )}
      <Input label="Bank reference" value={f.reference} onChange={v => setF({ ...f, reference: v })}
        note="The batch or transaction reference from the bank portal." />
      {file.missingCount > 0 && (
        <Alert type="warning">
          {file.missingCount} employee(s) are not in this file. Their pay stays in the holding account and is
          still owed — generate a further file once their bank details are on record.
        </Alert>
      )}
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.bankGlAccountId}>{busy ? 'Confirming…' : 'Confirm payment'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Payslips & P9 — the employee-facing record, from HR's side
// ═════════════════════════════════════════════════════════════════════════════
export function PayslipsTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [employeeId, setEmployeeId] = useState('')
  const [employees, setEmployees] = useState([])

  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])
  const slips = usePayrollData(() => hr.listPayslips({ year, employeeId: employeeId || undefined }), [year, employeeId])

  if (slips.loading) return <Loading />
  if (slips.denied) return <NoAccess />

  return (
    <div>
      <SectionHeader
        title="Payslips"
        sub="Rendered on request from the payslip lines, which are frozen when a run is approved — the same document every time."
        action={employeeId
          ? <Btn size="sm" variant="outline"
              onClick={() => hr.downloadFile(hr.p9PdfUrl(employeeId, year), `P9-${year}.pdf`)
                .catch(e => relayError(flash, e, 'Could not download the P9.'))}>
              P9 certificate {year}
            </Btn>
          : null}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1, year - 2].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
        <select value={employeeId} onChange={e => setEmployeeId(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, minWidth: 240 }}>
          <option value="">All employees</option>
          {employees.map(e => <option key={e.id} value={e.id}>{e.employeeNumber} — {e.fullName}</option>)}
        </select>
      </div>

      {!employeeId && (
        <p style={{ fontSize: 12, color: T.mgrey, marginTop: -4, marginBottom: 12 }}>
          Pick one employee to download their P9 annual tax card. It is built only from approved runs.
        </p>
      )}

      <DataTable
        headers={['Period', 'Employee', 'Gross', 'Taxable', 'PAYE', 'Net', '']}
        empty={`No payslips for ${year}.`}
        rows={(slips.data ?? []).map(p => [
          <span style={{ fontFamily: 'monospace' }}>{p.payrollPeriodCode}</span>,
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{p.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{p.employeeName}</span>
          </span>,
          money(p.grossPay, p.currencyCode),
          money(p.taxableIncome, p.currencyCode),
          money(p.paye, p.currencyCode),
          <strong>{money(p.netPay, p.currencyCode)}</strong>,
          <Btn size="sm" variant="outline"
            onClick={() => hr.downloadFile(hr.payslipPdfUrl(p.id), `payslip-${p.payrollPeriodCode}-${p.employeeNumber}.pdf`)
              .catch(e => relayError(flash, e, 'Could not download the payslip.'))}>PDF</Btn>,
        ])}
      />
    </div>
  )
}
