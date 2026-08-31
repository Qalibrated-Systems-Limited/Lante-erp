// Operations API service — the single home for operations/projects endpoint strings.
// Each fn returns the unwrapped `data` payload (ApiResponse<T>.data), mirroring services/finance.js
// and services/ticketing.js. NOTE: fleet / field-vehicle endpoints are intentionally EXCLUDED here —
// fleet is owned by a separate effort; those pages keep their own calls to avoid conflicts.
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

// ── Projects ──
export const listProjects   = (params) => api.get('/api/v1/projects', { params }).then(unwrap)
export const getProject     = (id) => api.get(`/api/v1/projects/${id}`).then(unwrap)
export const createProject  = (dto) => api.post('/api/v1/projects', dto).then(unwrap)
export const updateProject  = (id, dto) => api.put(`/api/v1/projects/${id}`, dto).then(unwrap)
export const submitProject  = (id) => api.post(`/api/v1/projects/${id}/submit`).then(unwrap)
export const approveProjectMd      = (id, dto) => api.post(`/api/v1/projects/${id}/approve/md`, dto).then(unwrap)
export const approveProjectFinance = (id, dto) => api.post(`/api/v1/projects/${id}/approve/finance`, dto).then(unwrap)
export const holdProject    = (id, dto) => api.post(`/api/v1/projects/${id}/hold`, dto).then(unwrap)
export const resumeProject  = (id) => api.post(`/api/v1/projects/${id}/resume`).then(unwrap)
export const closeProject   = (id, dto) => api.post(`/api/v1/projects/${id}/close`, dto).then(unwrap)

// Project → approvals / history / daily reports
export const createProjectApproval  = (id, dto) => api.post(`/api/v1/projects/${id}/approvals`, dto).then(unwrap)
export const processProjectApproval = (id, approvalId, dto) => api.put(`/api/v1/projects/${id}/approvals/${approvalId}/process`, dto).then(unwrap)
export const getProjectHistory      = (id) => api.get(`/api/v1/projects/${id}/history`).then(unwrap)
export const getProjectDailyReports = (id) => api.get(`/api/v1/projects/${id}/daily-reports`).then(unwrap)
export const addProjectDailyReport  = (id, dto) => api.post(`/api/v1/projects/${id}/daily-reports`, dto).then(unwrap)

// Project → milestones + tasks
export const getMilestones   = (id) => api.get(`/api/v1/projects/${id}/milestones`).then(unwrap)
export const addMilestone    = (id, dto) => api.post(`/api/v1/projects/${id}/milestones`, dto).then(unwrap)
export const updateMilestone = (id, milestoneId, dto) => api.put(`/api/v1/projects/${id}/milestones/${milestoneId}`, dto).then(unwrap)
export const getMilestoneTasks = (id, milestoneId) => api.get(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`).then(unwrap)
export const addMilestoneTask  = (id, milestoneId, dto) => api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`, dto).then(unwrap)
// The real route is the task itself — there is no /status sub-route. This previously pointed at one
// and would have 404'd; nothing called it, so correcting it rather than leaving the trap.
export const updateProjectTask = (id, milestoneId, taskId, dto) => api.put(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks/${taskId}`, dto).then(unwrap)
export const dispatchTask      = (id, milestoneId, taskId, dto) => api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks/${taskId}/dispatch`, dto).then(unwrap)

// Project → budget / costs / resources
export const getProjectBudget = (id) => api.get(`/api/v1/projects/${id}/budget`).then(unwrap)
export const addBudgetLine    = (id, dto) => api.post(`/api/v1/projects/${id}/budget/lines`, dto).then(unwrap)
export const getProjectCosts  = (id) => api.get(`/api/v1/projects/${id}/costs`).then(unwrap)
export const addProjectCost   = (id, dto) => api.post(`/api/v1/projects/${id}/budget/costs`, dto).then(unwrap)
export const getProjectResources = (id) => api.get(`/api/v1/projects/${id}/resources`).then(unwrap)

