// HR & Payroll API service (Module 3) — the single home for HR endpoint strings.
// All HR endpoints are namespaced under /api/v1/hr/*. Mirrors services/procurement.js.
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data
const B = '/api/v1/hr'

// ── H1: employee master (HR-001/002/004) ──
// List returns the full paged body { data, total, page, pageSize, pages }; detail/actions unwrap to data.
export const listEmployees     = (params) => api.get(`${B}/employees`, { params }).then(r => r.data)
export const getEmployee       = (id) => api.get(`${B}/employees/${id}`).then(unwrap)
export const getEmployeeByUser = (userId) => api.get(`${B}/employees/by-user/${userId}`).then(unwrap).catch(() => null)
export const employeeSummary   = () => api.get(`${B}/employees/summary`).then(unwrap)
export const createEmployee    = (dto) => api.post(`${B}/employees`, dto).then(unwrap)
export const updateEmployee    = (id, dto) => api.put(`${B}/employees/${id}`, dto).then(unwrap)
export const createUserAccount = (id, dto) => api.post(`${B}/employees/${id}/user-account`, dto ?? {}).then(unwrap)

// Onboarding sub-records (P1 steps 1.3, 1.4, 1.6)
export const addEmergencyContact  = (id, dto) => api.post(`${B}/employees/${id}/emergency-contacts`, dto).then(unwrap)
export const addEducation         = (id, dto) => api.post(`${B}/employees/${id}/education`, dto).then(unwrap)
export const addEmploymentHistory = (id, dto) => api.post(`${B}/employees/${id}/employment-history`, dto).then(unwrap)
export const addBankDetail        = (id, dto) => api.post(`${B}/employees/${id}/bank-details`, dto).then(unwrap)
export const removeSubRecord      = (kind, recordId) => api.delete(`${B}/employees/${kind}/${recordId}`).then(unwrap)

// Document vault (P2) — uploaded unverified, then confirmed by a SECOND officer
export const uploadDocument       = (id, dto) => api.post(`${B}/employees/${id}/documents`, dto).then(unwrap)
export const verifyDocument       = (documentId, dto) => api.post(`${B}/employees/documents/${documentId}/verify`, dto ?? {}).then(unwrap)
export const documentsAwaitingVerification = () => api.get(`${B}/employees/documents/awaiting-verification`).then(unwrap)

// Professional certifications (P2 step 2.4, HR-028/035)
export const addCertification      = (id, dto) => api.post(`${B}/employees/${id}/certifications`, dto).then(unwrap)
export const expiringCertifications = (withinDays = 30) => api.get(`${B}/employees/certifications/expiring`, { params: { withinDays } }).then(unwrap)

// ── H1: positions + org chart (HR-DEC-3, HR-005/P32) ──
export const listPositions   = (includeInactive = false) => api.get(`${B}/positions`, { params: { includeInactive } }).then(unwrap)
export const createPosition  = (dto) => api.post(`${B}/positions`, dto).then(unwrap)
export const updatePosition  = (id, dto) => api.put(`${B}/positions/${id}`, dto).then(unwrap)
export const getOrgChart     = (includeInactive = false) => api.get(`${B}/org-chart`, { params: { includeInactive } }).then(unwrap)
export const setReportingLine = (employeeId, dto) => api.put(`${B}/org-chart/${employeeId}/reporting-line`, dto).then(unwrap)
export const rebuildOrgChart = () => api.post(`${B}/org-chart/rebuild`).then(unwrap)

// Departments and branches are owned by user-service and read through HR (HR-DEC-3)
export const listDepartments = () => api.get(`${B}/departments`).then(unwrap)
export const listBranches    = () => api.get(`${B}/branches`).then(unwrap)

// ── H2: probation milestones + fixed-term contract renewal (HR-003/006, P3 + P33) ──
// The daily sweep raises the reviews and expiry warnings; the outcomes below are human decisions.
export const probationSummary   = () => api.get(`${B}/probation/summary`).then(unwrap)
export const listProbationReviews = (params) => api.get(`${B}/probation/reviews`, { params }).then(unwrap)
export const recordProbationOutcome = (reviewId, dto) => api.post(`${B}/probation/reviews/${reviewId}/outcome`, dto).then(unwrap)

