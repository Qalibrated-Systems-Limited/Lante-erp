import { useState, useEffect } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Btn, Badge, Alert, Tabs, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import { EmployeesTab, PositionsTab, OrgChartTab } from './EmployeeMaster.jsx'
import { ProbationTab, ContractsTab } from './ProbationContracts.jsx'
import { LeaveRequestsTab, LeaveEntitlementsTab, LeaveTypesTab, CarryForwardTab } from './LeaveManagement.jsx'
import { AttendanceTab, AbsencesTab, AttendanceReportsTab, WorkingTimeTab } from './AttendanceManagement.jsx'
import { PayrollSetupTab, SalaryStructuresTab, EmployeeSalariesTab, StatutoryRatesTab, PayrollDeductionsTab } from './PayrollSetup.jsx'
import { PayrollRunsTab, OvertimeTab, PayslipsTab } from './PayrollRuns.jsx'
import { LearningPlansTab, TrainingTab, ComplianceTab, KnowledgeSharingTab } from './LearningDevelopment.jsx'
import { SalaryIncrementsTab } from './SalaryIncrements.jsx'
import { ScorecardsTab, AppraisalsTab, PipsTab } from './Appraisals.jsx'
import { DisciplinaryTab, GrievancesTab, SeparationsTab } from './Discipline.jsx'
import { CommissionPlansTab, CommissionStatementsTab } from './Commission.jsx'
import { RequisitionsTab, VacanciesTab, ApplicantsTab, OffersTab } from './Recruitment.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// HR & Payroll — ported from the QSL Next.js reference (HR() + api/hr/
// route.js), not screenshots (none exist yet): the real L&D gate (HR-026,
// 40h/year), the increment status flow (proposed -> blocked/md_approved/
// rejected), the disciplinary 5-stage sequence (HR-020: incident ->
// investigation -> show_cause -> hearing -> outcome, each signed), the
// appraisal escalation rule (manager score < 50 for 2 consecutive months ->
// final_warning, 3 -> termination_review), and the real overtime formula
// (hourly = basicSalary/26/8; ×1.5 weekday, ×2 holiday) all mirror that
// source.
//
// EVERY TAB IS NOW REAL and wired to hr-service — H1 through H12, each in its
// own file (EmployeeMaster, ProbationContracts, LeaveManagement, Attendance-
// Management, PayrollSetup, PayrollRuns, LearningDevelopment, SalaryIncrements,
// Appraisals, Discipline, Commission, Recruitment). No "(mock)" tabs remain.
//
// What is left in THIS file is the residue the real tabs have not yet replaced:
// the seeded mock roster and the CPD-log and disciplinary-detail modals that
// still read it. They are kept only because they are still mounted here; every
// tab body above them calls the API.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const LD_TARGET = 40
const WARNING_SCORE = 50
const FINAL_WARN_COUNT = 2
const TERM_COUNT = 3
const DISC_STAGES = ['incident', 'investigation', 'show_cause', 'hearing', 'outcome']
const DEPARTMENTS = ['Admin', 'BD', 'Commercial', 'Engineering', 'Executive', 'Finance', 'HR', 'HSE', 'ICT', 'Logistics', 'Operations', 'Projects']
const CURRENT_USER = 'Henry Adar'

const CPD_PLATFORMS = [
  { id: 'p1', name: 'Alison', url: 'https://alison.com', description: 'Free CPD UK-accredited courses & diplomas, 6,000+ courses. Certificates verified via Learner Achievement Verification link.' },
  { id: 'p2', name: 'LinkedIn Learning', url: 'https://www.linkedin.com/learning/', description: 'Business, technical and creative courses with certificates.' },
  { id: 'p3', name: 'Coursera', url: 'https://www.coursera.org', description: 'University and industry courses, many free to audit.' },
  { id: 'p4', name: 'edX', url: 'https://www.edx.org', description: 'University-backed professional and technical courses.' },
  { id: 'p5', name: 'Udemy', url: 'https://www.udemy.com', description: 'Practical skills courses across IT, engineering and business.' },
  { id: 'p6', name: 'Saylor Academy', url: 'https://www.saylor.org', description: 'Free, fully accredited-pathway courses with free certificates.' },
  { id: 'p7', name: 'Google Digital Garage', url: 'https://learndigital.withgoogle.com/digitalgarage', description: 'Free digital skills, marketing and career certificates from Google.' },
  { id: 'p8', name: 'FutureLearn', url: 'https://www.futurelearn.com', description: 'University and industry short courses, free to audit.' },
  { id: 'p9', name: 'Khan Academy', url: 'https://www.khanacademy.org', description: 'Free courses in maths, science and more, with progress certificates.' },
  { id: 'p10', name: 'NEBOSH / IOSH (HSE)', url: 'https://www.nebosh.org.uk', description: 'Health & safety qualifications — relevant for HSE/field staff CPD.' },
  { id: 'p11', name: 'Kenya Accountants & Secretaries National Examinations Board (KASNEB)', url: 'https://www.kasneb.or.ke', description: 'CPD points for finance/accounting professionals in Kenya.' },
  { id: 'p12', name: 'Engineers Board of Kenya (EBK) CPD Portal', url: 'https://www.ebk.or.ke', description: 'Mandatory CPD tracking for registered engineers in Kenya.' },
]