// ── Assignments ──
export const listAssignments = (params) => api.get('/api/v1/assignments', { params }).then(unwrap)
export const getAssignment   = (id) => api.get(`/api/v1/assignments/${id}`).then(unwrap)
export const acceptAssignment   = (id) => api.post(`/api/v1/assignments/${id}/accept`).then(unwrap)
export const startAssignment    = (id) => api.post(`/api/v1/assignments/${id}/start`).then(unwrap)
export const completeAssignment = (id, dto) => api.post(`/api/v1/assignments/${id}/complete`, dto).then(unwrap)
export const cancelAssignment   = (id, dto) => api.post(`/api/v1/assignments/${id}/cancel`, dto).then(unwrap)
export const archiveAssignment  = (id) => api.post(`/api/v1/assignments/${id}/archive`).then(unwrap)
export const submitForLinking   = (id) => api.post(`/api/v1/assignments/${id}/submit-for-linking`).then(unwrap)
export const linkToProject      = (id, dto) => api.post(`/api/v1/assignments/${id}/link-to-project`, dto).then(unwrap)

// Assignment → check-ins / photos / summaries
export const getCheckIns   = (id) => api.get(`/api/v1/assignments/${id}/checkins`).then(unwrap)
export const checkIn       = (id, dto) => api.post(`/api/v1/assignments/${id}/checkins`, dto).then(unwrap)
export const checkOut      = (id, checkInId, dto) => api.post(`/api/v1/assignments/${id}/checkins/${checkInId}/checkout`, dto).then(unwrap)
export const getPhotos     = (id) => api.get(`/api/v1/assignments/${id}/photos`).then(unwrap)
export const addPhoto      = (id, formData) => api.post(`/api/v1/assignments/${id}/photos`, formData, { headers: { 'Content-Type': 'multipart/form-data' } }).then(unwrap)
export const getSummaries  = (id) => api.get(`/api/v1/assignments/${id}/summaries`).then(unwrap)
export const addSummary    = (id, dto) => api.post(`/api/v1/assignments/${id}/summaries`, dto).then(unwrap)

// Assignment → risks / feedback / NCRs / pre-deployment
export const getRisks     = (id) => api.get(`/api/v1/assignments/${id}/risks`).then(unwrap)
export const addRisk      = (id, dto) => api.post(`/api/v1/assignments/${id}/risks`, dto).then(unwrap)
export const updateRisk   = (id, riskId, dto) => api.put(`/api/v1/assignments/${id}/risks/${riskId}`, dto).then(unwrap)
export const deleteRisk   = (id, riskId) => api.delete(`/api/v1/assignments/${id}/risks/${riskId}`).then(unwrap)
export const getFeedback  = (id) => api.get(`/api/v1/assignments/${id}/feedback`).then(unwrap)
export const addFeedback  = (id, dto) => api.post(`/api/v1/assignments/${id}/feedback`, dto).then(unwrap)
export const getNcrs      = (id) => api.get(`/api/v1/assignments/${id}/ncrs`).then(unwrap)
export const addNcr       = (id, dto) => api.post(`/api/v1/assignments/${id}/ncrs`, dto).then(unwrap)
export const getPreDeployment = (id) => api.get(`/api/v1/assignments/${id}/pre-deployment`).then(unwrap)

// ── Service reports (≈ Field Service Report) ──
export const getServiceReportByAssignment = (id) => api.get(`/api/v1/service-reports/by-assignment/${id}`).then(unwrap)
export const signServiceReport = (id, dto) => api.post(`/api/v1/service-reports/${id}/sign`, dto).then(unwrap)

// ── Lab work orders / calibration ──
export const getLabWorkOrder     = (id) => api.get(`/api/v1/assignments/${id}/lab-work-order`).then(unwrap)
export const createLabWorkOrder  = (id, dto) => api.post(`/api/v1/assignments/${id}/lab-work-order`, dto).then(unwrap)
export const updateLabWorkOrder  = (id, lwoId, dto) => api.post(`/api/v1/assignments/${id}/lab-work-order/${lwoId}`, dto).then(unwrap)
export const addLabDataSheet     = (id, dto) => api.post(`/api/v1/assignments/${id}/lab-work-order/data-sheet`, dto).then(unwrap)
export const recalcLabDataSheet  = (id, dto) => api.post(`/api/v1/assignments/${id}/lab-work-order/data-sheet/recalculate`, dto).then(unwrap)
export const getCertificatePdf   = (id) => api.get(`/api/v1/assignments/${id}/lab-work-order/certificate-pdf`, { responseType: 'blob' }).then(r => r.data)