export const listContractAlerts  = (outcome) => api.get(`${B}/contracts/alerts`, { params: { outcome: outcome || undefined } }).then(unwrap)
export const renewContract       = (alertId, dto) => api.post(`${B}/contracts/alerts/${alertId}/renew`, dto).then(unwrap)
export const convertToPermanent  = (alertId, dto) => api.post(`${B}/contracts/alerts/${alertId}/convert-to-permanent`, dto ?? {}).then(unwrap)
export const letContractExpire   = (alertId, dto) => api.post(`${B}/contracts/alerts/${alertId}/let-expire`, dto ?? {}).then(unwrap)

// Runs the milestone sweep now rather than waiting for the daily tick. Idempotent.
export const runMilestoneSweep   = () => api.post(`${B}/milestones/sweep`).then(unwrap)

// ── H3: leave types, entitlements, applications, carry-forward (HR-005, P4/P5/P6) ──
export const leaveSummary      = () => api.get(`${B}/leave/summary`).then(unwrap)

export const listLeaveTypes    = (includeInactive = false) => api.get(`${B}/leave/types`, { params: { includeInactive } }).then(unwrap)
export const createLeaveType   = (dto) => api.post(`${B}/leave/types`, dto).then(unwrap)
export const updateLeaveType   = (id, dto) => api.put(`${B}/leave/types/${id}`, dto).then(unwrap)
export const seedLeaveTypes    = () => api.post(`${B}/leave/types/seed-defaults`).then(unwrap)

export const listEntitlements  = (params) => api.get(`${B}/leave/entitlements`, { params }).then(unwrap)
export const assignEntitlements = (year, employeeId) => api.post(`${B}/leave/entitlements/assign`, null, { params: { year, employeeId } }).then(unwrap)
export const adjustEntitlement = (id, dto) => api.post(`${B}/leave/entitlements/${id}/adjust`, dto).then(unwrap)

export const listLeaveRequests = (params) => api.get(`${B}/leave/requests`, { params }).then(unwrap)
export const getLeaveRequest   = (id) => api.get(`${B}/leave/requests/${id}`).then(unwrap)
// Dry-run: day count, balance, document rule and approval chain before anything is filed.
export const previewLeave      = (dto) => api.post(`${B}/leave/requests/preview`, dto).then(unwrap)
export const createLeaveRequest = (dto) => api.post(`${B}/leave/requests`, dto).then(unwrap)
export const decideLeave       = (id, dto) => api.post(`${B}/leave/requests/${id}/decide`, dto).then(unwrap)
export const cancelLeave       = (id, dto) => api.post(`${B}/leave/requests/${id}/cancel`, dto ?? {}).then(unwrap)
export const attachLeaveDocument = (id, dto) => api.post(`${B}/leave/requests/${id}/documents`, dto).then(unwrap)

export const listCarryForward  = (params) => api.get(`${B}/leave/carry-forward`, { params }).then(unwrap)
export const runLeaveSweep     = () => api.post(`${B}/leave/sweep`).then(unwrap)

// ── H4: attendance, absences, working time, holidays, scorecards (ATT-001..008, P27/P28) ──
export const attendanceSummary = () => api.get(`${B}/attendance/summary`).then(unwrap)

export const listAttendance    = (params) => api.get(`${B}/attendance/records`, { params }).then(unwrap)
export const clockIn           = (dto) => api.post(`${B}/attendance/clock-in`, dto).then(unwrap)
export const clockOut          = (dto) => api.post(`${B}/attendance/clock-out`, dto).then(unwrap)

export const listAbsences      = (params) => api.get(`${B}/attendance/absences`, { params }).then(unwrap)
export const recordAbsence     = (dto) => api.post(`${B}/attendance/absences`, dto).then(unwrap)
export const excuseAbsence     = (id, dto) => api.post(`${B}/attendance/absences/${id}/excuse`, dto).then(unwrap)
// Unpaid days awaiting payroll — days only; H4 holds no salary data (H6 turns them into money).
export const listUnpaidAbsences = (params) => api.get(`${B}/attendance/unpaid-absences`, { params }).then(unwrap)

export const getAttendanceSettings = () => api.get(`${B}/attendance/settings`).then(unwrap)
export const updateAttendanceSettings = (dto) => api.put(`${B}/attendance/settings`, dto).then(unwrap)

