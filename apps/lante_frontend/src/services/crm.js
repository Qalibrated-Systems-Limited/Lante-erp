// CRM & Sales API service (Module 6) — the single home for CRM endpoint strings.
// Each fn returns the unwrapped payload, mirroring services/operations.js / finance.js / ticketing.js.
// The crm-service is the customer system-of-record; endpoints land per phase (C1 CUSTOMER onward).
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

// ── Customers (C1) ──
// List returns the full paged body { data, total, page, pageSize, pages } (top-level pagination),
// matching the crm controller shape; detail/actions unwrap to `data`.
export const listCustomers   = (params) => api.get('/api/v1/customers', { params }).then(r => r.data)
export const getCustomer     = (id) => api.get(`/api/v1/customers/${id}`).then(unwrap)
export const createCustomer  = (dto) => api.post('/api/v1/customers', dto).then(unwrap)
export const updateCustomer  = (id, dto) => api.put(`/api/v1/customers/${id}`, dto).then(unwrap)
export const checkCustomerDuplicate = (params) => api.get('/api/v1/customers/duplicate-check', { params }).then(unwrap)

// Onboarding approval chain
export const approveCustomerLineManager = (id) => api.post(`/api/v1/customers/${id}/approve/line-manager`).then(unwrap)
export const approveCustomerHeadBd = (id) => api.post(`/api/v1/customers/${id}/approve/head-bd`).then(unwrap)
export const cfoReviewCustomer   = (id, dto) => api.post(`/api/v1/customers/${id}/cfo-review`, dto).then(unwrap)
export const approveCustomerMd   = (id) => api.post(`/api/v1/customers/${id}/approve/md`).then(unwrap)
export const rejectCustomer      = (id, dto) => api.post(`/api/v1/customers/${id}/reject`, dto).then(unwrap)
export const deactivateCustomer  = (id) => api.post(`/api/v1/customers/${id}/deactivate`).then(unwrap)

// Contacts
export const addCustomerContact    = (id, dto) => api.post(`/api/v1/customers/${id}/contacts`, dto).then(unwrap)
export const updateCustomerContact = (contactId, dto) => api.put(`/api/v1/customers/contacts/${contactId}`, dto).then(unwrap)

// ── Leads (C2) ──
// List returns full paged body { data, total, page, pageSize, pages, openCount, openValue }.
export const listLeads       = (params) => api.get('/api/v1/leads', { params }).then(r => r.data)
export const getLead         = (id) => api.get(`/api/v1/leads/${id}`).then(unwrap)
export const createLead      = (dto) => api.post('/api/v1/leads', dto).then(unwrap)
export const updateLead      = (id, dto) => api.put(`/api/v1/leads/${id}`, dto).then(unwrap)
export const addLeadActivity = (id, dto) => api.post(`/api/v1/leads/${id}/activities`, dto).then(unwrap)
export const assignLead      = (id, dto) => api.post(`/api/v1/leads/${id}/assign`, dto).then(unwrap)
export const qualifyLead     = (id, dto) => api.post(`/api/v1/leads/${id}/qualify`, dto).then(unwrap)
export const unqualifyLead   = (id, dto) => api.post(`/api/v1/leads/${id}/unqualify`, dto).then(unwrap)
export const convertLead     = (id, dto) => api.post(`/api/v1/leads/${id}/convert`, dto).then(unwrap)

// ── Opportunities & pipeline (C3) ──
export const getPipelineStages   = () => api.get('/api/v1/opportunities/stages').then(unwrap)
export const getPipelineBoard    = (params) => api.get('/api/v1/opportunities/board', { params }).then(r => r.data)
export const listOpportunities   = (params) => api.get('/api/v1/opportunities', { params }).then(r => r.data)
export const getOpportunity      = (id) => api.get(`/api/v1/opportunities/${id}`).then(unwrap)
export const createOpportunity   = (dto) => api.post('/api/v1/opportunities', dto).then(unwrap)
export const updateOpportunity   = (id, dto) => api.put(`/api/v1/opportunities/${id}`, dto).then(unwrap)
export const advanceOpportunity  = (id, dto) => api.post(`/api/v1/opportunities/${id}/advance`, dto).then(unwrap)
export const winOpportunity      = (id) => api.post(`/api/v1/opportunities/${id}/won`).then(unwrap)
export const loseOpportunity     = (id, dto) => api.post(`/api/v1/opportunities/${id}/lost`, dto).then(unwrap)
export const addOpportunityActivity = (id, dto) => api.post(`/api/v1/opportunities/${id}/activities`, dto).then(unwrap)

