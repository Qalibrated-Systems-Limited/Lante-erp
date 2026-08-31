import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H4 — attendance, absences, working-time configuration and the monthly/annual
// reporting. REAL, wired to hr-service (/api/v1/hr/attendance/*).
//
// Worth knowing before reading:
//   • An absence row exists for EVERY unattended working day, authorised ones
//     included — that is what makes the reconciliation against approved leave a
//     complete picture rather than a list of exceptions.
//   • H4 records unpaid DAYS, never money. Turning days into a deduction needs
//     the salary tables that arrive with payroll, so the Absences tab shows the
//     day count that payroll will consume.
//   • The holiday calendar on the Working Time tab also drives LEAVE day counts.
//     Editing it changes what a leave request costs, which is why the tab says so.
// Per HR-DEC-8 these are HR-admin screens: HR clocks staff in on their behalf and
// the same endpoints are what a future mobile app will call.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const fmtTime = (d) => (d ? new Date(d).toLocaleTimeString('en-KE', { hour: '2-digit', minute: '2-digit', hour12: false }) : '—')
const iso = (d) => d.toISOString().slice(0, 10)
const num = (n) => (n === null || n === undefined ? '—' : Number(n).toLocaleString('en-KE', { maximumFractionDigits: 2 }))

const STATUS_VARIANT = {
  Present: 'green', Late: 'amber', Absent: 'red', OnLeave: 'blue',
  Holiday: 'purple', NonWorkingDay: 'default',
}
const METHODS = ['Desktop', 'Biometric', 'Mobile', 'Manual']

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

function SweepButton({ flash, onDone }) {
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const r = await hr.runAttendanceSweep()
      flash(r?.message ?? 'Attendance sweep complete.')
      onDone()
    } catch (e) { flash(e.response?.data?.message ?? 'Sweep failed.') }
    finally { setBusy(false) }
  }
  return <Btn size="sm" variant="outline" onClick={run} disabled={busy}>{busy ? 'Running…' : 'Run attendance sweep'}</Btn>
}

