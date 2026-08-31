// Reports API service — the single place reports endpoint strings live.
// Each function returns the unwrapped `data` payload (ApiResponse<T>.data).
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

export const getManagementAccounts   = (params) => api.get('/api/v1/reports/management-accounts', { params }).then(unwrap)
export const getBudgetVariance       = (params) => api.get('/api/v1/reports/budget-variance', { params }).then(unwrap)
export const getAgedDebtors          = (params) => api.get('/api/v1/reports/aged-debtors', { params }).then(unwrap)
export const getCashFlowForecast     = (params) => api.get('/api/v1/reports/cash-flow-forecast', { params }).then(unwrap)
export const getProjectProfitability = (params) => api.get('/api/v1/reports/project-profitability', { params }).then(unwrap)
export const getFleetCostUtilisation = (params) => api.get('/api/v1/reports/fleet-cost-utilisation', { params }).then(unwrap)
export const getProcurementSpend     = (params) => api.get('/api/v1/reports/procurement-spend', { params }).then(unwrap)
export const getHseIncidentsTrir     = (params) => api.get('/api/v1/reports/hse-incidents-trir', { params }).then(unwrap)
export const getComplianceDashboardReport = () => api.get('/api/v1/reports/compliance-dashboard').then(unwrap)

// Schedule & Delivery (RPT-001..005) — report definitions, schedules, recipients, run history.
export const getReportDefinitions = () => api.get('/api/v1/report-definitions').then(unwrap)
export const runReportNow = (definitionId, format = 0) =>
  api.post(`/api/v1/report-definitions/${definitionId}/run-now`, null, { params: { format } }).then(unwrap)

export const getReportSchedules = () => api.get('/api/v1/report-schedules').then(unwrap)
export const createReportSchedule = (dto) => api.post('/api/v1/report-schedules', dto).then(unwrap)
export const updateReportSchedule = (id, dto) => api.put(`/api/v1/report-schedules/${id}`, dto).then(unwrap)
export const deleteReportSchedule = (id) => api.delete(`/api/v1/report-schedules/${id}`).then(unwrap)

export const getReportRecipients = (scheduleId) => api.get(`/api/v1/report-schedules/${scheduleId}/recipients`).then(unwrap)
export const addReportRecipient = (scheduleId, dto) => api.post(`/api/v1/report-schedules/${scheduleId}/recipients`, dto).then(unwrap)
export const removeReportRecipient = (scheduleId, recipientId) =>
  api.delete(`/api/v1/report-schedules/${scheduleId}/recipients/${recipientId}`).then(unwrap)

export const getReportRuns = (params) => api.get('/api/v1/report-runs', { params }).then(unwrap)