// ── Quotations (C4) ──
export const listQuotations   = (params) => api.get('/api/v1/quotations', { params }).then(r => r.data)
export const getQuotation     = (id) => api.get(`/api/v1/quotations/${id}`).then(unwrap)
export const getQuotationVersions = (num) => api.get(`/api/v1/quotations/number/${num}/versions`).then(unwrap)
export const getLastSale      = (params) => api.get('/api/v1/quotations/last-sale', { params }).then(unwrap)
export const createQuotation  = (dto) => api.post('/api/v1/quotations', dto).then(unwrap)
export const saveQuotationLines = (id, dto) => api.put(`/api/v1/quotations/${id}/lines`, dto).then(unwrap)
export const submitQuotation  = (id) => api.post(`/api/v1/quotations/${id}/submit`).then(unwrap)
export const deptHeadReviewQuotation = (id, dto) => api.post(`/api/v1/quotations/${id}/dept-head-review`, dto).then(unwrap)
export const mdApproveQuotation = (id) => api.post(`/api/v1/quotations/${id}/md-approve`).then(unwrap)
export const sendQuotation    = (id) => api.post(`/api/v1/quotations/${id}/send`).then(unwrap)
export const quotationOutcome = (id, dto) => api.post(`/api/v1/quotations/${id}/outcome`, dto).then(unwrap)
export const reviseQuotation  = (id) => api.post(`/api/v1/quotations/${id}/revise`).then(unwrap)

// ── Deals & contracts (C5) ──
export const listDeals        = (params) => api.get('/api/v1/deals', { params }).then(r => r.data)
export const getDeal          = (id) => api.get(`/api/v1/deals/${id}`).then(unwrap)
export const createDeal       = (dto) => api.post('/api/v1/deals', dto).then(unwrap)
export const updateDeal       = (id, dto) => api.put(`/api/v1/deals/${id}`, dto).then(unwrap)
export const registerContract = (id, dto) => api.post(`/api/v1/deals/${id}/contract`, dto).then(unwrap)
export const createDealProject = (id) => api.post(`/api/v1/deals/${id}/create-project`).then(unwrap)
export const closeDeal        = (id) => api.post(`/api/v1/deals/${id}/close`).then(unwrap)

// ── Tenders & bid bonds (C6) ──
export const listTenders     = (params) => api.get('/api/v1/tenders', { params }).then(r => r.data)
export const getTender       = (id) => api.get(`/api/v1/tenders/${id}`).then(unwrap)
export const createTender    = (dto) => api.post('/api/v1/tenders', dto).then(unwrap)
export const updateTender    = (id, dto) => api.put(`/api/v1/tenders/${id}`, dto).then(unwrap)
export const saveBidBond     = (id, dto) => api.post(`/api/v1/tenders/${id}/bid-bond`, dto).then(unwrap)
export const submitTender    = (id) => api.post(`/api/v1/tenders/${id}/submit`).then(unwrap)
export const winTender       = (id, dto) => api.post(`/api/v1/tenders/${id}/won`, dto).then(unwrap)
export const loseTender      = (id, dto) => api.post(`/api/v1/tenders/${id}/lost`, dto).then(unwrap)
export const noBidTender     = (id, dto) => api.post(`/api/v1/tenders/${id}/no-bid`, dto).then(unwrap)

// ── Client interaction & activity (C7) ──
export const getInteractions   = (customerId) => api.get(`/api/v1/customers/${customerId}/interactions`).then(unwrap)
export const logInteraction    = (customerId, dto) => api.post(`/api/v1/customers/${customerId}/interactions`, dto).then(unwrap)
export const listTasks         = (params) => api.get('/api/v1/tasks', { params }).then(r => r.data)
export const createTask        = (dto) => api.post('/api/v1/tasks', dto).then(unwrap)
export const completeTask      = (id) => api.post(`/api/v1/tasks/${id}/complete`).then(unwrap)
export const cancelTask        = (id) => api.post(`/api/v1/tasks/${id}/cancel`).then(unwrap)
export const listVisits        = (params) => api.get('/api/v1/visits', { params }).then(unwrap)
export const logVisit          = (dto) => api.post('/api/v1/visits', dto).then(unwrap)
export const getVisitTargets   = () => api.get('/api/v1/visit-targets').then(unwrap)
export const saveVisitTarget   = (dto) => api.post('/api/v1/visit-targets', dto).then(unwrap)
export const getActivityLog    = (params) => api.get('/api/v1/activity-log', { params }).then(unwrap)
export const logDailyActivity  = (dto) => api.post('/api/v1/activity-log', dto).then(unwrap)