// Real seeded roster from the deployed app (13 of the real 22 confirmed via
// screenshots — the rest are off-screen). Every real employee shown has a
// signature key already on file, unlike the source's example UI which shows
// some employees with "No Sig" — matches the deployed seed, not the source.
// L&D hours below are the real confirmed values (KPI Scorecards screenshot)
// for the names that appeared there; the rest default to 20h, the value
// nearly every other confirmed employee shares. avgScore and cpdPoints are
// 0/null for everyone — the real KPI Scorecards and CPD screenshots show
// "—" / 0.0 across the board (no appraisal cycle or CPD logging has run
// yet). reportingTo is null for everyone too — the real CPD table's
// "Manager" column shows "—" even for senior staff, so no hierarchy is
// actually assigned in the real seed yet either.
const INITIAL_EMPLOYEES = [
  { id: 'emp_1', empNo: 'LT-001', firstName: 'Henry', lastName: 'Adar', email: 'hadar@qalibrated.co.ke', department: 'Executive', role: 'Managing Director', basicSalary: 450_000, leaveBalance: 21, signatureKey: 'LT-DS-HA-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_2', empNo: 'LT-002', firstName: 'Sarah', lastName: 'Kamau', email: 'skamau@qalibrated.co.ke', department: 'Finance', role: 'Finance Manager', basicSalary: 280_000, leaveBalance: 21, signatureKey: 'LT-DS-SK-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_5', empNo: 'LT-005', firstName: 'David', lastName: 'Mwangi', email: 'dmwangi@qalibrated.co.ke', department: 'Engineering', role: 'Senior Engineer', basicSalary: 240_000, leaveBalance: 21, signatureKey: 'LT-DS-DM-2024', status: 'active', lAndDHours: 28, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_6', empNo: 'LT-006', firstName: 'Faith', lastName: 'Njeri', email: 'fnjeri@qalibrated.co.ke', department: 'BD', role: 'Sales Engineer', basicSalary: 200_000, leaveBalance: 21, signatureKey: 'LT-DS-FN-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_8', empNo: 'LT-008', firstName: 'Mary', lastName: 'Akinyi', email: 'makinyi@qalibrated.co.ke', department: 'Finance', role: 'Accountant', basicSalary: 160_000, leaveBalance: 21, signatureKey: 'LT-DS-MA-2024', status: 'active', lAndDHours: 15, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_14', empNo: 'LT-014', firstName: 'Tom', lastName: 'Omondi', email: 'tomondi@qalibrated.co.ke', department: 'Engineering', role: 'Calibration Technician', basicSalary: 130_000, leaveBalance: 21, signatureKey: 'LT-DS-TO-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_15', empNo: 'LT-015', firstName: 'Diana', lastName: 'Achieng', email: 'dachieng@qalibrated.co.ke', department: 'Commercial', role: 'Commercial Manager', basicSalary: 210_000, leaveBalance: 21, signatureKey: 'LT-DS-DA-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_16', empNo: 'LT-016', firstName: 'Kevin', lastName: 'Njoroge', email: 'knjoroge@qalibrated.co.ke', department: 'Commercial', role: 'Sales Representative', basicSalary: 110_000, leaveBalance: 21, signatureKey: 'LT-DS-KN-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_17', empNo: 'LT-017', firstName: 'Caroline', lastName: 'Mwende', email: 'cmwende@qalibrated.co.ke', department: 'Finance', role: 'Accountant', basicSalary: 145_000, leaveBalance: 21, signatureKey: 'LT-DS-CM-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_19', empNo: 'LT-019', firstName: 'James', lastName: 'Otieno', email: 'jotieno@qalibrated.co.ke', department: 'Projects', role: 'Projects Coordinator', basicSalary: 130_000, leaveBalance: 21, signatureKey: 'LT-DS-JO-2024', status: 'active', lAndDHours: 35, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_20', empNo: 'LT-020', firstName: 'Felix', lastName: 'Mbugua', email: 'fmbugua@qalibrated.co.ke', department: 'Commercial', role: 'Bids Coordinator', basicSalary: 140_000, leaveBalance: 21, signatureKey: 'LT-DS-FM-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 2200, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_21', empNo: 'LT-021', firstName: 'Joyce', lastName: 'Atieno', email: 'jatieno@qalibrated.co.ke', department: 'Finance', role: 'Fixed Assets Manager', basicSalary: 150_000, leaveBalance: 21, signatureKey: 'LT-DS-JA-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
  { id: 'emp_22', empNo: 'LT-022', firstName: 'Brenda', lastName: 'Cherono', email: 'bcherono@qalibrated.co.ke', department: 'Admin', role: 'Receptionist', basicSalary: 70_000, leaveBalance: 21, signatureKey: 'LT-DS-BC-2024', status: 'active', lAndDHours: 20, avgScore: null, helbMonthly: 3500, cpdPoints: 0, cpdTarget: 20, reportingTo: null },
]

const INITIAL_APPRAISAL_FOR_REVIEW = [
  { id: 'ap_1', employeeId: 'emp_22', period: '2026-06', selfScore: 70, achievements: 'Kept front-office running smoothly and coordinated 3 client site visits ahead of schedule.', managerScore: null, hrComments: null, status: 'pending_manager' },
]
const INITIAL_APPRAISAL_HISTORY = [
  { id: 'ap_0a', employeeId: 'emp_20', period: '2026-04', managerScore: 45, status: 'closed' },
  { id: 'ap_0b', employeeId: 'emp_20', period: '2026-05', managerScore: 42, status: 'pending_hr', hrComments: null },
]

const quoteLevel = (streak) => streak >= TERM_COUNT ? 'termination_review' : streak >= FINAL_WARN_COUNT ? 'final_warning' : 'warning'

export default function HrPage() {
  const [tab, setTab] = useState('employees')
  const [employees, setEmployees] = useState(INITIAL_EMPLOYEES)
  const [cpdLogs, setCpdLogs] = useState([])
  const [appraisals, setAppraisals] = useState([...INITIAL_APPRAISAL_FOR_REVIEW, ...INITIAL_APPRAISAL_HISTORY])
  const [warnings, setWarnings] = useState([])
  const [increments, setIncrements] = useState([])
  const [disciplinary, setDisciplinary] = useState([])
  const [reviewScores, setReviewScores] = useState({})
  const [modal, setModal] = useState(null)
  const [cpdLogDetailFor, setCpdLogDetailFor] = useState(null)
  const [discDetailFor, setDiscDetailFor] = useState(null)
  const [advForm, setAdvForm] = useState({ step: '', notes: '' })
  const [empSearch, setEmpSearch] = useState('')
  const [kpiSearch, setKpiSearch] = useState('')
  const [cpdSearch, setCpdSearch] = useState('')
  const [editEmpFor, setEditEmpFor] = useState(null)
  const [msg, setMsg] = useState(null)
  // The H1 screens report through a flash(text) callback; route it into this page's Alert banner.
  // Second argument is optional so every existing caller keeps working, but a refusal or a warning
  // must not render as a green tick — H5 in particular says most of what it has to say through those.
  const hrFlash = (text, type = 'success') => setMsg({ type, text })
  const [tabLoading, setTabLoading] = useState(false)

  useEffect(() => {
    setTabLoading(true)
    const t = setTimeout(() => setTabLoading(false), 400)
    return () => clearTimeout(t)
  }, [tab])

  const empName = (id) => { const e = employees.find(x => x.id === id); return e ? `${e.firstName} ${e.lastName}` : '—' }
  const activeEmployees = employees.filter(e => e.status === 'active')
  const visibleEmployees = activeEmployees.filter(e => `${e.empNo} ${e.firstName} ${e.lastName} ${e.department} ${e.role}`.toLowerCase().includes(empSearch.toLowerCase()))
  const kpiEmployees = activeEmployees.filter(e => `${e.firstName} ${e.lastName} ${e.department}`.toLowerCase().includes(kpiSearch.toLowerCase()))
  const cpdEmployees = activeEmployees.filter(e => `${e.firstName} ${e.lastName} ${e.department}`.toLowerCase().includes(cpdSearch.toLowerCase()))
  const appraisalForReview = appraisals.filter(a => a.status === 'pending_manager')
  const pendingHrReview = appraisals.filter(a => a.status === 'pending_hr')
  const activeWarnings = warnings.filter(w => !w.resolved)
  const discDetail = disciplinary.find(d => d.id === discDetailFor) || null

  function createEmployee(form) {
    const empNo = `LT-${String(employees.length + 1).padStart(3, '0')}`
    setEmployees(es => [...es, {
      id: `emp_${es.length + 1}`, empNo, firstName: form.firstName, lastName: form.lastName, email: form.email,
      department: form.department, role: form.role, basicSalary: Number(form.basicSalary) || 0,
      leaveBalance: 21, signatureKey: null, status: 'active', lAndDHours: 0, avgScore: null,
      helbMonthly: 0, cpdPoints: 0, cpdTarget: 20, reportingTo: null,
    }])
    setModal(null)
    setMsg({ type: 'success', text: `Employee created — Emp No: ${empNo}. Now register their user account.` })
  }

  function saveEmployee(updated) {
    setEmployees(es => es.map(e => e.id === updated.id ? updated : e))
    setEditEmpFor(null)
    setMsg({ type: 'success', text: `${updated.firstName} ${updated.lastName} updated.` })
  }

  function exitEmployee(emp) {
    if (window.confirm(`Exit ${emp.firstName} ${emp.lastName}? Their record stays for history but they drop out of active staff.`)) {
      setEmployees(es => es.map(e => e.id === emp.id ? { ...e, status: 'exited' } : e))
      setMsg({ type: 'success', text: `${emp.firstName} ${emp.lastName} exited.` })
    }
  }

  function logCpd(form) {
    setCpdLogs(ls => [...ls, { id: `cpd_${ls.length + 1}`, ...form, points: Number(form.points) }])
    setEmployees(es => es.map(e => e.id === form.employeeId ? { ...e, cpdPoints: e.cpdPoints + Number(form.points) } : e))
    setModal(null)
    setMsg({ type: 'success', text: `${form.points} CPD point(s) logged` })
  }



  function proposeIncrement(form) {
    const emp = employees.find(e => e.id === form.employeeId)
    const pct = emp.basicSalary ? Math.round((Number(form.proposedSalary) - emp.basicSalary) / emp.basicSalary * 1000) / 10 : 0
    const blocked = (emp.lAndDHours || 0) < LD_TARGET
    setIncrements(is => [...is, {
      id: `inc_${is.length + 1}`, employeeId: form.employeeId, currentSalary: emp.basicSalary, proposedSalary: Number(form.proposedSalary),
      incrementPct: pct, effectiveMonth: form.effectiveMonth, reason: form.reason,
      status: blocked ? 'blocked' : 'proposed', blockedReason: blocked ? `HR-026: L&D ${emp.lAndDHours || 0}h < ${LD_TARGET}h target — training block` : null,
    }])
    setModal(null)
    setMsg({ type: blocked ? 'warning' : 'success', text: blocked ? 'Proposed but BLOCKED (training shortfall)' : `Increment proposed (${pct}%)` })
  }

  function clearIncrementBlock(inc) {
    const emp = employees.find(e => e.id === inc.employeeId)
    if ((emp.lAndDHours || 0) < LD_TARGET) { setMsg({ type: 'error', text: `HR-026: L&D shortfall not resolved — ${emp.lAndDHours || 0}h of ${LD_TARGET}h. Log the missing training first.` }); return }
    setIncrements(is => is.map(i => i.id === inc.id ? { ...i, status: 'proposed' } : i))
    setMsg({ type: 'success', text: 'Done' })
  }

  function approveIncrement(inc) {
    setIncrements(is => is.map(i => i.id === inc.id ? { ...i, status: 'md_approved' } : i))
    setEmployees(es => es.map(e => e.id === inc.employeeId ? { ...e, basicSalary: inc.proposedSalary } : e))
    setMsg({ type: 'success', text: 'Done' })
  }

  function rejectIncrement(inc) {
    setIncrements(is => is.map(i => i.id === inc.id ? { ...i, status: 'rejected' } : i))
    setMsg({ type: 'success', text: 'Done' })
  }

  function createDisciplinary(form) {
    const caseNo = `DISC-${String(disciplinary.length + 1).padStart(3, '0')}`
    setDisciplinary(ds => [...ds, { id: caseNo, caseNo, employeeId: form.employeeId, incidentDesc: form.incidentDesc, stage: 'incident', status: 'open', steps: [{ step: 'incident', notes: form.incidentDesc, signedBy: CURRENT_USER, sig: `LT-DS-${caseNo}`, createdAt: new Date().toISOString() }] }])
    setModal(null)
    setMsg({ type: 'success', text: `Case ${caseNo} opened` })
  }

  function advanceDisciplinary(caseObj, step, notes) {
    const isOutcome = step === 'outcome'
    setDisciplinary(ds => ds.map(d => d.id !== caseObj.id ? d : {
      ...d, stage: step, status: isOutcome ? 'closed' : 'open',
      steps: [...d.steps, { step, notes, signedBy: CURRENT_USER, sig: `LT-DS-${caseObj.caseNo}-${step}`, createdAt: new Date().toISOString() }],
    }))
    setDiscDetailFor(null)
    setMsg({ type: 'success', text: `${step} recorded${isOutcome ? ' — case closed' : ''}` })
  }

  function reviewManager(appraisalId) {
    const score = reviewScores[appraisalId]?.score
    if (!score) return
    setAppraisals(as => as.map(a => a.id === appraisalId ? { ...a, managerScore: Number(score), status: 'pending_hr' } : a))
  }

  function reviewHr(appraisalId) {
    const appraisal = appraisals.find(a => a.id === appraisalId)
    const hrComments = reviewScores[appraisalId]?.hrComments || ''
    setAppraisals(as => as.map(a => a.id === appraisalId ? { ...a, status: 'closed', hrComments } : a))

    if (appraisal.managerScore != null && appraisal.managerScore < WARNING_SCORE) {
      const history = [...appraisals.filter(a => a.employeeId === appraisal.employeeId && a.status === 'closed' && a.period < appraisal.period), { ...appraisal, status: 'closed' }]
        .sort((a, b) => b.period.localeCompare(a.period))
      let streak = 0
      for (const h of history) { if (h.managerScore != null && h.managerScore < WARNING_SCORE) streak++; else break }
      const level = quoteLevel(streak)
      setWarnings(ws => [...ws, { id: `warn_${ws.length + 1}`, employeeId: appraisal.employeeId, level, reason: `Manager score ${appraisal.managerScore} < ${WARNING_SCORE} (${streak} consecutive low-scoring month(s))`, triggerPeriod: appraisal.period, issuedAt: new Date().toISOString(), resolved: false }])
      setMsg({ type: 'error', text: `Escalated to ${level.replace('_', ' ').toUpperCase()} (${streak} consecutive low-scoring month(s))` })
    } else {
      setMsg({ type: 'success', text: 'Appraisal reviewed' })
    }
  }

  function resolveWarning(id) {
    setWarnings(ws => ws.map(w => w.id === id ? { ...w, resolved: true } : w))
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        <Tabs tabs={[
          { id: 'employees', label: 'Employees' },
          { id: 'positions', label: 'Positions' },
          { id: 'orgchart', label: 'Org Chart' },
          { id: 'probation', label: 'Probation' },
          { id: 'contracts', label: 'Contracts' },
          { id: 'leave', label: 'Leave' },
          { id: 'entitlements', label: 'Entitlements' },
          { id: 'leavetypes', label: 'Leave Types' },
          { id: 'carryforward', label: 'Carry-Forward' },
          { id: 'attendance', label: 'Attendance' },
          { id: 'absences', label: 'Absences' },
          { id: 'attreports', label: 'Attendance Reports' },
          { id: 'workingtime', label: 'Working Time' },
          { id: 'payrollsetup', label: 'Payroll Setup' },
          { id: 'structures', label: 'Salary Structures' },
          { id: 'salaries', label: 'Salaries' },
          { id: 'statutory', label: 'Statutory Rates' },
          { id: 'deductions', label: 'Deductions' },
          { id: 'overtime', label: 'Overtime' },
          { id: 'runs', label: 'Payroll Runs' },
          { id: 'payslips', label: 'Payslips & P9' },
          { id: 'increments', label: 'Salary Increments' },
          { id: 'ldp', label: 'Learning Plans' },
          { id: 'training', label: 'Training & Hours' },
          { id: 'compliance', label: 'Mandatory Training' },
          { id: 'knowledge', label: 'Knowledge Sharing' },
          { id: 'kpi', label: 'KPI Scorecards' },
          { id: 'appraisals', label: 'Appraisals' },
          { id: 'pips', label: 'Improvement Plans' },
          { id: 'discipline', label: 'Discipline' },
          { id: 'grievances', label: 'Grievances' },
          { id: 'separations', label: 'Separations' },
          { id: 'commissionplans', label: 'Commission Plans' },
          { id: 'commission', label: 'Commission Statements' },
          { id: 'requisitions', label: 'Requisitions' },
          { id: 'vacancies', label: 'Vacancies' },
          { id: 'applicants', label: 'Applicants' },
          { id: 'offers', label: 'Job Offers' },
        ]} active={tab} setActive={setTab} />

        {tabLoading ? <Loading /> : (
          <>
            {tab === 'employees' && <EmployeesTab flash={hrFlash} />}
            {tab === 'positions' && <PositionsTab flash={hrFlash} />}
            {tab === 'orgchart' && <OrgChartTab flash={hrFlash} />}
            {tab === 'probation' && <ProbationTab flash={hrFlash} />}
            {tab === 'contracts' && <ContractsTab flash={hrFlash} />}
            {tab === 'attendance' && <AttendanceTab flash={hrFlash} />}
            {tab === 'absences' && <AbsencesTab flash={hrFlash} />}
            {tab === 'attreports' && <AttendanceReportsTab />}
            {tab === 'workingtime' && <WorkingTimeTab flash={hrFlash} />}

            {tab === 'leave' && <LeaveRequestsTab flash={hrFlash} />}
            {tab === 'entitlements' && <LeaveEntitlementsTab flash={hrFlash} />}
            {tab === 'leavetypes' && <LeaveTypesTab flash={hrFlash} />}
            {tab === 'carryforward' && <CarryForwardTab flash={hrFlash} />}

            {tab === 'payrollsetup' && <PayrollSetupTab flash={hrFlash} />}
            {tab === 'structures' && <SalaryStructuresTab flash={hrFlash} />}
            {tab === 'salaries' && <EmployeeSalariesTab flash={hrFlash} />}
            {tab === 'statutory' && <StatutoryRatesTab flash={hrFlash} />}
            {tab === 'deductions' && <PayrollDeductionsTab flash={hrFlash} />}
            {tab === 'overtime' && <OvertimeTab flash={hrFlash} />}
            {tab === 'runs' && <PayrollRunsTab flash={hrFlash} />}
            {tab === 'payslips' && <PayslipsTab flash={hrFlash} />}
            {tab === 'increments' && <SalaryIncrementsTab flash={hrFlash} />}
            {tab === 'kpi' && <ScorecardsTab flash={hrFlash} />}
            {tab === 'appraisals' && <AppraisalsTab flash={hrFlash} />}
            {tab === 'pips' && <PipsTab flash={hrFlash} />}
            {tab === 'discipline' && <DisciplinaryTab flash={hrFlash} />}
            {tab === 'grievances' && <GrievancesTab flash={hrFlash} />}
            {tab === 'separations' && <SeparationsTab flash={hrFlash} />}
            {tab === 'commissionplans' && <CommissionPlansTab flash={hrFlash} />}
            {tab === 'commission' && <CommissionStatementsTab flash={hrFlash} />}

            {tab === 'ldp' && <LearningPlansTab flash={hrFlash} />}
            {tab === 'training' && <TrainingTab flash={hrFlash} />}
            {tab === 'compliance' && <ComplianceTab flash={hrFlash} />}
            {tab === 'knowledge' && <KnowledgeSharingTab flash={hrFlash} />}






            {tab === 'requisitions' && <RequisitionsTab flash={hrFlash} />}
            {tab === 'vacancies' && <VacanciesTab flash={hrFlash} />}
            {tab === 'applicants' && <ApplicantsTab flash={hrFlash} />}
            {tab === 'offers' && <OffersTab flash={hrFlash} />}
          </>
        )}

        {modal === 'emp' && <NewEmployeeModal onClose={() => setModal(null)} onSubmit={createEmployee} />}

        {editEmpFor && <EditEmployeeModal employee={editEmpFor} onClose={() => setEditEmpFor(null)} onSubmit={saveEmployee} />}
        {modal === 'increment' && <IncrementModal employees={activeEmployees} onClose={() => setModal(null)} onSubmit={proposeIncrement} />}
        {modal === 'disciplinary' && <DisciplinaryModal employees={activeEmployees} onClose={() => setModal(null)} onSubmit={createDisciplinary} />}
        {modal === 'cpd' && <CpdModal employees={activeEmployees} onClose={() => setModal(null)} onSubmit={logCpd} />}

        {cpdLogDetailFor && (
          <Modal title={`CPD Log — ${empName(cpdLogDetailFor)}`} onClose={() => setCpdLogDetailFor(null)} width={620}>
            <DataTable headers={['Date', 'Activity', 'Provider', 'Points', '']} empty="No CPD activity logged yet."
              rows={cpdLogs.filter(l => l.employeeId === cpdLogDetailFor).map(l => [
                fmt.date(l.dateCompleted), l.activity, l.provider || '—', l.points,
                l.verificationUrl ? <a href={l.verificationUrl} target="_blank" rel="noopener noreferrer" style={{ fontSize: 11, color: T.blue }}>✓ Verify</a> : <span style={{ fontSize: 11, color: T.mgrey }}>No link</span>,
              ])} />
          </Modal>
        )}

        {discDetail && (
          <Modal title={`${discDetail.caseNo} — ${empName(discDetail.employeeId)}`} onClose={() => setDiscDetailFor(null)} width={560}>
            <div style={{ fontSize: 12, color: T.mgrey, marginBottom: 8 }}>{discDetail.incidentDesc}</div>
            <div style={{ display: 'flex', gap: 4, marginBottom: 12, flexWrap: 'wrap' }}>
              {DISC_STAGES.map(s => <Badge key={s} variant={discDetail.steps.some(st => st.step === s) ? 'green' : discDetail.stage === s ? 'amber' : 'default'}>{s}</Badge>)}
            </div>
            {discDetail.steps.map((s, i) => (
              <div key={i} style={{ padding: '7px 0', borderBottom: `1px solid ${T.offwt}`, fontSize: 12 }}>
                <strong style={{ textTransform: 'capitalize' }}>{s.step.replace('_', ' ')}</strong> {s.sig && <span style={{ color: T.green }}>🔐</span>} <span style={{ color: T.mgrey }}>· {fmt.date(s.createdAt)} · {s.signedBy}</span>
                {s.notes && <div style={{ color: T.dgrey, marginTop: 2 }}>{s.notes}</div>}
              </div>
            ))}
            {discDetail.status !== 'closed' && advForm.step && (
              <div style={{ marginTop: 14, padding: 10, background: T.offwt, borderRadius: 8 }}>
                <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 6 }}>Next step: <span style={{ textTransform: 'capitalize' }}>{advForm.step.replace('_', ' ')}</span></div>
                <Input label="Notes" value={advForm.notes} onChange={v => setAdvForm({ ...advForm, notes: v })} />
                <Btn size="sm" onClick={() => advanceDisciplinary(discDetail, advForm.step, advForm.notes)} style={{ marginTop: 6 }}>Record & Sign {advForm.step.replace('_', ' ')}</Btn>
              </div>
            )}
          </Modal>
        )}
      </div>
    </>
  )
}