// The holiday calendar also drives H3 leave day counts — a change here changes what leave costs.
export const listHolidays      = (params) => api.get(`${B}/attendance/holidays`, { params }).then(unwrap)
export const createHoliday     = (dto) => api.post(`${B}/attendance/holidays`, dto).then(unwrap)
export const updateHoliday     = (id, dto) => api.put(`${B}/attendance/holidays/${id}`, dto).then(unwrap)
export const deleteHoliday     = (id) => api.delete(`${B}/attendance/holidays/${id}`).then(unwrap)
export const seedKenyanHolidays = (year) => api.post(`${B}/attendance/holidays/seed-kenyan`, null, { params: { year } }).then(unwrap)

export const listScorecards    = (params) => api.get(`${B}/attendance/scorecards`, { params }).then(unwrap)
export const listMonthlyReports = (params) => api.get(`${B}/attendance/monthly-reports`, { params }).then(unwrap)
export const runAttendanceSweep = () => api.post(`${B}/attendance/sweep`).then(unwrap)

// ── H5: pay configuration — grades, structures, periods, salaries, statutory rates, deductions (P7/P8) ──
// Everything here sits behind the ring-fenced hr.payroll.* permissions, so a user with plain hr.read.*
// gets a 403 on these and the Payroll tabs render their "no access" state rather than empty tables.
export const payrollSummary    = () => api.get(`${B}/payroll/summary`).then(unwrap)

export const listJobGrades     = (includeInactive = false) => api.get(`${B}/payroll/grades`, { params: { includeInactive } }).then(unwrap)
export const createJobGrade    = (dto) => api.post(`${B}/payroll/grades`, dto).then(unwrap)
export const updateJobGrade    = (id, dto) => api.put(`${B}/payroll/grades/${id}`, dto).then(unwrap)

export const listStructures    = (includeInactive = false) => api.get(`${B}/payroll/structures`, { params: { includeInactive } }).then(unwrap)
export const getStructure      = (id) => api.get(`${B}/payroll/structures/${id}`).then(unwrap)
export const createStructure   = (dto) => api.post(`${B}/payroll/structures`, dto).then(unwrap)
export const updateStructure   = (id, dto) => api.put(`${B}/payroll/structures/${id}`, dto).then(unwrap)
export const seedStructure     = () => api.post(`${B}/payroll/structures/seed-default`).then(unwrap)

export const addComponent        = (structureId, dto) => api.post(`${B}/payroll/structures/${structureId}/components`, dto).then(unwrap)
export const updateComponent     = (id, dto) => api.put(`${B}/payroll/components/${id}`, dto).then(unwrap)
export const deactivateComponent = (id) => api.post(`${B}/payroll/components/${id}/deactivate`).then(unwrap)

// GL accounts come from finance's chart of accounts through the HR seam — an empty list means
// finance is unreachable, not that there are no accounts.
export const listGlAccounts       = () => api.get(`${B}/payroll/gl-accounts`).then(unwrap)
export const mapComponentAccount  = (id, glAccountId) => api.post(`${B}/payroll/components/${id}/gl-account`, { glAccountId }).then(unwrap)
export const mapStatutoryAccount  = (id, glAccountId) => api.post(`${B}/payroll/statutory-rates/${id}/gl-account`, { glAccountId }).then(unwrap)
export const mapDeductionAccount  = (id, glAccountId) => api.post(`${B}/payroll/deduction-types/${id}/gl-account`, { glAccountId }).then(unwrap)

export const listPayrollPeriods = (year) => api.get(`${B}/payroll/periods`, { params: { year } }).then(unwrap)
export const generatePeriods    = (dto) => api.post(`${B}/payroll/periods/generate`, dto).then(unwrap)

export const listSalaries      = (params) => api.get(`${B}/payroll/salaries`, { params }).then(unwrap)
export const proposeSalary     = (dto) => api.post(`${B}/payroll/salaries`, dto).then(unwrap)
export const decideSalary      = (id, dto) => api.post(`${B}/payroll/salaries/${id}/decide`, dto).then(unwrap)

export const listPayeBands     = (params) => api.get(`${B}/payroll/paye-bands`, { params }).then(unwrap)
export const seedPayeBands     = () => api.post(`${B}/payroll/paye-bands/seed`).then(unwrap)
export const createPayeBand    = (dto) => api.post(`${B}/payroll/paye-bands`, dto).then(unwrap)
export const updatePayeBand    = (id, dto) => api.put(`${B}/payroll/paye-bands/${id}`, dto).then(unwrap)