// ── Account ownership transfer (C8) ──
export const listTransfers    = (params) => api.get('/api/v1/transfers', { params }).then(r => r.data)
export const getTransfer      = (id) => api.get(`/api/v1/transfers/${id}`).then(unwrap)
export const raiseTransfer    = (dto) => api.post('/api/v1/transfers', dto).then(unwrap)
export const approveTransferHeadBd = (id) => api.post(`/api/v1/transfers/${id}/approve/head-bd`).then(unwrap)
export const approveTransferCfo = (id) => api.post(`/api/v1/transfers/${id}/approve/cfo`).then(unwrap)
export const approveTransferMd = (id) => api.post(`/api/v1/transfers/${id}/approve/md`).then(unwrap)
export const rejectTransfer   = (id, dto) => api.post(`/api/v1/transfers/${id}/reject`, dto).then(unwrap)
export const updateHandover   = (id, dto) => api.put(`/api/v1/transfers/${id}/handover`, dto).then(unwrap)
export const signHandover     = (id, dto) => api.post(`/api/v1/transfers/${id}/handover/sign`, dto).then(unwrap)
export const completeTransfer = (id) => api.post(`/api/v1/transfers/${id}/complete`).then(unwrap)

// ── Dashboards & targets (C9) ──
export const getMdPipeline    = () => api.get('/api/v1/dashboard/md-pipeline').then(unwrap)
export const getSePerformance = (params) => api.get('/api/v1/dashboard/se-performance', { params }).then(unwrap)
export const getSalesTargets  = () => api.get('/api/v1/dashboard/targets').then(unwrap)
export const saveSalesTarget  = (dto) => api.post('/api/v1/dashboard/targets', dto).then(unwrap)

// ── Marketing (C10) ──
export const getMarketingDashboard = () => api.get('/api/v1/marketing/dashboard').then(unwrap)
export const listCampaigns    = (params) => api.get('/api/v1/marketing/campaigns', { params }).then(unwrap)
export const getCampaign      = (id) => api.get(`/api/v1/marketing/campaigns/${id}`).then(unwrap)
export const createCampaign   = (dto) => api.post('/api/v1/marketing/campaigns', dto).then(unwrap)
export const updateCampaign   = (id, dto) => api.put(`/api/v1/marketing/campaigns/${id}`, dto).then(unwrap)
export const recordCampaignSpend = (id, dto) => api.post(`/api/v1/marketing/campaigns/${id}/spend`, dto).then(unwrap)
export const launchCampaign   = (id) => api.post(`/api/v1/marketing/campaigns/${id}/launch`).then(unwrap)
export const completeCampaign = (id) => api.post(`/api/v1/marketing/campaigns/${id}/complete`).then(unwrap)
export const cancelCampaign   = (id) => api.post(`/api/v1/marketing/campaigns/${id}/cancel`).then(unwrap)
export const listBrandAssets  = (params) => api.get('/api/v1/marketing/brand-assets', { params }).then(unwrap)
export const saveBrandAsset   = (dto) => api.post('/api/v1/marketing/brand-assets', dto).then(unwrap)
export const deleteBrandAsset = (id) => api.delete(`/api/v1/marketing/brand-assets/${id}`).then(r => r.data)

