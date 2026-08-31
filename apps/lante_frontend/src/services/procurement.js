// Procurement & Supply Chain API service (Module 4) — the single home for procurement endpoint strings.
// All procurement endpoints are namespaced under /api/v1/procurement/* (avoids the /suppliers path that
// Stores owns, and the /quotations path CRM/Ops/Ticketing own). Mirrors services/crm.js.
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data
const B = '/api/v1/procurement'

// ── P1: Approved Supplier Register ──
// List returns the full paged body { data, total, page, pageSize, pages }; detail/actions unwrap to data.
export const listSuppliers   = (params) => api.get(`${B}/suppliers`, { params }).then(r => r.data)
export const getSupplier     = (id) => api.get(`${B}/suppliers/${id}`).then(unwrap)
export const createSupplier  = (dto) => api.post(`${B}/suppliers`, dto).then(unwrap)
export const updateSupplier  = (id, dto) => api.put(`${B}/suppliers/${id}`, dto).then(unwrap)
export const asrSummary      = () => api.get(`${B}/suppliers/summary`).then(unwrap)

// Compliance documents
export const getSupplierDocuments = (id) => api.get(`${B}/suppliers/${id}/documents`).then(unwrap)
export const uploadSupplierDocument = (id, dto) => api.post(`${B}/suppliers/${id}/documents`, dto).then(unwrap)
export const verifySupplierDocument = (docId) => api.post(`${B}/suppliers/documents/${docId}/verify`).then(unwrap)

// Workflow (PROC-007 conflict → PROC-001 approval → blacklist)
export const runConflictCheck = (id, dto) => api.post(`${B}/suppliers/${id}/conflict-check`, dto).then(unwrap)
export const approveSupplier  = (id) => api.post(`${B}/suppliers/${id}/approve`).then(unwrap)
export const blacklistSupplier = (id, dto) => api.post(`${B}/suppliers/${id}/blacklist`, dto).then(unwrap)
export const reinstateSupplier = (id) => api.post(`${B}/suppliers/${id}/reinstate`).then(unwrap)

// Categories
export const listCategories  = () => api.get(`${B}/supplier-categories`).then(unwrap)
export const createCategory  = (dto) => api.post(`${B}/supplier-categories`, dto).then(unwrap)
export const updateCategory  = (id, dto) => api.put(`${B}/supplier-categories/${id}`, dto).then(unwrap)

// Gift register (PROC-007)
export const listGifts       = (params) => api.get(`${B}/gifts`, { params }).then(unwrap)
export const declareGift     = (dto) => api.post(`${B}/gifts`, dto).then(unwrap)

// ── P2: Purchase Requisitions ──
export const listRequisitions = (params) => api.get(`${B}/requisitions`, { params }).then(r => r.data)
export const getRequisition   = (id) => api.get(`${B}/requisitions/${id}`).then(unwrap)
export const prSummary        = () => api.get(`${B}/requisitions/summary`).then(unwrap)
export const createRequisition = (dto) => api.post(`${B}/requisitions`, dto).then(unwrap)
export const updateRequisition = (id, dto) => api.put(`${B}/requisitions/${id}`, dto).then(unwrap)
export const submitRequisition = (id) => api.post(`${B}/requisitions/${id}/submit`).then(unwrap)
export const reviewRequisition = (id, dto) => api.post(`${B}/requisitions/${id}/review`, dto).then(unwrap)
export const escalateRequisition = (id) => api.post(`${B}/requisitions/${id}/escalate`).then(unwrap)

// ── P3: Quotations & comparative analysis ──
export const getSourcing        = (prId) => api.get(`${B}/requisitions/${prId}/sourcing`).then(unwrap)
export const getComparison      = (prId) => api.get(`${B}/requisitions/${prId}/comparison`).then(unwrap)
export const recordQuotation    = (prId, dto) => api.post(`${B}/requisitions/${prId}/quotations`, dto).then(unwrap)
export const completeComparison = (prId, dto) => api.post(`${B}/requisitions/${prId}/complete-comparison`, dto).then(unwrap)
export const scoreQuotation     = (quotationId, dto) => api.post(`${B}/quotations/${quotationId}/score`, dto).then(unwrap)
export const deleteQuotation    = (quotationId) => api.delete(`${B}/quotations/${quotationId}`).then(unwrap)

// ── P4: LPO / Purchase Orders ──
export const listPurchaseOrders = (params) => api.get(`${B}/purchase-orders`, { params }).then(r => r.data)
export const getPurchaseOrder   = (id) => api.get(`${B}/purchase-orders/${id}`).then(unwrap)
export const getPoByRequisition = (prId) => api.get(`${B}/purchase-orders/by-requisition/${prId}`).then(unwrap).catch(() => null)
export const poSummary          = () => api.get(`${B}/purchase-orders/summary`).then(unwrap)
export const generateLpo        = (prId, dto) => api.post(`${B}/purchase-orders/generate/${prId}`, dto ?? {}).then(unwrap)
export const attachBoardResolution = (id, dto) => api.post(`${B}/purchase-orders/${id}/board-resolution`, dto).then(unwrap)
export const signLpo            = (id, dto) => api.post(`${B}/purchase-orders/${id}/sign`, dto).then(unwrap)