// ── Financials (per-assignment forms) ──
export const getRequisitions = (assignmentId) => api.get('/api/v1/financials/requisitions', { params: { assignmentId, pageSize: 50 } }).then(unwrap)
export const createRequisition = (dto) => api.post('/api/v1/financials/requisitions', dto).then(unwrap)
export const getClaims = (assignmentId) => api.get('/api/v1/financials/claims', { params: { assignmentId, pageSize: 50 } }).then(unwrap)
export const createClaim = (dto) => api.post('/api/v1/financials/claims', dto).then(unwrap)
export const getPettyCash = (assignmentId) => api.get(`/api/v1/financials/petty-cash/assignment/${assignmentId}`).then(unwrap)
export const createPettyCash = (dto) => api.post('/api/v1/financials/petty-cash', dto).then(unwrap)
export const getPerDiem = (assignmentId) => api.get(`/api/v1/financials/per-diem/assignment/${assignmentId}`).then(unwrap)
export const createPerDiem = (dto) => api.post('/api/v1/financials/per-diem', dto).then(unwrap)
export const getAdvanceReturns = (assignmentId) => api.get(`/api/v1/financials/advance-returns/assignment/${assignmentId}`).then(unwrap)
export const createAdvanceReturn = (dto) => api.post('/api/v1/financials/advance-returns', dto).then(unwrap)
export const getRefunds = (assignmentId) => api.get(`/api/v1/financials/refunds/assignment/${assignmentId}`).then(unwrap)
export const createRefund = (dto) => api.post('/api/v1/financials/refunds', dto).then(unwrap)

// ── Attachments ──
export const getAttachmentsByAssignment = (id) => api.get(`/api/v1/attachments/by-assignment/${id}`).then(unwrap)
export const getAttachments = (params) => api.get('/api/v1/attachments', { params }).then(unwrap)
export const uploadAttachment = (formData) => api.post('/api/v1/attachments', formData, { headers: { 'Content-Type': 'multipart/form-data' } }).then(unwrap)
export const deleteAttachment = (id) => api.delete(`/api/v1/attachments/${id}`).then(unwrap)

// ── Service requests (currently routed to ticketing; repoints to operations in O5 — paths unchanged) ──
// Returns the full body { data: [...], total, pages } — the SR list carries pagination at top level.
export const listServiceRequests = (params) => api.get('/api/v1/service-requests', { params }).then(r => r.data)
export const createServiceRequest = (dto) => api.post('/api/v1/service-requests', dto).then(unwrap)
export const getServiceRequest   = (id) => api.get(`/api/v1/service-requests/${id}`).then(unwrap)
export const reviewServiceRequest = (id, dto) => api.post(`/api/v1/service-requests/${id}/review`, dto).then(unwrap)
export const createQuotation  = (id, dto) => api.post(`/api/v1/service-requests/${id}/quotation`, dto).then(unwrap)
export const updateQuotation  = (id, dto) => api.put(`/api/v1/service-requests/${id}/quotation`, dto).then(unwrap)
export const sendQuotation    = (id) => api.post(`/api/v1/service-requests/${id}/quotation/send`).then(unwrap)
export const reviseQuotation  = (id) => api.post(`/api/v1/service-requests/${id}/quotation/revise`).then(unwrap)
export const recordLpo        = (id, dto) => api.post(`/api/v1/service-requests/${id}/quotation/lpo`, dto).then(unwrap)
export const rejectQuotation  = (id, dto) => api.post(`/api/v1/service-requests/${id}/quotation/reject`, dto).then(unwrap)
export const cancelServiceRequest = (id, dto) => api.post(`/api/v1/service-requests/${id}/cancel`, dto).then(unwrap)
export const createSrAssignment = (id, dto) => api.post(`/api/v1/service-requests/${id}/create-assignment`, dto).then(unwrap)