// ── Daily attendance (P27) ───────────────────────────────────────────────────
export function AttendanceTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState({ from: iso(new Date()), to: iso(new Date()), status: '' })
  const [clockOpen, setClockOpen] = useState(null)   // 'in' | 'out'

  const load = useCallback(() => {
    setLoading(true)
    hr.listAttendance({ from: filter.from || undefined, to: filter.to || undefined, status: filter.status || undefined })
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.attendanceSummary().then(setSum).catch(() => {})
  }, [filter])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Expected Today" value={sum?.expectedToday ?? 0} />
        <Kpi label="Clocked In" value={sum?.clockedInToday ?? 0} color={T.green} />
        <Kpi label="Late Today" value={sum?.lateToday ?? 0} color={T.amber} />
        <Kpi label="Absent Today" value={sum?.absentToday ?? 0} color={T.red} />
        <Kpi label="On Leave" value={sum?.onLeaveToday ?? 0} color={T.blue} />
        <Kpi label="Not Clocked Out" value={sum?.missingClockOutToday ?? 0} color={T.amber} />
        <Kpi label="Avg Punctuality" value={`${num(sum?.averagePunctualityThisYear ?? 0)}%`} sub="this year" />
      </div>

      {sum?.todayNote && (
        <Card style={{ padding: 12, marginBottom: 16, background: sum.todayIsWorkingDay ? T.offwt : T.blueL }}>
          <p style={{ margin: 0, fontSize: 12, color: T.dgrey }}>{sum.todayNote}</p>
        </Card>
      )}

      <SectionHeader
        title="Daily Attendance"
        sub="Office staff clock in by desktop or biometric; field staff need a GPS stamp. Lateness is measured against the configured start time, and no clock-in by the cut-off becomes an absence."
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn size="sm" onClick={() => setClockOpen('in')}>Clock in</Btn>
          <Btn size="sm" variant="outline" onClick={() => setClockOpen('out')}>Clock out</Btn>
          <SweepButton flash={flash} onDone={load} />
        </div>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14, alignItems: 'flex-end' }}>
        <div>
          <label style={{ display: 'block', fontSize: 11, color: T.mgrey, marginBottom: 3 }}>From</label>
          <input type="date" value={filter.from} onChange={e => setFilter(f => ({ ...f, from: e.target.value }))}
            style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
        </div>
        <div>
          <label style={{ display: 'block', fontSize: 11, color: T.mgrey, marginBottom: 3 }}>To</label>
          <input type="date" value={filter.to} onChange={e => setFilter(f => ({ ...f, to: e.target.value }))}
            style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
        </div>
        <select value={filter.status} onChange={e => setFilter(f => ({ ...f, status: e.target.value }))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {Object.keys(STATUS_VARIANT).map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Date', 'Employee', 'In', 'Out', 'Hours', 'Late', 'Method', 'GPS', 'Status', 'Note']}
          empty="No attendance in this range."
          rows={rows.map(r => [
            fmtDate(r.date),
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{r.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{r.employeeName}</span>
            </span>,
            fmtTime(r.clockInAt),
            r.missingClockOut
              ? <span style={{ color: T.amber, fontSize: 11, fontWeight: 600 }}>open</span>
              : fmtTime(r.clockOutAt),
            num(r.hoursWorked),
            r.lateMinutes > 0 ? <span style={{ color: T.amber, fontWeight: 600 }}>{r.lateMinutes}m</span> : '—',
            <span style={{ fontSize: 11 }}>{r.clockInMethod ?? '—'}</span>,
            r.hasGps
              ? <span style={{ fontSize: 10, fontFamily: 'monospace' }} title={`${r.clockInLatitude}, ${r.clockInLongitude}`}>
                  {Number(r.clockInLatitude).toFixed(3)}, {Number(r.clockInLongitude).toFixed(3)}
                </span>
              : <span style={{ fontSize: 11, color: T.mgrey }}>{r.workMode === 'Field' ? '⚠ none' : '—'}</span>,
            <Badge variant={STATUS_VARIANT[r.status] ?? 'default'}>{r.status}</Badge>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{r.notes ?? '—'}</span>,
          ])}
        />
      )}

      {clockOpen && (
        <ClockModal mode={clockOpen} flash={flash}
          onClose={() => setClockOpen(null)}
          onSaved={() => { setClockOpen(null); load() }} />
      )}
    </div>
  )
}