// ── P6: 3-way match & payment handoff ──
// PO vs Stores GRN receipt vs Finance supplier invoice. A clean match hands a payment voucher to Finance;
// discrepancies raise matching exceptions that block payment until resolved.
export const listMatches       = (params) => api.get(`${B}/matches`, { params }).then(r => r.data)
export const getMatch          = (id) => api.get(`${B}/matches/${id}`).then(unwrap)
export const getMatchByPo      = (poId) => api.get(`${B}/matches/by-po/${poId}`).then(unwrap).catch(() => null)
export const matchSummary      = () => api.get(`${B}/matches/summary`).then(unwrap)
export const listMatchExceptions = (params) => api.get(`${B}/matches/exceptions`, { params }).then(unwrap)
export const runMatch          = (poId) => api.post(`${B}/matches/run/${poId}`).then(unwrap)
export const runPendingMatches = () => api.post(`${B}/matches/run-pending`).then(unwrap)
export const resolveMatchException = (id, dto) => api.post(`${B}/matches/exceptions/${id}/resolve`, dto).then(unwrap)
export const raisePaymentVoucher = (matchId, dto) => api.post(`${B}/matches/${matchId}/voucher`, dto ?? {}).then(unwrap)

// ── P7: international sourcing (PROC-003) ──
// FX terms on an issued LPO, the T/T advance (MD approval mandatory), shipment + customs, landed cost.
export const listIntlPos      = (params) => api.get(`${B}/international`, { params }).then(r => r.data)
export const getIntlPo        = (id) => api.get(`${B}/international/${id}`).then(unwrap)
export const getIntlPoByPo    = (poId) => api.get(`${B}/international/by-po/${poId}`).then(unwrap).catch(() => null)
export const intlSummary      = () => api.get(`${B}/international/summary`).then(unwrap)
export const listCurrencies   = () => api.get(`${B}/international/currencies`).then(unwrap)
export const createIntlPo     = (dto) => api.post(`${B}/international`, dto).then(unwrap)
export const updateIntlShipment = (id, dto) => api.put(`${B}/international/${id}/shipment`, dto).then(unwrap)
export const requestTt        = (id, dto) => api.post(`${B}/international/${id}/tt/request`, dto).then(unwrap)
export const approveTt        = (id) => api.post(`${B}/international/${id}/tt/approve`).then(unwrap)
export const sendTt           = (id) => api.post(`${B}/international/${id}/tt/send`).then(unwrap)
export const addLandedCost    = (id, dto) => api.post(`${B}/international/${id}/components`, dto).then(unwrap)
export const removeLandedCost = (componentId) => api.delete(`${B}/international/components/${componentId}`).then(unwrap)
export const declareCustoms   = (id, dto) => api.post(`${B}/international/${id}/customs`, dto).then(unwrap)
export const finaliseLandedCost = (id) => api.post(`${B}/international/${id}/finalise-cost`).then(unwrap)

// ── P8: emergency procurement (PROC-004) ──
// Sourcing waived only: MD authorises before the purchase, justification + waiver mandatory,
// post-hoc PR due within 24h, every case reported in the monthly board pack.
export const listEmergencies   = (params) => api.get(`${B}/emergency`, { params }).then(r => r.data)
export const getEmergency      = (id) => api.get(`${B}/emergency/${id}`).then(unwrap)
export const getEmergencyByPo  = (poId) => api.get(`${B}/emergency/by-po/${poId}`).then(unwrap).catch(() => null)
export const emergencySummary  = () => api.get(`${B}/emergency/summary`).then(unwrap)
export const emergencyBoardPack = (period) => api.get(`${B}/emergency/board-pack`, { params: { period } }).then(unwrap)
export const declareEmergency  = (dto) => api.post(`${B}/emergency/declare`, dto).then(unwrap)
export const mdApproveEmergency = (id, dto) => api.post(`${B}/emergency/${id}/md-approve`, dto).then(unwrap)
export const raisePostHocPr    = (id, dto) => api.post(`${B}/emergency/${id}/post-hoc-pr`, dto ?? {}).then(unwrap)
export const markEmergencyBoardPack = (id, dto) => api.post(`${B}/emergency/${id}/board-pack`, dto).then(unwrap)

// ── P9: supplier performance review (PROC-002) ──
// Biannual auto-scoring from transaction data: Quality 30 / Delivery 25 / Pricing 25 / Compliance 20.
// <60 warns, <40 escalates to the MD (who blacklists via the ASR — the review never does).
export const listReviews      = (params) => api.get(`${B}/performance`, { params }).then(r => r.data)
export const getReview        = (id) => api.get(`${B}/performance/${id}`).then(unwrap)
export const reviewSummary    = (period) => api.get(`${B}/performance/summary`, { params: { period } }).then(unwrap)
export const supplierScoreHistory = (supplierId) => api.get(`${B}/performance/supplier/${supplierId}`).then(unwrap)
export const runReviews       = (dto) => api.post(`${B}/performance/run`, dto ?? {}).then(unwrap)
export const runSupplierReview = (supplierId, dto) => api.post(`${B}/performance/run/${supplierId}`, dto ?? {}).then(unwrap)
export const escalateReview   = (id, dto) => api.post(`${B}/performance/${id}/escalate`, dto ?? {}).then(unwrap)