// ── O1/O2/O3 — Project lifecycle + budget-burn + milestone extensions ──
export const activateProject     = (id) => api.post(`/api/v1/projects/${id}/activate`).then(unwrap)
export const getBudgetAlerts     = (id) => api.get(`/api/v1/projects/${id}/budget/alerts`).then(unwrap)
export const getProjectHseSummary = (id) => api.get(`/api/v1/projects/${id}/hse-summary`).then(unwrap)   // O8 seam
export const signOffMilestone    = (id, milestoneId, dto) => api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/sign-off`, dto).then(unwrap)
export const getMilestoneUpdates = (id, milestoneId) => api.get(`/api/v1/projects/${id}/milestones/${milestoneId}/updates`).then(unwrap)
export const addMilestoneUpdate  = (id, milestoneId, dto) => api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/updates`, dto).then(unwrap)

// ── O4 — Timesheets ──
export const listTimesheets       = (params) => api.get('/api/v1/timesheets', { params }).then(unwrap)
export const getTimesheet         = (id) => api.get(`/api/v1/timesheets/${id}`).then(unwrap)
export const createTimesheet      = (dto) => api.post('/api/v1/timesheets', dto).then(unwrap)
export const addTimesheetEntry    = (id, dto) => api.post(`/api/v1/timesheets/${id}/entries`, dto).then(unwrap)
export const updateTimesheetEntry = (entryId, dto) => api.put(`/api/v1/timesheets/entries/${entryId}`, dto).then(unwrap)
export const deleteTimesheetEntry = (entryId) => api.delete(`/api/v1/timesheets/entries/${entryId}`).then(unwrap)
export const requestOvertime      = (entryId, dto) => api.post(`/api/v1/timesheets/entries/${entryId}/overtime-request`, dto).then(unwrap)
export const submitTimesheet      = (id) => api.post(`/api/v1/timesheets/${id}/submit`).then(unwrap)
export const reviewTimesheet      = (id, dto) => api.post(`/api/v1/timesheets/${id}/review`, dto).then(unwrap)

// ── O5 — Field Service Reports (create/review) + equipment history ──
export const getServiceReport    = (id) => api.get(`/api/v1/service-reports/${id}`).then(unwrap)
export const createServiceReport = (dto) => api.post('/api/v1/service-reports', dto).then(unwrap)
export const updateServiceReport = (id, dto) => api.put(`/api/v1/service-reports/${id}`, dto).then(unwrap)
export const reviewServiceReport = (id, dto) => api.post(`/api/v1/service-reports/${id}/review`, dto).then(unwrap)
export const getEquipmentHistory = (serial) => api.get('/api/v1/service-reports/equipment/history', { params: { serial } }).then(unwrap)
export const recordCertificate   = (id, dto) => api.post(`/api/v1/service-requests/${id}/certificate`, dto).then(unwrap)

// ── O6 — Calibration: reference-standard register + certificate flow ──
export const listReferenceStandards  = (params) => api.get('/api/v1/reference-standards', { params }).then(unwrap)
export const getReferenceStandard    = (id) => api.get(`/api/v1/reference-standards/${id}`).then(unwrap)
export const createReferenceStandard = (dto) => api.post('/api/v1/reference-standards', dto).then(unwrap)
export const updateReferenceStandard = (id, dto) => api.put(`/api/v1/reference-standards/${id}`, dto).then(unwrap)
export const deleteReferenceStandard = (id) => api.delete(`/api/v1/reference-standards/${id}`).then(unwrap)
export const linkLabReferenceStandard = (assignmentId, dto) => api.post(`/api/v1/assignments/${assignmentId}/lab-work-order/reference-standard`, dto).then(unwrap)
export const tmReviewLab         = (assignmentId, dto) => api.post(`/api/v1/assignments/${assignmentId}/lab-work-order/tm-review`, dto).then(unwrap)
export const generateCertificate = (assignmentId) => api.post(`/api/v1/assignments/${assignmentId}/lab-work-order/generate-certificate`).then(unwrap)
export const getCertificateData  = (assignmentId) => api.get(`/api/v1/assignments/${assignmentId}/lab-work-order/certificate-data`).then(unwrap)

