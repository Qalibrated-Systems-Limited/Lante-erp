import { useState } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// My Workspace — matches the deployed QSL "My Workspace" (7 tabs). UI-only shell:
// tables start empty (as deployed); interactions update local state. Wire to
// /api/me?section=… later. See PORTING_GUIDE.md.
// ─────────────────────────────────────────────────────────────────────────────

const PROFILE = { emp_no: 'LT-001', department: 'Executive', role: 'Managing Director' }
const OV = { leave_balance: 21, l_and_d_hours: 38, ld_target: 40, open_tasks: 0, kpi_avg: null, pending_leave: 0 }
const PROJECT_OPTIONS = [
  { value: '', label: 'Select project…' },
  { value: 'p1', label: 'Coast Water Works — Calibration' },
  { value: 'p2', label: 'KPLC — Meter Testing' },
]

export default function MyWorkspacePage() {
  const { user } = useAuth()
  const [tab, setTab] = useState('overview')
  const [msg, setMsg] = useState(null)
  const [modal, setModal] = useState(null)

  const [leave, setLeave] = useState([])
  const [tasks, setTasks] = useState([])
  const [attendance, setAttendance] = useState({ today: null, recent: [] })
  const [timesheet, setTimesheet] = useState([])
  const [lvForm, setLvForm] = useState({ leave_type: 'annual', start_date: '', end_date: '', reason: '' })
  const [pwForm, setPwForm] = useState({ current_password: '', new_password: '', confirm: '' })
  const [tsForm, setTsForm] = useState({ project: '', date: '', hours: '', desc: '' })

  const name = user ? `${user.firstName} ${user.lastName}` : 'Colleague'
  const daysBetween = (a, b) => Math.max(1, Math.round((new Date(b) - new Date(a)) / 86400000) + 1)

  const applyLeave = () => {
    if (!lvForm.start_date || !lvForm.end_date) return
    const days = daysBetween(lvForm.start_date, lvForm.end_date)
    setLeave(l => [{ ...lvForm, days, status: 'pending' }, ...l])
    setMsg({ type: 'success', text: `Leave applied — ${days} day(s), pending approval` })
    setModal(null); setLvForm({ leave_type: 'annual', start_date: '', end_date: '', reason: '' }); setTab('leave')
  }
  const clock = (which) => {
    if (which === 'in') { setAttendance(a => ({ ...a, today: { clock_in: new Date().toISOString(), clock_out: null } })); setMsg({ type: 'success', text: 'Clocked in' }) }
    else setAttendance(a => ({ ...a, today: { ...a.today, clock_out: new Date().toISOString() } }))
  }
  const completeTask = (id) => { setTasks(ts => ts.map(t => t.id === id ? { ...t, status: 'completed' } : t)); setMsg({ type: 'success', text: 'Task completed' }) }
  const logHours = () => {
    if (!tsForm.project || !tsForm.hours) { setMsg({ type: 'error', text: 'Pick a project and enter hours.' }); return }
    const proj = PROJECT_OPTIONS.find(p => p.value === tsForm.project)?.label || '—'
    setTimesheet(ts => [{ date: tsForm.date || new Date().toISOString(), project: proj, hours: tsForm.hours, desc: tsForm.desc, status: 'pending' }, ...ts])
    setMsg({ type: 'success', text: 'Hours logged — pending approval.' }); setTsForm({ project: '', date: '', hours: '', desc: '' })
  }
  const changePw = () => {
    if (pwForm.new_password !== pwForm.confirm) { setMsg({ type: 'error', text: 'Passwords do not match' }); return }
    setPwForm({ current_password: '', new_password: '', confirm: '' }); setMsg({ type: 'success', text: 'Password changed' })
  }

  const box = { background: '#F8FAFC', border: `1px solid ${T.lgrey}`, borderRadius: 8, padding: '11px 14px' }

  return (
    <>
      <div style={{ padding: 'clamp(16px, 2.4vw, 26px)', width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        <div style={{ marginBottom: 16 }}>
          <h2 style={{ fontSize: 20, fontWeight: 800, color: T.navy, margin: 0 }}>Welcome, {name} 👋</h2>
          <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>Your personal workspace — leave, payslips, tasks and attendance.</p>
        </div>

        <Tabs
          tabs={[
            { id: 'overview', label: 'Overview' }, { id: 'leave', label: 'Leave' },
            { id: 'payslips', label: 'Payslips' }, { id: 'tasks', label: 'My Tasks' },
            { id: 'attendance', label: 'Attendance' }, { id: 'timesheet', label: 'Timesheet' },
            { id: 'account', label: 'Account' },
          ]}
          active={tab} setActive={setTab}
        />

        {tab === 'overview' && (
          <>
            {/* Self-appraisal banner */}
            <div style={{ background: T.amberL, border: '1px solid #FCD34D', borderRadius: 10, padding: '14px 18px', marginBottom: 20, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ fontSize: 22 }}>📝</span>
                <div>
                  <div style={{ fontSize: 14, fontWeight: 700, color: T.amber }}>Monthly Self-Appraisal due — June 2026</div>
                  <div style={{ fontSize: 12.5, color: T.dgrey }}>Takes about five minutes. Shared with your manager and HR once submitted.</div>
                </div>
              </div>
              <Btn onClick={() => setMsg({ type: 'info', text: 'Self-appraisal form coming soon.' })}>Fill It In Now</Btn>
            </div>

            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="Leave Balance" value={`${OV.leave_balance} days`} icon="🏖️" variant="green" />
              <Kpi label="L&D Hours" value={`${OV.l_and_d_hours} / ${OV.ld_target}`} icon="📚" />
              <Kpi label="Open Tasks" value={OV.open_tasks} icon="☑️" variant={OV.open_tasks ? 'amber' : 'green'} />
              <Kpi label="Avg KPI" value={OV.kpi_avg ?? '—'} icon="⭐" variant="amber" />
            </div>

            <Card>
              <SectionHeader title="My Profile" />
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12 }}>
                {[['Employee No', PROFILE.emp_no], ['Department', PROFILE.department], ['Role', PROFILE.role], ['Pending Leave', `${OV.pending_leave} request(s)`]].map(([l, v]) => (
                  <div key={l} style={box}>
                    <div style={{ fontSize: 10, color: T.mgrey, fontWeight: 700, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 4 }}>{l}</div>
                    <div style={{ fontSize: 14, fontWeight: 700, color: T.navy }}>{v || '—'}</div>
                  </div>
                ))}
              </div>
            </Card>
          </>
        )}

        {tab === 'leave' && (
          <>
            <SectionHeader title="My Leave" sub={`Balance: ${OV.leave_balance} days`} action={<Btn onClick={() => setModal('leave')}>+ Apply for Leave</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Type', 'From', 'To', 'Days', 'Status']} empty="No leave requests yet."
                rows={leave.map(r => [
                  <span style={{ textTransform: 'capitalize' }}>{r.leave_type}</span>, fmt.date(r.start_date), fmt.date(r.end_date), r.days,
                  <Badge variant={r.status === 'approved' ? 'green' : r.status === 'rejected' ? 'red' : 'amber'}>{r.status}</Badge>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'payslips' && (
          <>
            <SectionHeader title="My Payslips" sub="Finalised payroll runs" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Period', 'Gross', 'Net Pay', 'Pay Date']} empty="No finalised payslips yet." rows={[]} />
            </Card>
          </>
        )}

        {tab === 'tasks' && (
          <>
            <SectionHeader title="My Tasks" sub="Assigned to you" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Task', 'Priority', 'Due', 'Status']} empty="No tasks assigned to you."
                rows={tasks.map(t => [
                  <strong>{t.title}</strong>,
                  <Badge variant={t.priority === 'critical' ? 'red' : t.priority === 'high' ? 'amber' : 'blue'}>{t.priority}</Badge>,
                  fmt.date(t.due_date),
                  t.status !== 'completed'
                    ? <Btn size="sm" onClick={() => completeTask(t.id)}>✓ Done</Btn>
                    : <Badge variant="green">completed</Badge>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'attendance' && (
          <>
            <SectionHeader title="My Attendance" action={
              !attendance.today ? <Btn onClick={() => clock('in')}>Clock In</Btn>
                : !attendance.today.clock_out ? <Btn variant="gold" onClick={() => clock('out')}>Clock Out</Btn>
                  : <Badge variant="green">Done for today</Badge>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Date', 'In', 'Out', 'Hours', 'Late?']} empty="No attendance records."
                rows={attendance.recent.map(a => [
                  fmt.date(a.date),
                  a.clock_in ? new Date(a.clock_in).toLocaleTimeString() : '—',
                  a.clock_out ? new Date(a.clock_out).toLocaleTimeString() : '—',
                  a.hours_worked ? `${a.hours_worked}h` : '—',
                  a.is_late ? <Badge variant="red">late</Badge> : <Badge variant="green">on time</Badge>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'timesheet' && (
          <>
            <Alert type="info">Log your hours against projects — approved hours feed straight into project labour cost and profitability.</Alert>
            <Card style={{ marginBottom: 20 }}>
              <SectionHeader title="Log Hours" />
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'flex-end' }}>
                <div style={{ flex: '3 1 220px' }}><Select label="Project" value={tsForm.project} onChange={v => setTsForm({ ...tsForm, project: v })} options={PROJECT_OPTIONS} /></div>
                <div style={{ flex: '1 1 140px' }}><Input label="Date" type="date" value={tsForm.date} onChange={v => setTsForm({ ...tsForm, date: v })} /></div>
                <div style={{ flex: '1 1 90px' }}><Input label="Hours" type="number" value={tsForm.hours} onChange={v => setTsForm({ ...tsForm, hours: v })} /></div>
                <div style={{ flex: '3 1 220px' }}><Input label="What did you work on?" value={tsForm.desc} onChange={v => setTsForm({ ...tsForm, desc: v })} /></div>
                <Btn onClick={logHours} style={{ marginBottom: 14, padding: '9px 20px' }}>Log</Btn>
              </div>
            </Card>
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Date', 'Project', 'Hours', 'Description', 'Status']} empty="No hours logged yet."
                rows={timesheet.map(t => [
                  fmt.date(t.date), t.project, `${t.hours}h`, t.desc || '—',
                  <Badge variant={t.status === 'approved' ? 'green' : 'amber'}>{t.status}</Badge>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'account' && (
          <Card style={{ maxWidth: 440 }}>
            <SectionHeader title="Change Password" sub="Use your own secure password" />
            <Input label="Current Password" type="password" value={pwForm.current_password} onChange={v => setPwForm({ ...pwForm, current_password: v })} />
            <Input label="New Password" type="password" value={pwForm.new_password} onChange={v => setPwForm({ ...pwForm, new_password: v })} note="At least 8 characters" />
            <Input label="Confirm New Password" type="password" value={pwForm.confirm} onChange={v => setPwForm({ ...pwForm, confirm: v })} />
            <Btn onClick={changePw} disabled={!pwForm.new_password} style={{ marginTop: 8 }}>Update Password</Btn>
          </Card>
        )}

        {modal === 'leave' && (
          <Modal title="Apply for Leave (HR-005)" onClose={() => setModal(null)} width={460}>
            <Select label="Leave Type" value={lvForm.leave_type} onChange={v => setLvForm({ ...lvForm, leave_type: v })}
              options={[{ value: 'annual', label: 'Annual' }, { value: 'sick', label: 'Sick' }, { value: 'maternity', label: 'Maternity/Paternity' }, { value: 'compassionate', label: 'Compassionate' }, { value: 'unpaid', label: 'Unpaid' }]} />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <Input label="From" type="date" value={lvForm.start_date} onChange={v => setLvForm({ ...lvForm, start_date: v })} required />
              <Input label="To" type="date" value={lvForm.end_date} onChange={v => setLvForm({ ...lvForm, end_date: v })} required />
            </div>
            <Input label="Reason" value={lvForm.reason} onChange={v => setLvForm({ ...lvForm, reason: v })} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={applyLeave} disabled={!lvForm.start_date || !lvForm.end_date}>Submit</Btn>
            </div>
          </Modal>
        )}
      </div>
    </>
  )
}