function NewEmployeeModal({ onClose, onSubmit }) {
  const [f, setF] = useState({ firstName: '', lastName: '', email: '', department: DEPARTMENTS[0], role: '', basicSalary: '' })
  const canSubmit = f.firstName.trim().length > 0 && f.lastName.trim().length > 0 && f.email.trim().length > 0 && f.role.trim().length > 0
  return (
    <Modal title="Add New Employee" onClose={onClose}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="First Name" value={f.firstName} onChange={v => setF({ ...f, firstName: v })} required />
        <Input label="Last Name" value={f.lastName} onChange={v => setF({ ...f, lastName: v })} required />
      </div>
      <Input label="Email" type="email" value={f.email} onChange={v => setF({ ...f, email: v })} required />
      <Select label="Department" value={f.department} onChange={v => setF({ ...f, department: v })} options={DEPARTMENTS} />
      <Input label="Role / Job Title" value={f.role} onChange={v => setF({ ...f, role: v })} required />
      <Input label="Basic Salary (Kshs/month)" type="number" value={f.basicSalary} onChange={v => setF({ ...f, basicSalary: v })} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Save Employee</Btn>
      </div>
    </Modal>
  )
}

function EditEmployeeModal({ employee, onClose, onSubmit }) {
  const [f, setF] = useState({ ...employee })
  const canSubmit = f.firstName.trim().length > 0 && f.lastName.trim().length > 0 && f.email.trim().length > 0 && f.role.trim().length > 0
  return (
    <Modal title={`Edit Employee — ${employee.firstName} ${employee.lastName}`} onClose={onClose}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="First Name" value={f.firstName} onChange={v => setF({ ...f, firstName: v })} required />
        <Input label="Last Name" value={f.lastName} onChange={v => setF({ ...f, lastName: v })} required />
      </div>
      <Input label="Email" type="email" value={f.email} onChange={v => setF({ ...f, email: v })} required />
      <Select label="Department" value={f.department} onChange={v => setF({ ...f, department: v })} options={DEPARTMENTS} />
      <Input label="Role / Job Title" value={f.role} onChange={v => setF({ ...f, role: v })} required />
      <Input label="Basic Salary (Kshs/month)" type="number" value={f.basicSalary} onChange={v => setF({ ...f, basicSalary: Number(v) })} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Save Changes</Btn>
      </div>
    </Modal>
  )
}