// ── After-sales & retention (C11) ──
export const getAfterSalesSummary = () => api.get('/api/v1/aftersales/summary').then(unwrap)
export const listSurveys        = (params) => api.get('/api/v1/aftersales/surveys', { params }).then(unwrap)
export const sendSurvey         = (dto) => api.post('/api/v1/aftersales/surveys', dto).then(unwrap)
export const respondSurvey      = (id, dto) => api.post(`/api/v1/aftersales/surveys/${id}/respond`, dto).then(unwrap)
export const listServiceContracts = (params) => api.get('/api/v1/aftersales/service-contracts', { params }).then(unwrap)
export const getServiceContract = (id) => api.get(`/api/v1/aftersales/service-contracts/${id}`).then(unwrap)
export const createServiceContract = (dto) => api.post('/api/v1/aftersales/service-contracts', dto).then(unwrap)
export const updateServiceContract = (id, dto) => api.put(`/api/v1/aftersales/service-contracts/${id}`, dto).then(unwrap)
export const renewServiceContract = (id, dto) => api.post(`/api/v1/aftersales/service-contracts/${id}/renew`, dto).then(unwrap)
export const cancelServiceContract = (id) => api.post(`/api/v1/aftersales/service-contracts/${id}/cancel`).then(unwrap)
export const listComplaints     = (params) => api.get('/api/v1/aftersales/complaints', { params }).then(unwrap)
export const raiseComplaint     = (dto) => api.post('/api/v1/aftersales/complaints', dto).then(unwrap)
export const assignComplaint    = (id, dto) => api.post(`/api/v1/aftersales/complaints/${id}/assign`, dto).then(unwrap)
export const startComplaint     = (id) => api.post(`/api/v1/aftersales/complaints/${id}/start`).then(unwrap)
export const resolveComplaint   = (id, dto) => api.post(`/api/v1/aftersales/complaints/${id}/resolve`, dto).then(unwrap)
export const closeComplaint     = (id) => api.post(`/api/v1/aftersales/complaints/${id}/close`).then(unwrap)
export const listNps            = (params) => api.get('/api/v1/aftersales/nps', { params }).then(unwrap)
export const sendNps            = (dto) => api.post('/api/v1/aftersales/nps', dto).then(unwrap)
export const respondNps         = (id, dto) => api.post(`/api/v1/aftersales/nps/${id}/respond`, dto).then(unwrap)

// ── Legal & contract register (C12) ──
export const getLegalSummary   = () => api.get('/api/v1/legal/summary').then(unwrap)
export const listNdas          = (params) => api.get('/api/v1/legal/ndas', { params }).then(unwrap)
export const createNda         = (dto) => api.post('/api/v1/legal/ndas', dto).then(unwrap)
export const updateNda         = (id, dto) => api.put(`/api/v1/legal/ndas/${id}`, dto).then(unwrap)
export const terminateNda      = (id) => api.post(`/api/v1/legal/ndas/${id}/terminate`).then(unwrap)
export const listFrameworks    = (params) => api.get('/api/v1/legal/frameworks', { params }).then(unwrap)
export const createFramework   = (dto) => api.post('/api/v1/legal/frameworks', dto).then(unwrap)
export const updateFramework   = (id, dto) => api.put(`/api/v1/legal/frameworks/${id}`, dto).then(unwrap)
export const terminateFramework = (id) => api.post(`/api/v1/legal/frameworks/${id}/terminate`).then(unwrap)
export const listSubcontracts  = (params) => api.get('/api/v1/legal/subcontracts', { params }).then(unwrap)
export const createSubcontract = (dto) => api.post('/api/v1/legal/subcontracts', dto).then(unwrap)
export const updateSubcontract = (id, dto) => api.put(`/api/v1/legal/subcontracts/${id}`, dto).then(unwrap)
export const terminateSubcontract = (id) => api.post(`/api/v1/legal/subcontracts/${id}/terminate`).then(unwrap)
export const listCarriers      = (params) => api.get('/api/v1/legal/carriers', { params }).then(unwrap)
export const getCarrier        = (id) => api.get(`/api/v1/legal/carriers/${id}`).then(unwrap)
export const createCarrier     = (dto) => api.post('/api/v1/legal/carriers', dto).then(unwrap)
export const updateCarrier     = (id, dto) => api.put(`/api/v1/legal/carriers/${id}`, dto).then(unwrap)
export const vetCarrier        = (id, dto) => api.post(`/api/v1/legal/carriers/${id}/vet`, dto).then(unwrap)
export const suspendCarrier    = (id) => api.post(`/api/v1/legal/carriers/${id}/suspend`).then(unwrap)

// ── Payment / debtor alerts (C13, Finance-backed) ──
export const getPaymentAlertSummary = () => api.get('/api/v1/payment-alerts/summary').then(unwrap)
export const listPaymentAlerts = (params) => api.get('/api/v1/payment-alerts', { params }).then(unwrap)
export const acknowledgePaymentAlert = (id) => api.post(`/api/v1/payment-alerts/${id}/acknowledge`).then(unwrap)
