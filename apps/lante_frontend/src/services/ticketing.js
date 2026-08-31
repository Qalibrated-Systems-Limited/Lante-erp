// Ticketing API service — the single place ticketing endpoint strings live.
// Each function returns the unwrapped `data` payload (ApiResponse<T>.data), mirroring services/finance.js.
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

// ── Tickets ──
export const listTickets      = (params) => api.get('/api/v1/tickets', { params }).then(unwrap)
export const getTicket        = (id) => api.get(`/api/v1/tickets/${id}`).then(unwrap)
export const createTicket     = (dto) => api.post('/api/v1/tickets', dto).then(unwrap)
export const assignTicket     = (id, dto) => api.put(`/api/v1/tickets/${id}/assign`, dto).then(unwrap)
export const assignDepartment = (id, dto) => api.put(`/api/v1/tickets/${id}/department`, dto).then(unwrap)
export const resolveTicket    = (id, dto) => api.put(`/api/v1/tickets/${id}/resolve`, dto).then(unwrap)
export const closeTicket      = (id) => api.put(`/api/v1/tickets/${id}/close`).then(unwrap)
export const reopenTicket     = (id) => api.put(`/api/v1/tickets/${id}/reopen`).then(unwrap)
export const setTicketStatus  = (id, dto) => api.put(`/api/v1/tickets/${id}/status`, dto).then(unwrap)
export const escalateTicket   = (id, dto) => api.put(`/api/v1/tickets/${id}/escalate`, dto).then(unwrap)
export const mergeTicket      = (sourceId, dto) => api.post(`/api/v1/tickets/${sourceId}/merge`, dto).then(unwrap)
export const getDuplicates    = (id) => api.get(`/api/v1/tickets/${id}/duplicates`).then(unwrap)
export const getChildren      = (id) => api.get(`/api/v1/tickets/${id}/children`).then(unwrap)

// ── Comments / history / attachments ──
export const getComments   = (id) => api.get(`/api/v1/tickets/${id}/comments`).then(unwrap)
export const addComment    = (id, dto) => api.post(`/api/v1/tickets/${id}/comments`, dto).then(unwrap)
export const getHistory    = (id) => api.get(`/api/v1/tickets/${id}/history`).then(unwrap)
export const getAttachments = (id) => api.get(`/api/v1/tickets/${id}/attachments`).then(unwrap)
export const uploadAttachments = (id, formData) =>
  api.post(`/api/v1/tickets/${id}/attachments`, formData, { headers: { 'Content-Type': 'multipart/form-data' } }).then(unwrap)

// ── Escalations / watchers ──
export const getEscalations       = (id) => api.get(`/api/v1/tickets/${id}/escalations`).then(unwrap)
export const acknowledgeEscalation = (id, escId) => api.put(`/api/v1/tickets/${id}/escalations/${escId}/acknowledge`).then(unwrap)
export const getWatchers   = (id) => api.get(`/api/v1/tickets/${id}/watchers`).then(unwrap)
export const addWatcher    = (id, userId) => api.post(`/api/v1/tickets/${id}/watchers/${userId}`).then(unwrap)
export const removeWatcher = (id, userId) => api.delete(`/api/v1/tickets/${id}/watchers/${userId}`).then(unwrap)

// ── Complaint workflow (D5) ──
export const getComplaintSteps    = (id) => api.get(`/api/v1/tickets/${id}/complaint-steps`).then(unwrap)
export const completeComplaintStep = (id, stepNumber, dto) =>
  api.post(`/api/v1/tickets/${id}/complaint-steps/${stepNumber}/complete`, dto).then(unwrap)

// ── Satisfaction (rating) ──
export const getRating    = (id) => api.get(`/api/v1/tickets/${id}/rating`).then(unwrap)
export const submitRating = (id, dto) => api.post(`/api/v1/tickets/${id}/rating`, dto).then(unwrap)

// ── Dashboard ──
export const getDashboardSummary = () => api.get('/api/v1/tickets/dashboard/summary').then(unwrap)
export const getMySummary        = () => api.get('/api/v1/tickets/dashboard/my-summary').then(unwrap)
export const getSlaCompliance    = () => api.get('/api/v1/tickets/dashboard/sla-compliance').then(unwrap)
export const getCsat             = (params) => api.get('/api/v1/tickets/dashboard/csat', { params }).then(unwrap)
export const getCsOverview       = (params) => api.get('/api/v1/tickets/dashboard/cs-overview', { params }).then(unwrap)

// ── Reference data (used across ticketing pages) ──
export const getCategories  = () => api.get('/api/v1/ticket-categories').then(unwrap)
export const getDepartments = () => api.get('/api/v1/departments').then(unwrap)
export const getUsers       = () => api.get('/api/v1/users?pageSize=200').then(unwrap)
export const searchCustomers = (q) => api.get('/api/v1/customers', { params: q ? { q } : {} }).then(unwrap)

// ── Tags ──
export const listTags        = () => api.get('/api/v1/tags').then(unwrap)
export const createTag       = (dto) => api.post('/api/v1/tags', dto).then(unwrap)
export const deleteTag       = (id) => api.delete(`/api/v1/tags/${id}`).then(unwrap)
export const getTicketTags   = (id) => api.get(`/api/v1/tags/tickets/${id}`).then(unwrap)
export const addTicketTag    = (id, dto) => api.post(`/api/v1/tags/tickets/${id}`, dto).then(unwrap)
export const removeTicketTag = (id, tagId) => api.delete(`/api/v1/tags/tickets/${id}/${tagId}`).then(unwrap)

// ── Macros ──
export const listMacros  = () => api.get('/api/v1/macros').then(unwrap)
export const createMacro = (dto) => api.post('/api/v1/macros', dto).then(unwrap)
export const updateMacro = (id, dto) => api.put(`/api/v1/macros/${id}`, dto).then(unwrap)
export const deleteMacro = (id) => api.delete(`/api/v1/macros/${id}`).then(unwrap)
export const applyMacro  = (ticketId, dto) => api.post(`/api/v1/macros/apply/${ticketId}`, dto).then(unwrap)

// ── Workflow rules ──
export const listWorkflowRules  = () => api.get('/api/v1/workflow-rules').then(unwrap)
export const createWorkflowRule = (dto) => api.post('/api/v1/workflow-rules', dto).then(unwrap)
export const updateWorkflowRule = (id, dto) => api.put(`/api/v1/workflow-rules/${id}`, dto).then(unwrap)
export const deleteWorkflowRule = (id) => api.delete(`/api/v1/workflow-rules/${id}`).then(unwrap)
export const toggleWorkflowRule = (id, activate) =>
  api.patch(`/api/v1/workflow-rules/${id}/${activate ? 'activate' : 'deactivate'}`).then(unwrap)

// ── Knowledge base (D7) ──
export const listKb        = () => api.get('/api/v1/kb').then(unwrap)
export const getKbArticle  = (id) => api.get(`/api/v1/kb/${id}`).then(unwrap)
export const suggestKb     = (q) => api.get('/api/v1/kb/suggest', { params: { q } }).then(unwrap)
export const createKbArticle = (dto) => api.post('/api/v1/kb', dto).then(unwrap)
export const updateKbArticle = (id, dto) => api.put(`/api/v1/kb/${id}`, dto).then(unwrap)
export const deleteKbArticle = (id) => api.delete(`/api/v1/kb/${id}`).then(unwrap)