// ── O7 — Variation orders + subcontractor-compliance seam ──
export const getVariationOrder            = (id) => api.get(`/api/v1/variation-orders/${id}`).then(unwrap)
export const listVariationOrdersByProject = (projectId) => api.get(`/api/v1/variation-orders/by-project/${projectId}`).then(unwrap)
export const createVariationOrder         = (dto) => api.post('/api/v1/variation-orders', dto).then(unwrap)
export const updateVariationOrder         = (id, dto) => api.put(`/api/v1/variation-orders/${id}`, dto).then(unwrap)
export const deleteVariationOrder         = (id) => api.delete(`/api/v1/variation-orders/${id}`).then(unwrap)
export const submitVariationOrder         = (id) => api.post(`/api/v1/variation-orders/${id}/submit`).then(unwrap)
export const approveVariationOrderMd      = (id, dto) => api.post(`/api/v1/variation-orders/${id}/approve/md`, dto).then(unwrap)
export const approveVariationOrderClient  = (id, dto) => api.post(`/api/v1/variation-orders/${id}/approve/client`, dto).then(unwrap)
export const checkSubcontractorCompliance = (subId) => api.get(`/api/v1/subcontractor-compliance/${subId}`).then(unwrap)

// ── O9 — Project handovers ──
export const getHandover             = (id) => api.get(`/api/v1/handovers/${id}`).then(unwrap)
export const listHandoversByProject  = (projectId) => api.get(`/api/v1/handovers/by-project/${projectId}`).then(unwrap)
export const createHandover          = (dto) => api.post('/api/v1/handovers', dto).then(unwrap)
export const updateHandover          = (id, dto) => api.put(`/api/v1/handovers/${id}`, dto).then(unwrap)
export const addHandoverSignature    = (id, dto) => api.post(`/api/v1/handovers/${id}/signatures`, dto).then(unwrap)
export const completeHandover        = (id) => api.post(`/api/v1/handovers/${id}/complete`).then(unwrap)
export const deleteHandover          = (id) => api.delete(`/api/v1/handovers/${id}`).then(unwrap)

// ── O9 — Negligence incidents ──
export const listNegligenceIncidents = (params) => api.get('/api/v1/negligence-incidents', { params }).then(unwrap)
export const getNegligenceIncident   = (id) => api.get(`/api/v1/negligence-incidents/${id}`).then(unwrap)
export const reportNegligence        = (dto) => api.post('/api/v1/negligence-incidents', dto).then(unwrap)
export const respondNegligence       = (id, dto) => api.post(`/api/v1/negligence-incidents/${id}/responses`, dto).then(unwrap)

// ── Reference data (used across ops pages) ──
export const getDepartments = (params) => api.get('/api/v1/departments', { params }).then(unwrap)
export const getUsers       = (params = { pageSize: 200 }) => api.get('/api/v1/users', { params }).then(unwrap)

// ── O6.1 — Issued-certificate register (expiry tracking + audit counts) ──
export const listCertificates     = (params) => api.get('/api/v1/calibration-certificates', { params }).then(unwrap)
export const getCertificateSummary = (params) => api.get('/api/v1/calibration-certificates/summary', { params }).then(unwrap)
export const withdrawCertificate  = (id, dto) => api.post(`/api/v1/calibration-certificates/${id}/withdraw`, dto).then(unwrap)

// Renders from the certificate's own stored snapshot, so a preview always shows what was signed.
// Distinct from getCertificatePdf above, which re-derives it from the live lab work order.
export const getRegisterCertificatePdf = (id) =>
  api.get(`/api/v1/calibration-certificates/${id}/pdf`, { responseType: 'blob' }).then(r => r.data)

// ── PR1 — schedule, baseline & dependencies ──
export const getProjectSchedule     = (id) => api.get(`/api/v1/projects/${id}/schedule`).then(unwrap)
export const setProjectBaseline     = (id, force = false) => api.post(`/api/v1/projects/${id}/baseline${force ? '?force=true' : ''}`).then(unwrap)
export const linkMilestones         = (id, dto) => api.post(`/api/v1/projects/${id}/milestone-dependencies`, dto).then(unwrap)
export const unlinkMilestones       = (id, depId) => api.delete(`/api/v1/projects/${id}/milestone-dependencies/${depId}`)