export const listStatutoryRates = (params) => api.get(`${B}/payroll/statutory-rates`, { params }).then(unwrap)
export const seedStatutoryRates = () => api.post(`${B}/payroll/statutory-rates/seed`).then(unwrap)
export const createStatutoryRate = (dto) => api.post(`${B}/payroll/statutory-rates`, dto).then(unwrap)
export const updateStatutoryRate = (id, dto) => api.put(`${B}/payroll/statutory-rates/${id}`, dto).then(unwrap)
// Signing off the rates the whole payroll runs on needs hr.payroll.approve.
export const confirmRates       = (dto) => api.post(`${B}/payroll/statutory-rates/confirm`, dto ?? {}).then(unwrap)

export const listDeductionTypes  = (includeInactive = false) => api.get(`${B}/payroll/deduction-types`, { params: { includeInactive } }).then(unwrap)
export const seedDeductionTypes  = () => api.post(`${B}/payroll/deduction-types/seed-defaults`).then(unwrap)
export const createDeductionType = (dto) => api.post(`${B}/payroll/deduction-types`, dto).then(unwrap)
export const updateDeductionType = (id, dto) => api.put(`${B}/payroll/deduction-types/${id}`, dto).then(unwrap)

export const listDeductions    = (params) => api.get(`${B}/payroll/deductions`, { params }).then(unwrap)
export const addDeduction      = (dto) => api.post(`${B}/payroll/deductions`, dto).then(unwrap)
export const stopDeduction     = (id, dto) => api.post(`${B}/payroll/deductions/${id}/stop`, dto ?? {}).then(unwrap)

// ── H6: overtime, payroll runs, payslips, bank files, P9 (HR-007/008/009/010/011/012, P9–P12) ──
// Overtime sits on hr.manager (the line manager approves, as with leave); everything to do with a run is on
// the ring-fenced payroll tier, and approving one needs hr.payroll.approve AND a second officer.
export const listOvertime      = (params) => api.get(`${B}/overtime`, { params }).then(unwrap)
export const requestOvertime   = (dto) => api.post(`${B}/overtime`, dto).then(unwrap)
export const decideOvertime    = (id, dto) => api.post(`${B}/overtime/${id}/decide`, dto).then(unwrap)

export const listPayrollRuns   = (status) => api.get(`${B}/payroll/runs`, { params: { status: status || undefined } }).then(unwrap)
export const getPayrollRun     = (id) => api.get(`${B}/payroll/runs/${id}`).then(unwrap)
export const createPayrollRun  = (dto) => api.post(`${B}/payroll/runs`, dto ?? {}).then(unwrap)
// Safe to repeat while unapproved — the server releases what it claimed before rebuilding.
export const computePayrollRun = (id) => api.post(`${B}/payroll/runs/${id}/compute`).then(unwrap)
export const previewPayrollJournal = (id) => api.get(`${B}/payroll/runs/${id}/journal-preview`).then(unwrap)
export const decidePayrollRun  = (id, dto) => api.post(`${B}/payroll/runs/${id}/decide`, dto).then(unwrap)
export const retryPayrollJournal = (id) => api.post(`${B}/payroll/runs/${id}/post-journal`).then(unwrap)

export const listPayslips      = (params) => api.get(`${B}/payroll/payslips`, { params }).then(unwrap)
// PDFs are rendered on demand from the frozen payslip lines — nothing is stored, so these are plain downloads.
export const payslipPdfUrl     = (id) => `${B}/payroll/payslips/${id}/pdf`
export const getP9             = (employeeId, year) => api.get(`${B}/payroll/p9`, { params: { employeeId, year } }).then(unwrap)
export const p9PdfUrl          = (employeeId, year) => `${B}/payroll/p9/pdf?employeeId=${employeeId}&year=${year}`

export const listBankFiles     = (runId) => api.get(`${B}/payroll/bank-files`, { params: { runId: runId || undefined } }).then(unwrap)
export const generateBankFile  = (runId, dto) => api.post(`${B}/payroll/runs/${runId}/bank-file`, dto).then(unwrap)
export const bankFileUrl       = (id) => `${B}/payroll/bank-files/${id}/download`
export const confirmBankFile   = (id, dto) => api.post(`${B}/payroll/bank-files/${id}/confirm`, dto).then(unwrap)