function IncrementModal({ employees, onClose, onSubmit }) {
  const [f, setF] = useState({ employeeId: '', proposedSalary: '', effectiveMonth: '2026-09', reason: '' })
  const canSubmit = !!f.employeeId && f.proposedSalary !== ''
  return (
    <Modal title="Propose Salary Increment (HR-013)" onClose={onClose} width={500}>
      <Alert type="info">Auto-blocked if the employee's L&D is below 40h (HR-026); the HR Head must clear it before MD approval.</Alert>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })} required
        options={[{ value: '', label: 'Select...' }, ...employees.map(e => ({ value: e.id, label: `${e.firstName} ${e.lastName} — ${fmt.kes(e.basicSalary)} · L&D ${e.lAndDHours || 0}h` }))]} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Proposed Salary" type="number" value={f.proposedSalary} onChange={v => setF({ ...f, proposedSalary: v })} required />
        <Input label="Effective Month" value={f.effectiveMonth} onChange={v => setF({ ...f, effectiveMonth: v })} placeholder="YYYY-MM" />
      </div>
      <Input label="Reason" value={f.reason} onChange={v => setF({ ...f, reason: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Propose</Btn>
      </div>
    </Modal>
  )
}

function DisciplinaryModal({ employees, onClose, onSubmit }) {
  const [f, setF] = useState({ employeeId: '', incidentDesc: '' })
  const canSubmit = !!f.employeeId && f.incidentDesc.trim().length > 0
  return (
    <Modal title="New Disciplinary Case (HR-020)" onClose={onClose} width={500}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })} required
        options={[{ value: '', label: 'Select...' }, ...employees.map(e => ({ value: e.id, label: `${e.firstName} ${e.lastName}` }))]} />
      <Input label="Incident Description" value={f.incidentDesc} onChange={v => setF({ ...f, incidentDesc: v })} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Open Case</Btn>
      </div>
    </Modal>
  )
}