// ── PR1 — detailed budget, rate card & commercials ──
export const getBudgetVersions      = (id) => api.get(`/api/v1/projects/${id}/budget-versions`).then(unwrap)
export const createBudgetVersion    = (id, dto) => api.post(`/api/v1/projects/${id}/budget-versions`, dto).then(unwrap)
export const upsertBudgetLine       = (id, vId, dto) => api.post(`/api/v1/projects/${id}/budget-versions/${vId}/lines`, dto).then(unwrap)
export const deleteBudgetLine       = (id, vId, lineId) => api.delete(`/api/v1/projects/${id}/budget-versions/${vId}/lines/${lineId}`).then(unwrap)
export const submitBudgetVersion    = (id, vId) => api.post(`/api/v1/projects/${id}/budget-versions/${vId}/submit`).then(unwrap)
export const approveBudgetVersion   = (id, vId) => api.post(`/api/v1/projects/${id}/budget-versions/${vId}/approve`).then(unwrap)
export const rejectBudgetVersion    = (id, vId, reason) => api.post(`/api/v1/projects/${id}/budget-versions/${vId}/reject`, { reason }).then(unwrap)

export const getContractRates       = (id, activeOnly = false) => api.get(`/api/v1/projects/${id}/contract-rates`, { params: { activeOnly } }).then(unwrap)
export const addContractRate        = (id, dto) => api.post(`/api/v1/projects/${id}/contract-rates`, dto).then(unwrap)
export const deactivateContractRate = (id, rateId) => api.delete(`/api/v1/projects/${id}/contract-rates/${rateId}`)

export const getProjectCommercials  = (id) => api.get(`/api/v1/projects/${id}/commercials`).then(unwrap)
export const getProjectDocuments    = (id) => api.get(`/api/v1/projects/${id}/documents`).then(unwrap)

// Multipart: the browser sets the boundary, so don't force a Content-Type here.
export const uploadProjectContract  = (id, file, description) => {
  const form = new FormData()
  form.append('file', file)
  if (description) form.append('description', description)
  return api.post(`/api/v1/projects/${id}/contract`, form).then(unwrap)
}
export const getTaskDependencies    = (id) => api.get(`/api/v1/projects/${id}/task-dependencies`).then(unwrap)
export const linkTasks              = (id, dto) => api.post(`/api/v1/projects/${id}/task-dependencies`, dto).then(unwrap)
export const unlinkTasks            = (id, depId) => api.delete(`/api/v1/projects/${id}/task-dependencies/${depId}`)

// ── PR3 — governance: RAID + change control ────────────────────────────────────
export const getGovernanceSummary   = (id) => api.get(`/api/v1/projects/${id}/governance/summary`).then(unwrap)

export const getProjectRisks        = (id, includeClosed = false) =>
  api.get(`/api/v1/projects/${id}/risks`, { params: { includeClosed } }).then(unwrap)
export const addProjectRisk         = (id, dto) => api.post(`/api/v1/projects/${id}/risks`, dto).then(unwrap)
export const updateProjectRisk      = (id, riskId, dto) => api.put(`/api/v1/projects/${id}/risks/${riskId}`, dto).then(unwrap)
export const deleteProjectRisk      = (id, riskId) => api.delete(`/api/v1/projects/${id}/risks/${riskId}`)
export const realiseProjectRisk     = (id, riskId, dto) => api.post(`/api/v1/projects/${id}/risks/${riskId}/realise`, dto).then(unwrap)

export const getProjectIssues       = (id, includeClosed = false) =>
  api.get(`/api/v1/projects/${id}/issues`, { params: { includeClosed } }).then(unwrap)
export const addProjectIssue        = (id, dto) => api.post(`/api/v1/projects/${id}/issues`, dto).then(unwrap)
export const updateProjectIssue     = (id, issueId, dto) => api.put(`/api/v1/projects/${id}/issues/${issueId}`, dto).then(unwrap)
export const resolveProjectIssue    = (id, issueId, dto) => api.post(`/api/v1/projects/${id}/issues/${issueId}/resolve`, dto).then(unwrap)