/// Streams an authenticated file download to the browser. The PDF and CSV endpoints need the bearer token,
/// so a bare <a href> would get a 401 — fetch it as a blob and hand it to a temporary link instead.
export const downloadFile = async (url, fallbackName) => {
  const res = await api.get(url, { responseType: 'blob' })
  const match = /filename="?([^"';]+)"?/i.exec(res.headers?.['content-disposition'] ?? '')
  const href = URL.createObjectURL(res.data)
  const a = document.createElement('a')
  a.href = href
  a.download = match?.[1] ?? fallbackName
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(href)
}

// ── H7: learning & development (HR-025..HR-036, P22/P23/P25/P26) ──
// On the general hr.* tier, not the payroll one: training records are not pay data and line managers need to
// see their team's. Compliance evidence for HSE and anti-bribery is read live from hse and compliance — a
// source that cannot be reached reports state "Unknown", which is NOT a failure and does not block anything.
export const learningSummary   = (year) => api.get(`${B}/learning/summary`, { params: { year } }).then(unwrap)

export const listLdps          = (params) => api.get(`${B}/learning/plans`, { params }).then(unwrap)
export const getLdp            = (id) => api.get(`${B}/learning/plans/${id}`).then(unwrap)
export const saveLdp           = (dto) => api.post(`${B}/learning/plans`, dto).then(unwrap)
export const submitLdp         = (id) => api.post(`${B}/learning/plans/${id}/submit`).then(unwrap)
export const decideLdp         = (id, dto) => api.post(`${B}/learning/plans/${id}/decide`, dto).then(unwrap)

export const listTraining      = (params) => api.get(`${B}/learning/training`, { params }).then(unwrap)
export const logTraining       = (dto) => api.post(`${B}/learning/training`, dto).then(unwrap)
export const listTrainingHours = (params) => api.get(`${B}/learning/hours`, { params }).then(unwrap)

export const listRequirements  = (includeInactive = false) => api.get(`${B}/learning/requirements`, { params: { includeInactive } }).then(unwrap)
export const seedRequirements  = () => api.post(`${B}/learning/requirements/seed`).then(unwrap)
export const createRequirement = (dto) => api.post(`${B}/learning/requirements`, dto).then(unwrap)
export const updateRequirement = (id, dto) => api.put(`${B}/learning/requirements/${id}`, dto).then(unwrap)
export const listCompliance    = (employeeId) => api.get(`${B}/learning/compliance`, { params: { employeeId: employeeId || undefined } }).then(unwrap)
// The H8 gate — HR-029 mandatory training + HR-035 professional certification.
export const incrementEligibility = (employeeId) => api.get(`${B}/learning/increment-eligibility`, { params: { employeeId } }).then(unwrap)

export const listKnowledgeSessions = (params) => api.get(`${B}/learning/knowledge-sharing`, { params }).then(unwrap)
export const logKnowledgeSession   = (dto) => api.post(`${B}/learning/knowledge-sharing`, dto).then(unwrap)

export const listLdBudgets     = (year) => api.get(`${B}/learning/budgets`, { params: { year } }).then(unwrap)
export const saveLdBudget      = (dto) => api.post(`${B}/learning/budgets`, dto).then(unwrap)
export const runLearningSweep  = () => api.post(`${B}/learning/sweep`).then(unwrap)

// ── H8: salary increments (HR-013, P13) ──
// Two hard gates live on the server (HR-029 mandatory training, HR-035 professional certification) and are
// re-checked at approval — the UI shows them, it does not enforce them.
export const listIncrements    = (params) => api.get(`${B}/payroll/increments`, { params }).then(unwrap)
export const previewIncrement  = (employeeId) => api.get(`${B}/payroll/increments/preview`, { params: { employeeId } }).then(unwrap)
export const proposeIncrement  = (dto) => api.post(`${B}/payroll/increments`, dto).then(unwrap)
export const decideIncrement   = (id, dto) => api.post(`${B}/payroll/increments/${id}/decide`, dto).then(unwrap)

// ── H9: KPI scorecards, appraisals, 360 feedback, improvement plans (HR-014..020, P14–P17) ──
// The four appraisal steps are enforced IN ORDER by the server, along with the second-officer rules
// (nobody reviews their own self-assessment; nobody signs off their own review). The UI shows whose turn
// it is; it does not decide.
export const appraisalSummary  = (year) => api.get(`${B}/appraisals/summary`, { params: { year } }).then(unwrap)