function CpdModal({ employees, onClose, onSubmit }) {
  const [f, setF] = useState({ employeeId: '', platformId: '', activity: '', provider: '', points: '', dateCompleted: '', verificationUrl: '' })
  const canSubmit = !!f.employeeId && f.activity.trim().length > 0 && f.points !== ''
  return (
    <Modal title="Log CPD Activity" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })} required
        options={[{ value: '', label: 'Select...' }, ...employees.map(e => ({ value: e.id, label: `${e.firstName} ${e.lastName}` }))]} />
      <Select label="Platform" value={f.platformId} onChange={v => setF({ ...f, platformId: v })}
        options={[{ value: '', label: 'Other / not listed' }, ...CPD_PLATFORMS.map(p => ({ value: p.id, label: p.name }))]} />
      <Input label="Activity / Course Title" value={f.activity} onChange={v => setF({ ...f, activity: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Provider" value={f.provider} onChange={v => setF({ ...f, provider: v })} />
        <Input label="Points" type="number" value={f.points} onChange={v => setF({ ...f, points: v })} required />
      </div>
      <Input label="Date Completed" type="date" value={f.dateCompleted} onChange={v => setF({ ...f, dateCompleted: v })} />
      <Input label="Verification Link" value={f.verificationUrl} onChange={v => setF({ ...f, verificationUrl: v })}
        placeholder="e.g. Alison Learner Achievement Verification URL, Coursera Verify Certificate link"
        note="Most free platforms issue a public link to verify the certificate is genuine — paste it here so HR/managers can check it." />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Log Activity</Btn>
      </div>
    </Modal>
  )
}