export const getChangeRequests      = (id) => api.get(`/api/v1/projects/${id}/change-requests`).then(unwrap)
export const createChangeRequest    = (id, dto) => api.post(`/api/v1/projects/${id}/change-requests`, dto).then(unwrap)
export const updateChangeRequest    = (id, crId, dto) => api.put(`/api/v1/projects/${id}/change-requests/${crId}`, dto).then(unwrap)
export const submitChangeRequest    = (id, crId) => api.post(`/api/v1/projects/${id}/change-requests/${crId}/submit`).then(unwrap)
export const withdrawChangeRequest  = (id, crId) => api.post(`/api/v1/projects/${id}/change-requests/${crId}/withdraw`).then(unwrap)
export const decideChangeRequest    = (id, crId, dto) => api.post(`/api/v1/projects/${id}/change-requests/${crId}/decide`, dto).then(unwrap)

// ── PR3b — comment threads + mentions ──────────────────────────────────────────
export const getProjectComments   = (id, targetType, targetId) =>
  api.get(`/api/v1/projects/${id}/comments`, { params: { targetType, targetId } }).then(unwrap)
export const addProjectComment    = (id, dto) => api.post(`/api/v1/projects/${id}/comments`, dto).then(unwrap)
export const updateComment        = (commentId, dto) => api.put(`/api/v1/comments/${commentId}`, dto).then(unwrap)
export const deleteComment        = (commentId) => api.delete(`/api/v1/comments/${commentId}`)

export const getMyMentions        = (unreadOnly = false) =>
  api.get('/api/v1/my/mentions', { params: { unreadOnly } }).then(unwrap)
export const markMentionRead      = (mentionId) => api.post(`/api/v1/my/mentions/${mentionId}/read`)
export const markAllMentionsRead  = () => api.post('/api/v1/my/mentions/read-all').then(unwrap)

// ── PR4a — earned value / S-curve / portfolio ──────────────────────────────────
export const getProjectEvm = (id, asOf) =>
  api.get(`/api/v1/projects/${id}/evm`, { params: { asOf } }).then(unwrap)
export const getPortfolioEvm = (asOf) =>
  api.get('/api/v1/projects/portfolio/evm', { params: { asOf } }).then(unwrap)

// ── PR4b — templates + recurring work ──────────────────────────────────────────
export const getProjectTemplates   = (includeInactive = false) =>
  api.get('/api/v1/project-templates', { params: { includeInactive } }).then(unwrap)
export const getProjectTemplate    = (id) => api.get(`/api/v1/project-templates/${id}`).then(unwrap)
export const saveProjectTemplate   = (id, dto) =>
  (id ? api.put(`/api/v1/project-templates/${id}`, dto) : api.post('/api/v1/project-templates', dto)).then(unwrap)
export const deactivateTemplate    = (id) => api.delete(`/api/v1/project-templates/${id}`)
export const instantiateTemplate   = (id, dto) =>
  api.post(`/api/v1/project-templates/${id}/instantiate`, dto).then(unwrap)

export const getRecurringSchedules = (includeInactive = false) =>
  api.get('/api/v1/recurring-projects', { params: { includeInactive } }).then(unwrap)
export const saveRecurringSchedule = (id, dto) =>
  (id ? api.put(`/api/v1/recurring-projects/${id}`, dto) : api.post('/api/v1/recurring-projects', dto)).then(unwrap)
export const setRecurringActive    = (id, active) =>
  api.post(`/api/v1/recurring-projects/${id}/active`, null, { params: { active } }).then(unwrap)
export const runRecurringNow       = (id) => api.post(`/api/v1/recurring-projects/${id}/run-now`).then(unwrap)

// ── PR4c — board / calendar / workload ─────────────────────────────────────────
export const getProjectBoard = (id) => api.get(`/api/v1/projects/${id}/board`).then(unwrap)
export const getWorkload     = (from, to, departmentId) =>
  api.get('/api/v1/projects/workload', { params: { from, to, departmentId } }).then(unwrap)