function ClockModal({ mode, onClose, onSaved, flash }) {
  const [employees, setEmployees] = useState([])
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    employeeId: '', date: iso(new Date()), time: '', method: '',
    latitude: '', longitude: '', notes: '',
  })
  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const chosen = employees.find(e => e.id === f.employeeId)
  const save = async () => {
    setBusy(true)
    const at = f.time ? `${f.date}T${f.time}:00Z` : undefined
    const body = {
      employeeId: f.employeeId,
      date: f.date || undefined,
      at,
      latitude: f.latitude === '' ? undefined : Number(f.latitude),
      longitude: f.longitude === '' ? undefined : Number(f.longitude),
      notes: f.notes || undefined,
    }
    try {
      const r = mode === 'in'
        ? await hr.clockIn({ ...body, method: f.method || undefined })
        : await hr.clockOut(body)
      flash(r?.message ?? 'Recorded.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not record it.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={mode === 'in' ? 'Clock in' : 'Clock out'} onClose={onClose} width={540}>
      <Select label="Employee" required value={f.employeeId} onChange={v => setF(s => ({ ...s, employeeId: v }))}
        options={[{ value: '', label: 'Select employee…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}${e.workMode === 'Field' ? ' (field)' : ''}` }))]} />

      {mode === 'in' && chosen?.workMode === 'Field' && (
        <p style={{ fontSize: 11, color: T.amber, marginTop: -8, marginBottom: 12 }}>
          Field staff need a GPS location. Without one, record it as a Manual HR correction instead.
        </p>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Working day" type="date" required value={f.date} onChange={v => setF(s => ({ ...s, date: v }))} />
        <Input label="Time (blank = now)" type="time" value={f.time} onChange={v => setF(s => ({ ...s, time: v }))} />
      </div>

      {mode === 'in' && (
        <Select label="Method" value={f.method} onChange={v => setF(s => ({ ...s, method: v }))}
          options={[{ value: '', label: 'Default for their work mode' },
            ...METHODS.map(m => ({ value: m, label: m === 'Manual' ? 'Manual (HR correction)' : m }))]} />
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Latitude" value={f.latitude} onChange={v => setF(s => ({ ...s, latitude: v }))} placeholder="-1.286389" />
        <Input label="Longitude" value={f.longitude} onChange={v => setF(s => ({ ...s, longitude: v }))} placeholder="36.817223" />
      </div>
      <Input label="Notes" value={f.notes} onChange={v => setF(s => ({ ...s, notes: v }))} />

      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 6 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId}>
          {busy ? 'Saving…' : mode === 'in' ? 'Clock in' : 'Clock out'}
        </Btn>
      </div>
    </Modal>
  )
}

// ── Absences (P27 step 27.5 / ATT-004) ───────────────────────────────────────
export function AbsencesTab({ flash }) {
  const [rows, setRows] = useState([])
  const [unpaid, setUnpaid] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [authorised, setAuthorised] = useState('false')
  const [newOpen, setNewOpen] = useState(false)
  const [excusing, setExcusing] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    hr.listAbsences({ authorised: authorised === '' ? undefined : authorised === 'true' })
      .then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.listUnpaidAbsences({}).then(u => setUnpaid(u ?? [])).catch(() => setUnpaid([]))
    hr.attendanceSummary().then(setSum).catch(() => {})
  }, [authorised])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Unauthorised (month)" value={sum?.unauthorisedAbsencesThisMonth ?? 0} color={T.red} />
        <Kpi label="Unpaid Days Pending" value={num(sum?.unpaidDaysPendingPayroll ?? 0)} color={T.amber} sub="awaiting payroll" />
        <Kpi label="Patterns Flagged" value={sum?.absencePatternsFlagged ?? 0} color={T.red} sub="3+ in 30 days" />
      </div>

      <SectionHeader
        title="Absences"
        sub="A row exists for every unattended working day. Days covered by approved leave are authorised automatically; the rest carry one unpaid day each until HR excuses them."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ Record absence</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={authorised} onChange={e => setAuthorised(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="false">Unauthorised only</option>
          <option value="true">Authorised only</option>
          <option value="">All</option>
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Date', 'Employee', 'Reason', 'Authorised', 'Leave', 'Unpaid', 'Source', 'Payroll', 'Actions']}
          empty="No absences match this filter."
          rows={rows.map(a => [
            fmtDate(a.date),
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{a.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{a.employeeName}</span>
            </span>,
            <span style={{ fontSize: 11 }}>{a.reason ?? '—'}</span>,
            a.isAuthorised
              ? <Badge variant="green">authorised</Badge>
              : <Badge variant="red">unauthorised</Badge>,
            a.linkedLeaveTypeCode
              ? <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{a.linkedLeaveTypeCode}</span>
              : '—',
            a.unpaidDays > 0 ? <span style={{ color: T.red, fontWeight: 600 }}>{num(a.unpaidDays)}</span> : '—',
            <span style={{ fontSize: 11, color: T.mgrey }}>{a.source === 'Detected' ? 'detected' : 'HR'}</span>,
            a.releasedToPayrollAt
              ? <span style={{ fontSize: 11, color: T.mgrey }}>taken</span>
              : a.unpaidDays > 0 ? <span style={{ fontSize: 11, color: T.amber }}>pending</span> : '—',
            !a.isAuthorised && !a.releasedToPayrollAt
              ? <Btn size="sm" variant="outline" onClick={() => setExcusing(a)}>Excuse</Btn>
              : <span style={{ fontSize: 11, color: T.mgrey }}>—</span>,
          ])}
        />
      )}

      {unpaid.length > 0 && (
        <>
          <SectionHeader title="Unpaid Days Awaiting Payroll"
            sub="What payroll will deduct. H4 records the day count; the money is calculated by payroll from the employee's salary." />
          <DataTable
            headers={['Employee', 'Unpaid days', 'Absences', 'Dates']}
            empty="Nothing pending."
            rows={unpaid.map(u => [
              <span>
                <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{u.employeeNumber}</span>
                <span style={{ marginLeft: 6 }}>{u.employeeName}</span>
              </span>,
              <strong>{num(u.unpaidDays)}</strong>,
              u.absenceCount,
              <span style={{ fontSize: 10, color: T.mgrey }}>
                {(u.dates ?? []).slice(0, 6).map(d => fmtDate(d)).join(', ')}
                {(u.dates?.length ?? 0) > 6 && ` +${u.dates.length - 6} more`}
              </span>,
            ])}
          />
        </>
      )}

      {newOpen && <RecordAbsenceModal flash={flash} onClose={() => setNewOpen(false)} onSaved={() => { setNewOpen(false); load() }} />}
      {excusing && <ExcuseModal absence={excusing} flash={flash} onClose={() => setExcusing(null)} onSaved={() => { setExcusing(null); load() }} />}
    </div>
  )
}

function RecordAbsenceModal({ onClose, onSaved, flash }) {
  const [employees, setEmployees] = useState([])
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({ employeeId: '', date: iso(new Date()), reason: '', isAuthorised: false })
  useEffect(() => { hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.recordAbsence({ ...f, reason: f.reason || undefined })
      flash(r?.message ?? 'Recorded.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not record the absence.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Record absence" onClose={onClose} width={520}>
      <Select label="Employee" required value={f.employeeId} onChange={v => setF(s => ({ ...s, employeeId: v }))}
        options={[{ value: '', label: 'Select employee…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} />
      <Input label="Date" type="date" required value={f.date} onChange={v => setF(s => ({ ...s, date: v }))}
        note="Must be a working day, not in the future, and after their hire date." />
      <Input label="Reason" value={f.reason} onChange={v => setF(s => ({ ...s, reason: v }))} />
      <Select label="Authorised" value={String(f.isAuthorised)} onChange={v => setF(s => ({ ...s, isAuthorised: v === 'true' }))}
        options={[
          { value: 'false', label: 'No — counts as one unpaid day' },
          { value: 'true', label: 'Yes — no unpaid days' },
        ]} />
      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.date}>{busy ? 'Saving…' : 'Record'}</Btn>
      </div>
    </Modal>
  )
}

function ExcuseModal({ absence, onClose, onSaved, flash }) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setBusy(true)
    try {
      const r = await hr.excuseAbsence(absence.id, { reason })
      flash(r?.message ?? 'Excused.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not excuse it.') }
    finally { setBusy(false) }
  }
  return (
    <Modal title={`Excuse absence — ${absence.employeeName}`} onClose={onClose} width={480}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        {fmtDate(absence.date)}. Authorising this clears its {num(absence.unpaidDays)} unpaid day so payroll will
        not deduct it.
      </p>
      <Input label="Reason" required value={reason} onChange={setReason} placeholder="Why the absence is excused…" />
      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !reason}>{busy ? 'Saving…' : 'Excuse absence'}</Btn>
      </div>
    </Modal>
  )
}

// ── Scorecards + monthly reports (P28) ───────────────────────────────────────
export function AttendanceReportsTab() {
  const [cards, setCards] = useState([])
  const [reports, setReports] = useState([])
  const [loading, setLoading] = useState(true)
  const [year, setYear] = useState(new Date().getFullYear())

  const load = useCallback(() => {
    setLoading(true)
    Promise.all([
      hr.listScorecards({ year }).then(r => setCards(r ?? [])).catch(() => setCards([])),
      hr.listMonthlyReports({ year }).then(r => setReports(r ?? [])).catch(() => setReports([])),
    ]).finally(() => setLoading(false))
  }, [year])
  useEffect(() => { load() }, [load])

  const scoreColour = (s) => (s >= 90 ? T.green : s >= 75 ? T.amber : T.red)

  return (
    <div>
      <SectionHeader
        title="Attendance Scorecards"
        sub="Recomputed from the underlying records each month, so they stay true after an absence is excused or a clock-in corrected. The score weights punctuality 70 and turning up 30, and feeds the KPI appraisal."
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <input type="number" value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ width: 110, height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
      </div>

      {loading ? <Loading /> : (
        <>
          <DataTable
            headers={['Employee', 'Dept', 'Expected', 'Present', 'Late', 'On Leave', 'Auth. Abs', 'Unauth. Abs', 'Late Mins', 'Punctuality', 'Absence', 'Score']}
            empty={`No scorecards for ${year} — they are written by the monthly sweep.`}
            rows={cards.map(s => [
              <span>
                <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{s.employeeNumber}</span>
                <span style={{ marginLeft: 6 }}>{s.employeeName}</span>
              </span>,
              <span style={{ fontSize: 11 }}>{s.departmentName ?? '—'}</span>,
              s.expectedDays,
              s.daysPresent,
              s.daysLate > 0 ? <span style={{ color: T.amber }}>{s.daysLate}</span> : '—',
              s.daysOnLeave || '—',
              s.authorisedAbsences || '—',
              s.unauthorisedAbsences > 0 ? <span style={{ color: T.red, fontWeight: 600 }}>{s.unauthorisedAbsences}</span> : '—',
              s.totalLateMinutes || '—',
              // Punctuality is undefined with no attended days — say so rather than showing a bare 0%.
              s.daysPresent > 0 ? `${num(s.punctualityRate)}%` : <span style={{ fontSize: 11, color: T.mgrey }}>no data</span>,
              `${num(s.absenceRate)}%`,
              <strong style={{ color: scoreColour(s.score) }}>{num(s.score)}</strong>,
            ])}
          />

          <div style={{ marginTop: 26 }}>
            <SectionHeader
              title="Monthly Departmental Reports"
              sub="Generated once the month turns. Each department head sees their own department's figures."
            />
            <DataTable
              headers={['Period', 'Department', 'Headcount', 'Expected', 'Present', 'Late', 'Auth. Abs', 'Unauth. Abs', 'Unpaid Days', 'Punctuality', 'Absence Rate']}
              empty={`No monthly reports for ${year} yet.`}
              rows={reports.map(r => [
                r.period,
                r.departmentName ?? 'Unassigned',
                r.headcount,
                r.expectedDays,
                r.daysPresent,
                r.daysLate || '—',
                r.authorisedAbsences || '—',
                r.unauthorisedAbsences > 0 ? <span style={{ color: T.red, fontWeight: 600 }}>{r.unauthorisedAbsences}</span> : '—',
                r.unpaidDays > 0 ? <span style={{ color: T.red }}>{num(r.unpaidDays)}</span> : '—',
                `${num(r.punctualityRate)}%`,
                `${num(r.absenceRate)}%`,
              ])}
            />
          </div>
        </>
      )}
    </div>
  )
}

// ── Working time + holidays (ATT-001) ────────────────────────────────────────
export function WorkingTimeTab({ flash }) {
  const [config, setConfig] = useState(null)
  const [holidays, setHolidays] = useState([])
  const [loading, setLoading] = useState(true)
  const [year, setYear] = useState(new Date().getFullYear())
  const [editing, setEditing] = useState(null)   // holiday | 'new'
  const [busy, setBusy] = useState(false)
  const [form, setForm] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    hr.getAttendanceSettings().then(c => { setConfig(c); setForm(c) }).catch(() => setConfig(null))
    hr.listHolidays({ year }).then(h => setHolidays(h ?? [])).catch(() => setHolidays([])).finally(() => setLoading(false))
  }, [year])
  useEffect(() => { load() }, [load])

  const saveSettings = async () => {
    setBusy(true)
    try {
      const r = await hr.updateAttendanceSettings({
        workDayStartMinutes: Number(form.workDayStartMinutes),
        workDayEndMinutes: Number(form.workDayEndMinutes),
        lunchStartMinutes: Number(form.lunchStartMinutes),
        lunchMinutes: Number(form.lunchMinutes),
        graceMinutes: Number(form.graceMinutes),
        absenceCutoffMinutes: Number(form.absenceCutoffMinutes),
        worksMonday: form.worksMonday, worksTuesday: form.worksTuesday, worksWednesday: form.worksWednesday,
        worksThursday: form.worksThursday, worksFriday: form.worksFriday,
        worksSaturday: form.worksSaturday, worksSunday: form.worksSunday,
        requireGpsForField: form.requireGpsForField,
        absencePatternThreshold: Number(form.absencePatternThreshold),
        absencePatternWindowDays: Number(form.absencePatternWindowDays),
        absenceBackfillDays: Number(form.absenceBackfillDays),
      })
      flash(r?.message ?? 'Saved.')
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not save the settings.') }
    finally { setBusy(false) }
  }

  const seed = async () => {
    setBusy(true)
    try {
      const r = await hr.seedKenyanHolidays(year)
      flash(r?.message ?? 'Seeded.')
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not install the holidays.') }
    finally { setBusy(false) }
  }

  const removeHoliday = async (h) => {
    setBusy(true)
    try {
      const r = await hr.deleteHoliday(h.id)
      flash(r?.message ?? 'Removed.')
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not remove it.') }
    finally { setBusy(false) }
  }

  if (loading || !form) return <Loading />

  const minutesField = (label, key, note) => (
    <div>
      <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>{label}</label>
      <input type="time" value={hhmm(form[key])} onChange={e => setForm(s => ({ ...s, [key]: toMinutes(e.target.value) }))}
        style={{ width: '100%', height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box' }} />
      {note && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>{note}</p>}
    </div>
  )

  return (
    <div>
      <SectionHeader
        title="Working Time"
        sub="The working day, grace period and absence cut-off. The working week set here also decides how many days a leave request costs."
      />

      <Card style={{ padding: 18, marginBottom: 24 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 14, marginBottom: 14 }}>
          {minutesField('Day starts', 'workDayStartMinutes')}
          {minutesField('Day ends', 'workDayEndMinutes')}
          {minutesField('Lunch starts', 'lunchStartMinutes')}
          <Input label="Lunch (minutes)" type="number" value={form.lunchMinutes}
            onChange={v => setForm(s => ({ ...s, lunchMinutes: v }))} note="Deducted from hours worked" />
          <Input label="Grace (minutes)" type="number" value={form.graceMinutes}
            onChange={v => setForm(s => ({ ...s, graceMinutes: v }))} note={`Late after ${config?.lateAfter}`} />
          {minutesField('Absent after', 'absenceCutoffMinutes', 'No clock-in by then = absence')}
        </div>

        <p style={{ fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 6 }}>Working week</p>
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', marginBottom: 16 }}>
          {[['worksMonday', 'Mon'], ['worksTuesday', 'Tue'], ['worksWednesday', 'Wed'], ['worksThursday', 'Thu'],
            ['worksFriday', 'Fri'], ['worksSaturday', 'Sat'], ['worksSunday', 'Sun']].map(([key, label]) => (
            <label key={key} style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13 }}>
              <input type="checkbox" checked={!!form[key]} onChange={e => setForm(s => ({ ...s, [key]: e.target.checked }))} />
              {label}
            </label>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 14 }}>
          <Select label="GPS for field staff" value={String(form.requireGpsForField)}
            onChange={v => setForm(s => ({ ...s, requireGpsForField: v === 'true' }))}
            options={[{ value: 'true', label: 'Required' }, { value: 'false', label: 'Optional' }]} />
          <Input label="Absence pattern threshold" type="number" value={form.absencePatternThreshold}
            onChange={v => setForm(s => ({ ...s, absencePatternThreshold: v }))} note="Escalates to HR" />
          <Input label="Pattern window (days)" type="number" value={form.absencePatternWindowDays}
            onChange={v => setForm(s => ({ ...s, absencePatternWindowDays: v }))} />
          <Input label="Absence backfill (days)" type="number" value={form.absenceBackfillDays}
            onChange={v => setForm(s => ({ ...s, absenceBackfillDays: v }))}
            note="How far back the sweep detects missed days" />
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 14 }}>
          <Btn onClick={saveSettings} disabled={busy}>{busy ? 'Saving…' : 'Save working time'}</Btn>
        </div>
      </Card>

      <SectionHeader
        title="Public Holidays"
        sub="Holidays are excluded from both attendance expectations and leave day counts — adding one makes leave spanning it cost a day less. Fixed-date holidays repeat every year; movable ones (Good Friday, the Eids) need a row per year."
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn size="sm" variant="outline" onClick={seed} disabled={busy}>Install Kenyan holidays</Btn>
          <Btn size="sm" onClick={() => { setEditing('new'); }}>+ Add holiday</Btn>
        </div>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <input type="number" value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ width: 110, height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
      </div>

      <DataTable
        headers={['Date', 'Name', 'Repeats', 'Note', 'Actions']}
        empty="No holidays configured — install the Kenyan set to start."
        rows={holidays.map(h => [
          fmtDate(h.date),
          h.name,
          h.isRecurring
            ? <Badge variant="navy">every year</Badge>
            : <Badge variant="default">{new Date(h.date).getFullYear()} only</Badge>,
          <span style={{ fontSize: 11, color: T.mgrey }}>{h.notes ?? '—'}</span>,
          <div style={{ display: 'flex', gap: 6 }}>
            <Btn size="sm" variant="outline" onClick={() => setEditing(h)}>Edit</Btn>
            <Btn size="sm" variant="ghost" onClick={() => removeHoliday(h)} disabled={busy}>Remove</Btn>
          </div>,
        ])}
      />

      {editing && (
        <HolidayModal holiday={editing === 'new' ? null : editing} year={year} flash={flash}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); load() }} />
      )}
    </div>
  )
}

function HolidayModal({ holiday, year, onClose, onSaved, flash }) {
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    date: holiday ? String(holiday.date).slice(0, 10) : `${year}-01-01`,
    name: holiday?.name ?? '',
    isRecurring: holiday?.isRecurring ?? false,
    notes: holiday?.notes ?? '',
  })

  const save = async () => {
    setBusy(true)
    try {
      const dto = { date: f.date, name: f.name, isRecurring: f.isRecurring, notes: f.notes || undefined }
      const r = holiday ? await hr.updateHoliday(holiday.id, dto) : await hr.createHoliday(dto)
      flash(r?.message ?? 'Saved.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not save the holiday.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={holiday ? `Edit ${holiday.name}` : 'Add public holiday'} onClose={onClose} width={480}>
      <Input label="Date" type="date" required value={f.date} onChange={v => setF(s => ({ ...s, date: v }))} />
      <Input label="Name" required value={f.name} onChange={v => setF(s => ({ ...s, name: v }))} placeholder="Good Friday" />
      <Select label="Repeats" value={String(f.isRecurring)} onChange={v => setF(s => ({ ...s, isRecurring: v === 'true' }))}
        options={[
          { value: 'false', label: 'This year only (movable feast)' },
          { value: 'true', label: 'Every year on this date' },
        ]} />
      <Input label="Note" value={f.notes} onChange={v => setF(s => ({ ...s, notes: v }))} />
      <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.name || !f.date}>{busy ? 'Saving…' : 'Save holiday'}</Btn>
      </div>
    </Modal>
  )
}

// Minutes-from-midnight is how the server stores a time-of-day; the inputs speak "HH:mm".
const hhmm = (m) => `${String(Math.floor((m ?? 0) / 60)).padStart(2, '0')}:${String((m ?? 0) % 60).padStart(2, '0')}`
const toMinutes = (v) => {
  const [h, m] = String(v || '0:0').split(':').map(Number)
  return (h || 0) * 60 + (m || 0)
}