export const listKpiScorecards = (params) => api.get(`${B}/appraisals/scorecards`, { params }).then(unwrap)
export const getKpiScorecard   = (id) => api.get(`${B}/appraisals/scorecards/${id}`).then(unwrap)
export const createKpiScorecard = (dto) => api.post(`${B}/appraisals/scorecards`, dto).then(unwrap)
export const updateKpiScorecard = (id, dto) => api.put(`${B}/appraisals/scorecards/${id}`, dto).then(unwrap)
export const assignKpiTargets  = (id) => api.post(`${B}/appraisals/scorecards/${id}/assign-targets`).then(unwrap)

export const listKpiTargets    = (params) => api.get(`${B}/appraisals/targets`, { params }).then(unwrap)
export const setKpiTarget      = (dto) => api.post(`${B}/appraisals/targets`, dto).then(unwrap)

export const listAppraisalCycles = (year) => api.get(`${B}/appraisals/cycles`, { params: { year } }).then(unwrap)
export const openAppraisalCycle  = (dto) => api.post(`${B}/appraisals/cycles`, dto).then(unwrap)
export const closeAppraisalCycle = (id) => api.post(`${B}/appraisals/cycles/${id}/close`).then(unwrap)

export const listAppraisals    = (params) => api.get(`${B}/appraisals`, { params }).then(unwrap)
export const getAppraisal      = (id) => api.get(`${B}/appraisals/${id}`).then(unwrap)
export const submitSelfAssessment = (id, dto) => api.post(`${B}/appraisals/${id}/self-assessment`, dto).then(unwrap)
export const reviewAppraisal   = (id, dto) => api.post(`${B}/appraisals/${id}/review`, dto).then(unwrap)
export const signOffAppraisal  = (id, dto) => api.post(`${B}/appraisals/${id}/sign-off`, dto).then(unwrap)
export const recordAppraisal   = (id, dto) => api.post(`${B}/appraisals/${id}/record`, dto).then(unwrap)

// Counts and averages only — individual 360 ratings are anonymous to the employee.
export const getFeedback360    = (id) => api.get(`${B}/appraisals/${id}/feedback-360`).then(unwrap)
export const requestFeedback360 = (id, dto) => api.post(`${B}/appraisals/${id}/feedback-360/request`, dto).then(unwrap)
export const submitFeedback360 = (id, dto) => api.post(`${B}/appraisals/${id}/feedback-360`, dto).then(unwrap)

export const listPips          = (params) => api.get(`${B}/appraisals/pips`, { params }).then(unwrap)
export const updatePip         = (id, dto) => api.put(`${B}/appraisals/pips/${id}`, dto).then(unwrap)
export const closePip          = (id, dto) => api.post(`${B}/appraisals/pips/${id}/close`, dto).then(unwrap)

// ── H10: discipline, warnings, grievances, separation (HR-021..024, P18–P21) ──
// Case stages are enforced in order by the server, with second-officer rules on the outcome and the appeal.
// Final dues sit on the ring-fenced payroll tier — they are pay data.
export const disciplineSummary = () => api.get(`${B}/discipline/summary`).then(unwrap)

export const listCases         = (params) => api.get(`${B}/discipline/cases`, { params }).then(unwrap)
export const getCase           = (id) => api.get(`${B}/discipline/cases/${id}`).then(unwrap)
export const openCase          = (dto) => api.post(`${B}/discipline/cases`, dto).then(unwrap)
export const issueShowCause    = (id, dto) => api.post(`${B}/discipline/cases/${id}/show-cause`, dto ?? {}).then(unwrap)
export const recordCaseResponse = (id, dto) => api.post(`${B}/discipline/cases/${id}/response`, dto).then(unwrap)
export const recordCaseOutcome = (id, dto) => api.post(`${B}/discipline/cases/${id}/outcome`, dto).then(unwrap)
export const recordCaseAppeal  = (id, dto) => api.post(`${B}/discipline/cases/${id}/appeal`, dto).then(unwrap)
export const closeCase         = (id, reason) => api.post(`${B}/discipline/cases/${id}/close`, { decision: reason }).then(unwrap)

export const listWarnings      = (params) => api.get(`${B}/discipline/warnings`, { params }).then(unwrap)
export const issueWarning      = (dto) => api.post(`${B}/discipline/warnings`, dto).then(unwrap)
export const acknowledgeWarning = (id, dto) => api.post(`${B}/discipline/warnings/${id}/acknowledge`, dto ?? {}).then(unwrap)

