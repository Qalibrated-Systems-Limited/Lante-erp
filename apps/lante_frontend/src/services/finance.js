// Finance API service — the single place finance endpoint strings live.
// Each function returns the unwrapped `data` payload (ApiResponse<T>.data).
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

// ── Chart of accounts / config ──
export const getChartOfAccounts = () => api.get('/api/v1/finance/chart-of-accounts').then(unwrap)
export const createAccount      = (dto) => api.post('/api/v1/finance/chart-of-accounts', dto).then(unwrap)
export const getCurrencies      = () => api.get('/api/v1/finance/currencies').then(unwrap)
export const refreshRates       = () => api.post('/api/v1/finance/currencies/refresh-rates').then(unwrap)
export const setCurrencyRate    = (id, rate) => api.post(`/api/v1/finance/currencies/${id}/rate`, { rate }).then(unwrap)
export const getFiscalYears     = () => api.get('/api/v1/finance/fiscal-years').then(unwrap)
export const closePeriod        = (id) => api.post(`/api/v1/finance/periods/${id}/close`).then(unwrap)
export const getCostCentres     = () => api.get('/api/v1/finance/cost-centres').then(unwrap)

// ── Journals ──
export const listJournals   = () => api.get('/api/v1/finance/journals').then(unwrap)
export const getJournal     = (id) => api.get(`/api/v1/finance/journals/${id}`).then(unwrap)
export const createJournal  = (dto) => api.post('/api/v1/finance/journals', dto).then(unwrap)
export const submitJournal  = (id) => api.post(`/api/v1/finance/journals/${id}/submit`).then(unwrap)
export const reviewJournal  = (id) => api.post(`/api/v1/finance/journals/${id}/review`).then(unwrap)
export const approveJournal = (id) => api.post(`/api/v1/finance/journals/${id}/approve`).then(unwrap)
export const reverseJournal = (id) => api.post(`/api/v1/finance/journals/${id}/reverse`).then(unwrap)

// ── Accounts Receivable ──
export const getCustomers    = () => api.get('/api/v1/finance/customers').then(unwrap)
export const createCustomer  = (dto) => api.post('/api/v1/finance/customers', dto).then(unwrap)
export const getTaxCategories = () => api.get('/api/v1/finance/tax-categories').then(unwrap)
export const listInvoices    = () => api.get('/api/v1/finance/invoices').then(unwrap)
export const getInvoice      = (id) => api.get(`/api/v1/finance/invoices/${id}`).then(unwrap)
export const createInvoice   = (dto) => api.post('/api/v1/finance/invoices', dto).then(unwrap)
export const issueInvoice    = (id) => api.post(`/api/v1/finance/invoices/${id}/issue`).then(unwrap)
// Reason is required by the API, not optional — it rejects blank. See documentActions.js for when
// the UI should offer this at all.
export const cancelInvoice   = (id, reason) => api.post(`/api/v1/finance/invoices/${id}/cancel`, { reason }).then(unwrap)
export const getDebtorAging  = (params) => api.get('/api/v1/finance/debtors/aging', { params }).then(unwrap)
export const listReceipts    = () => api.get('/api/v1/finance/receipts').then(unwrap)
export const createReceipt   = (dto) => api.post('/api/v1/finance/receipts', dto).then(unwrap)
export const computeVat      = (periodId) => api.get('/api/v1/finance/vat/compute', { params: { periodId } }).then(unwrap)
export const fileVat         = (periodId) => api.post('/api/v1/finance/vat/file', null, { params: { periodId } }).then(unwrap)

// ── Accounts Payable ──
export const getSuppliers          = () => api.get('/api/v1/finance/suppliers').then(unwrap)
export const createSupplier        = (dto) => api.post('/api/v1/finance/suppliers', dto).then(unwrap)
export const getPaymentAuthority   = () => api.get('/api/v1/finance/payment-authority').then(unwrap)
export const listSupplierInvoices  = () => api.get('/api/v1/finance/supplier-invoices').then(unwrap)
export const createSupplierInvoice = (dto) => api.post('/api/v1/finance/supplier-invoices', dto).then(unwrap)
export const approveSupplierInvoice = (id) => api.post(`/api/v1/finance/supplier-invoices/${id}/approve`).then(unwrap)
export const cancelSupplierInvoice  = (id, reason) => api.post(`/api/v1/finance/supplier-invoices/${id}/cancel`, { reason }).then(unwrap)
export const listVouchers          = () => api.get('/api/v1/finance/vouchers').then(unwrap)
export const createVoucher         = (dto) => api.post('/api/v1/finance/vouchers', dto).then(unwrap)
export const approveVoucher        = (id) => api.post(`/api/v1/finance/vouchers/${id}/approve`).then(unwrap)
export const payVoucher            = (id) => api.post(`/api/v1/finance/vouchers/${id}/pay`).then(unwrap)

// ── Budgets ──
export const listBudgets       = (fiscalYearId) => api.get('/api/v1/finance/budgets', { params: { fiscalYearId } }).then(unwrap)
export const createBudget      = (dto) => api.post('/api/v1/finance/budgets', dto).then(unwrap)
export const listRevenueTargets = (fiscalYearId) => api.get('/api/v1/finance/revenue-targets', { params: { fiscalYearId } }).then(unwrap)
export const createRevenueTarget = (dto) => api.post('/api/v1/finance/revenue-targets', dto).then(unwrap)