export const listGrievances    = (params) => api.get(`${B}/discipline/grievances`, { params }).then(unwrap)
export const submitGrievance   = (dto) => api.post(`${B}/discipline/grievances`, dto).then(unwrap)
export const acknowledgeGrievance = (id) => api.post(`${B}/discipline/grievances/${id}/acknowledge`).then(unwrap)
export const assignGrievance   = (id, dto) => api.post(`${B}/discipline/grievances/${id}/assign`, dto).then(unwrap)
export const resolveGrievance  = (id, dto) => api.post(`${B}/discipline/grievances/${id}/resolve`, dto).then(unwrap)

export const listSeparations   = (params) => api.get(`${B}/discipline/separations`, { params }).then(unwrap)
export const getSeparation     = (id) => api.get(`${B}/discipline/separations/${id}`).then(unwrap)
export const initiateSeparation = (dto) => api.post(`${B}/discipline/separations`, dto).then(unwrap)
export const adjustDues        = (id, dto) => api.put(`${B}/discipline/separations/${id}/dues`, dto).then(unwrap)
export const submitSeparation  = (id) => api.post(`${B}/discipline/separations/${id}/submit`).then(unwrap)
export const decideSeparation  = (id, dto) => api.post(`${B}/discipline/separations/${id}/decide`, dto).then(unwrap)
export const paySeparation     = (id, dto) => api.post(`${B}/discipline/separations/${id}/pay`, dto ?? {}).then(unwrap)
export const runDisciplineSweep = () => api.post(`${B}/discipline/sweep`).then(unwrap)

// ── H11: sales commission (COM-001..009, P29–P31) ──
// H11-DEC-1: CRM owns the revenue target AND the attainment; HR owns the bands, the MD's approval of the
// basis, and the quarterly split. Nothing here writes a target. When CRM cannot be read the API says so
// rather than returning zeros — the UI must relay that, not paper over it.
export const commissionSummary = (year) => api.get(`${B}/commission/summary`, { params: { year } }).then(unwrap)

export const listCommissionBands = (includeInactive = false) => api.get(`${B}/commission/bands`, { params: { includeInactive } }).then(unwrap)
export const seedCommissionBands = () => api.post(`${B}/commission/bands/seed`).then(unwrap)
export const createCommissionBand = (dto) => api.post(`${B}/commission/bands`, dto).then(unwrap)
export const updateCommissionBand = (id, dto) => api.put(`${B}/commission/bands/${id}`, dto).then(unwrap)

export const listCommissionPlans = (params) => api.get(`${B}/commission/plans`, { params }).then(unwrap)
export const saveCommissionPlan = (dto) => api.post(`${B}/commission/plans`, dto).then(unwrap)
export const submitCommissionPlan = (id) => api.post(`${B}/commission/plans/${id}/submit`).then(unwrap)
export const decideCommissionPlan = (id, dto) => api.post(`${B}/commission/plans/${id}/decide`, dto).then(unwrap)

export const listCommissionStatements = (params) => api.get(`${B}/commission/statements`, { params }).then(unwrap)
export const computeCommissionStatements = (dto) => api.post(`${B}/commission/statements/compute`, dto ?? {}).then(unwrap)
export const decideCommissionStatement = (id, dto) => api.post(`${B}/commission/statements/${id}/decide`, dto).then(unwrap)
// COM-004 — the same quarterly issue the daily worker makes, on demand.
export const runCommissionSweep = () => api.post(`${B}/commission/sweep`).then(unwrap)

export const listCommissionDisputes = (status) => api.get(`${B}/commission/disputes`, { params: { status: status || undefined } }).then(unwrap)
export const raiseCommissionDispute = (statementId, dto) => api.post(`${B}/commission/statements/${statementId}/dispute`, dto).then(unwrap)
export const resolveCommissionDispute = (id, dto) => api.post(`${B}/commission/disputes/${id}/resolve`, dto).then(unwrap)

// ── H12: recruitment (P1 entry point — requisition → vacancy → applicant → interview → offer → hire) ──
// The order is the control, and the server enforces it: nothing is advertised without an approved
// requisition, nobody is interviewed unscreened, no offer goes out without a panel recommendation, and
// only an ACCEPTED offer can become an employee. The UI offers the step a record is actually on rather
// than every button at once — `nextStep` on each DTO is the server's own answer to "what now".
export const recruitmentSummary = () => api.get(`${B}/recruitment/summary`).then(unwrap)
export const sourceEffectiveness = (year) => api.get(`${B}/recruitment/source-effectiveness`, { params: { year } }).then(unwrap)

export const listRequisitions  = (params) => api.get(`${B}/recruitment/requisitions`, { params }).then(unwrap)
export const getRequisition    = (id) => api.get(`${B}/recruitment/requisitions/${id}`).then(unwrap)
export const raiseRequisition  = (dto) => api.post(`${B}/recruitment/requisitions`, dto).then(unwrap)
export const submitRequisition = (id) => api.post(`${B}/recruitment/requisitions/${id}/submit`).then(unwrap)
// hr.approve, and the server also refuses whoever raised it — segregation is on identity, not permission.
export const decideRequisition = (id, dto) => api.post(`${B}/recruitment/requisitions/${id}/decide`, dto).then(unwrap)
export const cancelRequisition = (id, dto) => api.post(`${B}/recruitment/requisitions/${id}/cancel`, dto ?? {}).then(unwrap)

export const listVacancies     = (params) => api.get(`${B}/recruitment/vacancies`, { params }).then(unwrap)
export const getVacancy        = (id) => api.get(`${B}/recruitment/vacancies/${id}`).then(unwrap)
export const postVacancy       = (dto) => api.post(`${B}/recruitment/vacancies`, dto).then(unwrap)
export const closeVacancy      = (id, dto) => api.post(`${B}/recruitment/vacancies/${id}/close`, dto ?? {}).then(unwrap)

export const listApplicants    = (params) => api.get(`${B}/recruitment/applicants`, { params }).then(unwrap)
export const getApplicant      = (id) => api.get(`${B}/recruitment/applicants/${id}`).then(unwrap)
export const receiveApplication = (dto) => api.post(`${B}/recruitment/applicants`, dto).then(unwrap)
export const screenApplicant   = (id, dto) => api.post(`${B}/recruitment/applicants/${id}/screen`, dto).then(unwrap)
export const rejectApplicant   = (id, dto) => api.post(`${B}/recruitment/applicants/${id}/reject`, dto).then(unwrap)
export const withdrawApplicant = (id, dto) => api.post(`${B}/recruitment/applicants/${id}/withdraw`, dto ?? {}).then(unwrap)
// Creates the H1 employee record from the accepted offer.
export const hireApplicant     = (id, dto) => api.post(`${B}/recruitment/applicants/${id}/hire`, dto ?? {}).then(unwrap)

export const listInterviews    = (params) => api.get(`${B}/recruitment/interviews`, { params }).then(unwrap)
export const scheduleInterview = (dto) => api.post(`${B}/recruitment/interviews`, dto).then(unwrap)
export const scoreInterview    = (id, dto) => api.post(`${B}/recruitment/interviews/${id}/score`, dto).then(unwrap)
export const cancelInterview   = (id, dto) => api.post(`${B}/recruitment/interviews/${id}/cancel`, dto ?? {}).then(unwrap)

export const listOffers        = (params) => api.get(`${B}/recruitment/offers`, { params }).then(unwrap)
export const getOffer          = (id) => api.get(`${B}/recruitment/offers/${id}`).then(unwrap)
export const prepareOffer      = (dto) => api.post(`${B}/recruitment/offers`, dto).then(unwrap)
export const submitOffer       = (id) => api.post(`${B}/recruitment/offers/${id}/submit`).then(unwrap)
// hr.approve; the preparer cannot approve their own offer.
export const decideOffer       = (id, dto) => api.post(`${B}/recruitment/offers/${id}/decide`, dto).then(unwrap)
export const issueOffer        = (id) => api.post(`${B}/recruitment/offers/${id}/issue`).then(unwrap)
export const respondToOffer    = (id, dto) => api.post(`${B}/recruitment/offers/${id}/respond`, dto).then(unwrap)
export const withdrawOffer     = (id, dto) => api.post(`${B}/recruitment/offers/${id}/withdraw`, dto).then(unwrap)

// Closes vacancies past their closing date and lapses offers nobody answered. Same sweep the daily worker runs.
export const runRecruitmentSweep = () => api.post(`${B}/recruitment/sweep`).then(unwrap)