// ── Month-End & P&L ──
export const getPeriodClose   = (periodId) => api.get(`/api/v1/finance/month-end/${periodId}`).then(unwrap)
export const toggleChecklist  = (itemId, complete) => api.post(`/api/v1/finance/month-end/checklist/${itemId}`, null, { params: { complete } }).then(unwrap)
export const closeMonthEnd    = (periodId) => api.post(`/api/v1/finance/month-end/${periodId}/close`).then(unwrap)
export const reopenMonthEnd   = (periodId) => api.post(`/api/v1/finance/month-end/${periodId}/reopen`).then(unwrap)
export const getProfitAndLoss = (periodId) => api.get(`/api/v1/finance/month-end/${periodId}/pnl`).then(unwrap)

// ── Cash Flow ──
export const getCashFlow = (params) => api.get('/api/v1/finance/cash-flow', { params }).then(unwrap)

// ── Imprest (Process 14) ──
export const listImprest       = () => api.get('/api/v1/finance/imprest').then(unwrap)
export const listAdvances      = () => api.get('/api/v1/finance/imprest/advances').then(unwrap)
export const requestImprest    = (dto) => api.post('/api/v1/finance/imprest', dto).then(unwrap)
export const approveImprest    = (id) => api.post(`/api/v1/finance/imprest/${id}/approve`).then(unwrap)
export const disburseImprest   = (id) => api.post(`/api/v1/finance/imprest/${id}/disburse`).then(unwrap)
export const retireImprest     = (id, dto) => api.post(`/api/v1/finance/imprest/${id}/retire`, dto).then(unwrap)
export const runImprestConversions = (asOf) => api.post('/api/v1/finance/imprest/run-conversions', null, { params: { asOf } }).then(unwrap)

// ── Bank Reconciliation (Processes 9/10) ──
export const listReconciliations = () => api.get('/api/v1/finance/bank-rec').then(unwrap)
export const getReconciliation   = (id) => api.get(`/api/v1/finance/bank-rec/${id}`).then(unwrap)
export const createReconciliation = (dto) => api.post('/api/v1/finance/bank-rec', dto).then(unwrap)
export const matchStatementLine  = (id, lineId, glEntryId) => api.post(`/api/v1/finance/bank-rec/${id}/lines/${lineId}/match`, null, { params: { glEntryId } }).then(unwrap)
export const unmatchStatementLine = (id, lineId) => api.post(`/api/v1/finance/bank-rec/${id}/lines/${lineId}/unmatch`).then(unwrap)
export const postBankItem        = (id, lineId, dto) => api.post(`/api/v1/finance/bank-rec/${id}/lines/${lineId}/post`, dto).then(unwrap)
export const completeReconciliation = (id) => api.post(`/api/v1/finance/bank-rec/${id}/complete`).then(unwrap)

// ── Statutory (FIN-025) ──
export const getObligations    = (period) => api.get('/api/v1/finance/statutory/obligations', { params: { period } }).then(unwrap)
export const listRemittances   = () => api.get('/api/v1/finance/statutory/remittances').then(unwrap)
export const remitStatutory    = (dto) => api.post('/api/v1/finance/statutory/remit', dto).then(unwrap)

// ── Reports ──
export const getTrialBalance = (params) => api.get('/api/v1/finance/trial-balance', { params }).then(unwrap)

// ── Fixed Assets & Depreciation (ASSET-001..008) ──
export const listAssetCategories  = () => api.get('/api/v1/finance/fixed-assets/categories').then(unwrap)
export const listFixedAssets      = () => api.get('/api/v1/finance/fixed-assets').then(unwrap)
export const createFixedAsset     = (dto) => api.post('/api/v1/finance/fixed-assets', dto).then(unwrap)
export const runDepreciation      = (period) => api.post('/api/v1/finance/fixed-assets/depreciation/run', null, { params: { period } }).then(unwrap)
export const getDepreciationSchedule = (period) => api.get('/api/v1/finance/fixed-assets/depreciation/schedule', { params: { period } }).then(unwrap)
export const listAssetDisposals   = () => api.get('/api/v1/finance/fixed-assets/disposals').then(unwrap)
export const disposeAsset         = (assetId, dto) => api.post(`/api/v1/finance/fixed-assets/${assetId}/dispose`, dto).then(unwrap)
export const approveDisposalMd    = (id) => api.post(`/api/v1/finance/fixed-assets/disposals/${id}/approve-md`).then(unwrap)
export const approveDisposalBoard = (id) => api.post(`/api/v1/finance/fixed-assets/disposals/${id}/approve-board`).then(unwrap)

// Cross-service (Fleet) — for the Motor Vehicles asset-link dropdown (ASSET-008).
export const listTrucks = () => api.get('/api/v1/trucks').then(r => r.data?.data)

// TrucksController.Update is a full-replace PUT (LicensePlate/Model required) — the caller must
// pass the truck's current record, not just the assetId, per the CreateTruckRequest contract.
export const linkFixedAssetToTruck = (truck, assetId) => api.put(`/api/v1/trucks/${truck.id}`, {
  licensePlate: truck.licensePlate, model: truck.model, driverId: truck.driverId ?? null,
  vehicleClassId: truck.vehicleClassId ?? null, insuranceExpiryDate: truck.insuranceExpiryDate ?? null,
  nextServiceDate: truck.nextServiceDate ?? null, odometer: truck.odometer ?? null, status: truck.status,
  assetId,
})
