import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, HelpPanel, DocumentPreview, Tabs, SectionHeader, DataTable, Modal, Input, FileInput, Select, Loading, MiniStat, worstVariant } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Subcontractor Engagement & Prequalification — backed by the subcontracts-service
// microservice (packages/microservices/subcontracts) via the gateway's
// /api/v1/subcontractors, /prequalifications, /subcontract-awards,
// /subcon-scorecards and /payment-retentions routes. Same shell/tabs/KPI
// convention as every other module page.
//
// SUB-005 hard gate: mobilization can only be activated once HSE confirms an
// Approved RAMS record exists for the subcontractor — enforced server-side by
// SubcontractsService's AwardWorkflowService, not just in this UI.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20

const PQQ_STATUSES = ['Sent', 'Completed', 'PendingApproval', 'Approved', 'Rejected']
const PQQ_STATUS_LABELS = { Sent: 'Sent', Completed: 'Completed', PendingApproval: 'Pending Approval', Approved: 'Approved', Rejected: 'Rejected' }
const AWARD_STATUSES = ['PendingApproval', 'Approved', 'Mobilized', 'Completed', 'Terminated']
const AWARD_STATUS_LABELS = { PendingApproval: 'Pending Approval', Approved: 'Approved', Mobilized: 'Mobilized', Completed: 'Completed', Terminated: 'Terminated' }
const RETENTION_STATUSES = ['Certified', 'Paid']

const pqqVariant = s => s === 'Approved' ? 'green' : s === 'Rejected' ? 'red' : s === 'PendingApproval' ? 'amber' : 'default'
const awardVariant = s => s === 'Mobilized' || s === 'Completed' ? 'green' : s === 'Terminated' ? 'red' : s === 'Approved' ? 'blue' : 'amber'

const today = () => new Date().toISOString().slice(0, 10)

const EMPTY_SUBCONTRACTOR = { name: '', tradeCategory: '', insuranceExpiry: '', tccExpiry: '', hasDeclaredRelationship: false, relationshipDetails: '', notes: '' }
const EMPTY_PQQ = { subcontractorId: '', documentUrl: '', submittedOn: today() }
const EMPTY_PQQ_APPROVE = { score: '' }
const EMPTY_AWARD = { subcontractorId: '', projectId: '', value: '', signedAgreementUrl: '' }
const EMPTY_SCORECARD = { awardId: '', projectManagerName: '', score: '', completedOn: today(), notes: '' }
const EMPTY_RETENTION = { awardId: '', certifiedAmount: '', retentionHeld: '', wht: '' }

const DELETE_TITLES = {
  subcontractor: 'Delete Subcontractor',
  pqq: 'Delete Prequalification',
  award: 'Delete Award',
  scorecard: 'Delete Scorecard',
  retention: 'Delete Payment Record',
}
const DELETE_ENDPOINTS = {
  subcontractor: id => `/api/v1/subcontractors/${id}`,
  pqq: id => `/api/v1/prequalifications/${id}`,
  award: id => `/api/v1/subcontract-awards/${id}`,
  scorecard: id => `/api/v1/subcon-scorecards/${id}`,
  retention: id => `/api/v1/payment-retentions/${id}`,
}
const DELETE_SUCCESS = {
  subcontractor: 'Subcontractor deleted.',
  pqq: 'Prequalification deleted.',
  award: 'Award deleted.',
  scorecard: 'Scorecard deleted.',
  retention: 'Payment record deleted.',
}
const DELETE_FAIL = {
  subcontractor: 'Failed to delete subcontractor.',
  pqq: 'Failed to delete prequalification.',
  award: 'Failed to delete award.',
  scorecard: 'Failed to delete scorecard.',
  retention: 'Failed to delete payment record.',
}

export default function SubcontractsPage() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission('subcontracts.write')
  const canApprove = hasPermission('subcontracts.approve')
  const canDelete = hasPermission('subcontracts.delete')

  const [tab, setTab] = useState('subcontractors')
  const [modal, setModal] = useState(null)
  const [msg, setMsg] = useState(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(null) // 'prequalifications' | 'awards' | null

  // Per-tab paginated data — each register's own DataTable rows for the currently active page.
  const [subcontractors, setSubcontractors] = useState([])
  const [prequalifications, setPrequalifications] = useState([])
  const [awards, setAwards] = useState([])
  const [scorecards, setScorecards] = useState([])
  const [retentions, setRetentions] = useState([])
  const [projects, setProjects] = useState([])

  // Independent pagination state per register.
  const [subPage, setSubPage] = useState(1)
  const [subTotalCount, setSubTotalCount] = useState(0)
  const [pqqPage, setPqqPage] = useState(1)
  const [pqqTotalCount, setPqqTotalCount] = useState(0)
  const [awardPage, setAwardPage] = useState(1)
  const [awardTotalCount, setAwardTotalCount] = useState(0)
  const [scorecardPage, setScorecardPage] = useState(1)
  const [scorecardTotalCount, setScorecardTotalCount] = useState(0)
  const [retentionPage, setRetentionPage] = useState(1)
  const [retentionTotalCount, setRetentionTotalCount] = useState(0)

  // Unpaged (capped at the backend's max pageSize of 100) lookup lists — used for cross-tab
  // name/label lookups, Select dropdown options, and KPI counts that must reflect more than
  // just whatever page a register's table happens to be on. If a register ever exceeds 100
  // records, lookups/KPIs derived from these arrays only reflect the first 100 — there's no
  // true "all" or count-only endpoint on the backend to fall back to. Per-register *TotalCount
  // state above is always exact (it comes straight from the backend's TotalCount), so it's used
  // instead wherever a raw count (not a filtered/derived one) is all that's needed.
  const [allSubcontractors, setAllSubcontractors] = useState([])
  const [allPrequalifications, setAllPrequalifications] = useState([])
  const [allAwards, setAllAwards] = useState([])

  const [subForm, setSubForm] = useState(EMPTY_SUBCONTRACTOR)
  const [pqqForm, setPqqForm] = useState(EMPTY_PQQ)
  const [pqqApproveForm, setPqqApproveForm] = useState(EMPTY_PQQ_APPROVE)
  const [awardForm, setAwardForm] = useState(EMPTY_AWARD)
  const [scorecardForm, setScorecardForm] = useState(EMPTY_SCORECARD)
  const [retentionForm, setRetentionForm] = useState(EMPTY_RETENTION)
  const [activePqqId, setActivePqqId] = useState(null)
  const [activeAwardId, setActiveAwardId] = useState(null)

  const [viewingSub, setViewingSub] = useState(null)
  const [editingSub, setEditingSub] = useState(false)
  const [subEditForm, setSubEditForm] = useState(EMPTY_SUBCONTRACTOR)

  const [viewingPqq, setViewingPqq] = useState(null)
  const [editingPqq, setEditingPqq] = useState(false)
  const [pqqEditForm, setPqqEditForm] = useState(EMPTY_PQQ)

  const [viewingAward, setViewingAward] = useState(null)
  const [editingAward, setEditingAward] = useState(false)
  const [awardEditForm, setAwardEditForm] = useState(EMPTY_AWARD)

  const [viewingScorecard, setViewingScorecard] = useState(null)
  const [editingScorecard, setEditingScorecard] = useState(false)
  const [scorecardEditForm, setScorecardEditForm] = useState(EMPTY_SCORECARD)

  const [viewingRetention, setViewingRetention] = useState(null)
  const [editingRetention, setEditingRetention] = useState(false)
  const [retentionEditForm, setRetentionEditForm] = useState(EMPTY_RETENTION)

  const [previewUrl, setPreviewUrl] = useState(null)

  const [deleteTarget, setDeleteTarget] = useState(null) // { type: 'subcontractor'|'pqq'|'award'|'scorecard'|'retention', row }

  const emptyPaged = { data: { data: { items: [], totalCount: 0 } } }

  async function uploadSubcontractsFile(folder, file) {
    if (!file) return null
    setUploading(folder)
    try {
      const form = new FormData()
      form.append('folder', folder)
      form.append('file', file)
      const res = await api.post('/api/v1/subcontracts-uploads', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      return res.data?.data?.url ?? null
    } catch {
      setMsg({ type: 'error', text: 'File upload failed.' })
      return null
    } finally {
      setUploading(null)
    }
  }

  // Tracks the 6 independent first-loads (5 registers + lookups) so the page-level `loading`
  // spinner clears once every register/lookup has loaded at least once, without being retriggered
  // by a later page change on just one register.
  const initialLoadsRemaining = useRef(6)
  function markInitialLoadDone() {
    if (initialLoadsRemaining.current > 0) {
      initialLoadsRemaining.current -= 1
      if (initialLoadsRemaining.current === 0) setLoading(false)
    }
  }

  const loadSubcontractors = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/subcontractors', { params: { page: subPage, pageSize: PAGE_SIZE } })
      const data = res.data?.data || {}
      setSubcontractors(data.items ?? [])
      setSubTotalCount(data.totalCount ?? 0)
    } catch { setMsg({ type: 'error', text: 'Failed to load subcontractors.' }) }
    finally { markInitialLoadDone() }
  }, [subPage])

  const loadPrequalifications = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/prequalifications', { params: { page: pqqPage, pageSize: PAGE_SIZE } })
      const data = res.data?.data || {}
      setPrequalifications(data.items ?? [])
      setPqqTotalCount(data.totalCount ?? 0)
    } catch { setMsg({ type: 'error', text: 'Failed to load prequalifications.' }) }
    finally { markInitialLoadDone() }
  }, [pqqPage])

  const loadAwards = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/subcontract-awards', { params: { page: awardPage, pageSize: PAGE_SIZE } })
      const data = res.data?.data || {}
      setAwards(data.items ?? [])
      setAwardTotalCount(data.totalCount ?? 0)
    } catch { setMsg({ type: 'error', text: 'Failed to load awards.' }) }
    finally { markInitialLoadDone() }
  }, [awardPage])

  const loadScorecards = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/subcon-scorecards', { params: { page: scorecardPage, pageSize: PAGE_SIZE } })
      const data = res.data?.data || {}
      setScorecards(data.items ?? [])
      setScorecardTotalCount(data.totalCount ?? 0)
    } catch { setMsg({ type: 'error', text: 'Failed to load scorecards.' }) }
    finally { markInitialLoadDone() }
  }, [scorecardPage])

  const loadRetentions = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/payment-retentions', { params: { page: retentionPage, pageSize: PAGE_SIZE } })
      const data = res.data?.data || {}
      setRetentions(data.items ?? [])
      setRetentionTotalCount(data.totalCount ?? 0)
    } catch { setMsg({ type: 'error', text: 'Failed to load payment retentions.' }) }
    finally { markInitialLoadDone() }
  }, [retentionPage])

  const loadLookups = useCallback(async () => {
    try {
      const [subRes, pqqRes, awardRes, projectRes] = await Promise.all([
        api.get('/api/v1/subcontractors', { params: { pageSize: 100 } }).catch(() => emptyPaged),
        api.get('/api/v1/prequalifications', { params: { pageSize: 100 } }).catch(() => emptyPaged),
        api.get('/api/v1/subcontract-awards', { params: { pageSize: 100 } }).catch(() => emptyPaged),
        api.get('/api/v1/projects?pageSize=200').catch(() => ({ data: { data: { items: [] } } })),
      ])
      setAllSubcontractors(subRes.data?.data?.items ?? [])
      setAllPrequalifications(pqqRes.data?.data?.items ?? [])
      setAllAwards(awardRes.data?.data?.items ?? [])
      setProjects(projectRes.data?.data?.items ?? [])
    } catch { /* non-fatal — per-tab table data still loads independently */ }
    finally { markInitialLoadDone() }
  }, [])

  // Each register refetches only its own page when its own page state changes, independent of
  // the other 4 registers and of which tab is currently active. All 6 also run once on mount.
  useEffect(() => { loadSubcontractors() }, [loadSubcontractors])
  useEffect(() => { loadPrequalifications() }, [loadPrequalifications])
  useEffect(() => { loadAwards() }, [loadAwards])
  useEffect(() => { loadScorecards() }, [loadScorecards])
  useEffect(() => { loadRetentions() }, [loadRetentions])
  useEffect(() => { loadLookups() }, [loadLookups])

  // Full manual refresh — used after every create/update/delete/status-change action so that a
  // mutation on one register also refreshes the cross-tab lookup/KPI data (names, dropdown
  // options, counts) that may depend on it.
  async function load() {
    setLoading(true)
    try {
      await Promise.all([
        loadSubcontractors(), loadPrequalifications(), loadAwards(), loadScorecards(), loadRetentions(), loadLookups(),
      ])
    } finally {
      setLoading(false)
    }
  }

  function subcontractorName(id) { return allSubcontractors.find(s => s.id === id)?.name ?? id }
  function awardLabel(id) { const a = allAwards.find(a => a.id === id); return a ? `${a.subcontractorName ?? subcontractorName(a.subcontractorId)} — ${a.projectName ?? 'Project'}` : id }
  function projectName(id) { return projects.find(p => p.id === id)?.name ?? '' }

  function deleteTargetLabel() {
    if (!deleteTarget) return ''
    const { type, row } = deleteTarget
    if (type === 'subcontractor') return row.name
    if (type === 'pqq') return subcontractorName(row.subcontractorId)
    if (type === 'award') return row.subcontractorName ?? subcontractorName(row.subcontractorId)
    if (type === 'scorecard') return awardLabel(row.awardId)
    if (type === 'retention') return awardLabel(row.awardId)
    return ''
  }

  async function submitSubcontractor() {
    if (!subForm.name || !subForm.tradeCategory) return
    setSaving(true)
    try {
      await api.post('/api/v1/subcontractors', {
        name: subForm.name, tradeCategory: subForm.tradeCategory,
        insuranceExpiry: subForm.insuranceExpiry ? new Date(subForm.insuranceExpiry).toISOString() : null,
        tccExpiry: subForm.tccExpiry ? new Date(subForm.tccExpiry).toISOString() : null,
        hasDeclaredRelationship: subForm.hasDeclaredRelationship,
        relationshipDetails: subForm.relationshipDetails || null,
        notes: subForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Subcontractor added to the ASR.' })
      setSubForm(EMPTY_SUBCONTRACTOR); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add subcontractor.' })
    } finally { setSaving(false) }
  }

  async function submitPqq() {
    if (!pqqForm.subcontractorId) return
    setSaving(true)
    try {
      await api.post('/api/v1/prequalifications', {
        subcontractorId: pqqForm.subcontractorId,
        documentUrl: pqqForm.documentUrl || null,
        submittedOn: pqqForm.submittedOn ? new Date(pqqForm.submittedOn).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'Prequalification recorded.' })
      setPqqForm(EMPTY_PQQ); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record prequalification.' })
    } finally { setSaving(false) }
  }

  function openPqqApprove(p) {
    setActivePqqId(p.id)
    setPqqApproveForm({ score: p.score ?? '' })
    setModal('pqqApprove')
  }

  async function submitPqqApprove() {
    setSaving(true)
    try {
      await api.patch(`/api/v1/prequalifications/${activePqqId}/approve`, { score: Number(pqqApproveForm.score) || 0 })
      setMsg({ type: 'success', text: 'Prequalification approved — ASR updated.' })
      setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to approve prequalification.' })
    } finally { setSaving(false) }
  }

  async function rejectPqq(id) {
    setSaving(true)
    try {
      await api.patch(`/api/v1/prequalifications/${id}/reject`)
      setMsg({ type: 'success', text: 'Prequalification rejected.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to reject prequalification.' })
    } finally { setSaving(false) }
  }

  async function submitAward() {
    if (!awardForm.subcontractorId || !awardForm.value) return
    setSaving(true)
    try {
      await api.post('/api/v1/subcontract-awards', {
        subcontractorId: awardForm.subcontractorId,
        subcontractorName: subcontractorName(awardForm.subcontractorId),
        projectId: awardForm.projectId || null,
        projectName: awardForm.projectId ? projectName(awardForm.projectId) : null,
        value: Number(awardForm.value) || 0,
        signedAgreementUrl: awardForm.signedAgreementUrl || null,
      })
      setMsg({ type: 'success', text: 'Award created — pending approval.' })
      setAwardForm(EMPTY_AWARD); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to create award.' })
    } finally { setSaving(false) }
  }

  async function setAwardStatus(id, status) {
    setSaving(true)
    try {
      await api.patch(`/api/v1/subcontract-awards/${id}/status`, { status: AWARD_STATUSES.indexOf(status) })
      setMsg({ type: 'success', text: `Award marked ${AWARD_STATUS_LABELS[status]}.` })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update award status.' })
    } finally { setSaving(false) }
  }

  async function activateMobilization(id) {
    setSaving(true)
    try {
      await api.patch(`/api/v1/subcontract-awards/${id}/activate-mobilization`)
      setMsg({ type: 'success', text: 'Mobilization activated — RAMS approval confirmed.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Mobilization blocked — RAMS not yet approved by HSE, or the subcontractor is restricted.' })
    } finally { setSaving(false) }
  }

  async function submitScorecard() {
    if (!scorecardForm.awardId || !scorecardForm.score) return
    setSaving(true)
    try {
      await api.post('/api/v1/subcon-scorecards', {
        awardId: scorecardForm.awardId,
        projectManagerName: scorecardForm.projectManagerName || null,
        score: Number(scorecardForm.score) || 0,
        completedOn: new Date(scorecardForm.completedOn).toISOString(),
        notes: scorecardForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Scorecard recorded — ASR performance updated.' })
      setScorecardForm(EMPTY_SCORECARD); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record scorecard.' })
    } finally { setSaving(false) }
  }

  async function submitRetention() {
    if (!retentionForm.awardId || !retentionForm.certifiedAmount) return
    setSaving(true)
    try {
      await api.post('/api/v1/payment-retentions', {
        awardId: retentionForm.awardId,
        certifiedAmount: Number(retentionForm.certifiedAmount) || 0,
        retentionHeld: Number(retentionForm.retentionHeld) || 0,
        wht: Number(retentionForm.wht) || 0,
      })
      setMsg({ type: 'success', text: 'Payment certified.' })
      setRetentionForm(EMPTY_RETENTION); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to certify payment.' })
    } finally { setSaving(false) }
  }

  async function markPaid(id) {
    setSaving(true)
    try {
      await api.patch(`/api/v1/payment-retentions/${id}/mark-paid`)
      setMsg({ type: 'success', text: 'Payment marked as paid.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to mark payment as paid.' })
    } finally { setSaving(false) }
  }

  function openSubView(s) {
    setViewingSub(s); setEditingSub(false)
    setSubEditForm({
      name: s.name, tradeCategory: s.tradeCategory,
      insuranceExpiry: s.insuranceExpiry ? s.insuranceExpiry.slice(0, 10) : '',
      tccExpiry: s.tccExpiry ? s.tccExpiry.slice(0, 10) : '',
      hasDeclaredRelationship: !!s.hasDeclaredRelationship,
      relationshipDetails: s.relationshipDetails ?? '', notes: s.notes ?? '',
    })
  }
  async function submitSubEdit() {
    if (!viewingSub || !subEditForm.name || !subEditForm.tradeCategory) return
    setSaving(true)
    try {
      await api.put(`/api/v1/subcontractors/${viewingSub.id}`, {
        name: subEditForm.name, tradeCategory: subEditForm.tradeCategory,
        safetyScore: viewingSub.safetyScore ?? 0, ramsSubmitted: !!viewingSub.ramsSubmitted,
        insuranceExpiry: subEditForm.insuranceExpiry ? new Date(subEditForm.insuranceExpiry).toISOString() : null,
        tccExpiry: subEditForm.tccExpiry ? new Date(subEditForm.tccExpiry).toISOString() : null,
        hasDeclaredRelationship: subEditForm.hasDeclaredRelationship,
        relationshipDetails: subEditForm.relationshipDetails || null,
        notes: subEditForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Subcontractor updated.' })
      setViewingSub(null); setEditingSub(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update subcontractor.' })
    } finally { setSaving(false) }
  }

  function openPqqView(p) {
    setViewingPqq(p); setEditingPqq(false)
    setPqqEditForm({ subcontractorId: p.subcontractorId, documentUrl: p.documentUrl ?? '', submittedOn: p.submittedOn ? p.submittedOn.slice(0, 10) : '' })
  }
  async function submitPqqEdit() {
    if (!viewingPqq) return
    setSaving(true)
    try {
      await api.put(`/api/v1/prequalifications/${viewingPqq.id}`, {
        documentUrl: pqqEditForm.documentUrl || null,
        submittedOn: pqqEditForm.submittedOn ? new Date(pqqEditForm.submittedOn).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'Prequalification updated.' })
      setViewingPqq(null); setEditingPqq(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update prequalification.' })
    } finally { setSaving(false) }
  }

  function openAwardView(a) {
    setViewingAward(a); setEditingAward(false)
    setAwardEditForm({ subcontractorId: a.subcontractorId, projectId: a.projectId ?? '', value: a.value, signedAgreementUrl: a.signedAgreementUrl ?? '' })
  }
  async function submitAwardEdit() {
    if (!viewingAward || !awardEditForm.subcontractorId || !awardEditForm.value) return
    setSaving(true)
    try {
      await api.put(`/api/v1/subcontract-awards/${viewingAward.id}`, {
        subcontractorId: awardEditForm.subcontractorId,
        subcontractorName: subcontractorName(awardEditForm.subcontractorId),
        projectId: awardEditForm.projectId || null,
        projectName: awardEditForm.projectId ? projectName(awardEditForm.projectId) : null,
        value: Number(awardEditForm.value) || 0,
        signedAgreementUrl: awardEditForm.signedAgreementUrl || null,
      })
      setMsg({ type: 'success', text: 'Award updated.' })
      setViewingAward(null); setEditingAward(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update award.' })
    } finally { setSaving(false) }
  }

  function openScorecardView(s) {
    setViewingScorecard(s); setEditingScorecard(false)
    setScorecardEditForm({ awardId: s.awardId, projectManagerName: s.projectManagerName ?? '', score: s.score, completedOn: s.completedOn ? s.completedOn.slice(0, 10) : '', notes: s.notes ?? '' })
  }
  async function submitScorecardEdit() {
    if (!viewingScorecard || !scorecardEditForm.score) return
    setSaving(true)
    try {
      await api.put(`/api/v1/subcon-scorecards/${viewingScorecard.id}`, {
        projectManagerName: scorecardEditForm.projectManagerName || null,
        score: Number(scorecardEditForm.score) || 0,
        completedOn: new Date(scorecardEditForm.completedOn).toISOString(),
        notes: scorecardEditForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Scorecard updated.' })
      setViewingScorecard(null); setEditingScorecard(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update scorecard.' })
    } finally { setSaving(false) }
  }

  function openRetentionView(r) {
    setViewingRetention(r); setEditingRetention(false)
    setRetentionEditForm({ awardId: r.awardId, certifiedAmount: r.certifiedAmount, retentionHeld: r.retentionHeld, wht: r.wht })
  }
  async function submitRetentionEdit() {
    if (!viewingRetention || !retentionEditForm.certifiedAmount) return
    setSaving(true)
    try {
      await api.put(`/api/v1/payment-retentions/${viewingRetention.id}`, {
        certifiedAmount: Number(retentionEditForm.certifiedAmount) || 0,
        retentionHeld: Number(retentionEditForm.retentionHeld) || 0,
        wht: Number(retentionEditForm.wht) || 0,
      })
      setMsg({ type: 'success', text: 'Payment record updated.' })
      setViewingRetention(null); setEditingRetention(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update payment record.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    const { type, row } = deleteTarget
    setSaving(true)
    try {
      await api.delete(DELETE_ENDPOINTS[type](row.id))
      setMsg({ type: 'success', text: DELETE_SUCCESS[type] })
      setDeleteTarget(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? DELETE_FAIL[type] })
    } finally { setSaving(false) }
  }

  const restrictedCount = allSubcontractors.filter(s => s.isRestricted).length
  const watchListedCount = allSubcontractors.filter(s => s.watchListed).length
  const pendingPqqCount = allPrequalifications.filter(p => p.status === 'Sent' || p.status === 'Completed' || p.status === 'PendingApproval').length
  const mobilizedCount = allAwards.filter(a => a.status === 'Mobilized').length
  const subcontractsSeverity = worstVariant(
    pendingPqqCount > 0 ? 'amber' : 'green',
    restrictedCount > 0 ? 'red' : 'green',
    watchListedCount > 0 ? 'amber' : 'green',
  )

  const TABS = [
    { id: 'subcontractors', label: 'Subcontractors (ASR)' },
    { id: 'prequalifications', label: 'Prequalifications' },
    { id: 'awards', label: 'Awards & Mobilization' },
    { id: 'scorecards', label: 'Scorecards' },
    { id: 'retentions', label: 'Payment Retentions' },
  ]

  return (
    <>
      <div style={{ padding: PAD }}>
        <SectionHeader title="Subcontractor Engagement & Prequalification" sub="Approved Subcontractor Register, PQQ approvals, awards, mobilization gate, performance and payments" />

        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        {tab === 'subcontractors' && (
          <HelpPanel title="About the Approved Subcontractor Register (ASR)" variant={subcontractsSeverity}>
            The single source of truth for every subcontractor's safety score, RAMS submission,
            performance history and prequalification status — HSE and Operations both reference
            this register. Click a row to see the full record and edit it. Prequalified status
            itself only changes via the Prequalifications tab's approval workflow.
          </HelpPanel>
        )}
        {tab === 'prequalifications' && (
          <HelpPanel title="About Prequalifications (PQQ)" variant={subcontractsSeverity}>
            Every subcontractor must complete a Prequalification Questionnaire before award —
            approval by the Head of Projects automatically updates the ASR's prequalified
            status. Click a row to see the full record (including the PQQ document) and edit
            its submission details; Approve/Reject move the workflow forward and aren't
            editable here.
          </HelpPanel>
        )}
        {tab === 'awards' && (
          <HelpPanel title="About Awards & Mobilization" variant={subcontractsSeverity}>
            Tracks each subcontract award, its value and signed agreement, through to site
            mobilization — mobilization is hard-gated server-side on HSE confirming an Approved
            RAMS record for that subcontractor. Click a row to see the full record (including
            the signed agreement) and edit its details; status/mobilization move forward via
            the action buttons, not this edit.
          </HelpPanel>
        )}
        {tab === 'scorecards' && (
          <HelpPanel title="About Performance Scorecards" variant={subcontractsSeverity}>
            The Project Manager records a performance scorecard within 14 days of practical
            completion — a score below 6.0 automatically flags the subcontractor on the ASR
            watch list. Click a row to see the full record and edit it.
          </HelpPanel>
        )}
        {tab === 'retentions' && (
          <HelpPanel title="About Payment Certification & Retentions" variant={subcontractsSeverity}>
            Tracks each certified invoice against an award, the 10% retention held, and WHT
            deducted. Click a row to see the full record and edit the amounts; "Mark Paid"
            records the actual payment date and isn't editable here.
          </HelpPanel>
        )}

        <div style={{ ...KPI_GRID, marginBottom: 18 }}>
          <Kpi label="Subcontractors" value={subTotalCount} icon="🏗️" />
          <Kpi label="Pending Prequalification" value={pendingPqqCount} icon="📋" variant={pendingPqqCount > 0 ? 'amber' : 'green'} />
          <Kpi label="Mobilized" value={mobilizedCount} icon="🚧" variant="green" />
          <Kpi label="Restricted (Insurance/TCC)" value={restrictedCount} icon="⛔" variant={restrictedCount > 0 ? 'red' : 'green'} />
          <Kpi label="Watch-Listed" value={watchListedCount} icon="⚠️" variant={watchListedCount > 0 ? 'amber' : 'green'} />
        </div>

        <Tabs tabs={TABS} active={tab} setActive={setTab} />

        {loading ? <Loading /> : (
          <>
            {tab === 'subcontractors' && (
              <>
                <SectionHeader title="Approved Subcontractor Register" sub="Searchable by trade category — the single source of truth HSE and Operations reference"
                  action={canWrite && <Btn onClick={() => setModal('sub')}>+ Add Subcontractor</Btn>} />
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable
                    headers={['Name', 'Trade Category', 'Safety Score', 'Performance', 'Prequalified', 'Status', 'Actions']}
                    empty="No subcontractors added yet."
                    rows={subcontractors.map(s => [
                      <span onClick={() => openSubView(s)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{s.name}</span>,
                      s.tradeCategory || '—',
                      <Badge variant={s.safetyScore >= 80 ? 'green' : s.safetyScore >= 50 ? 'amber' : 'red'}>{s.safetyScore}</Badge>,
                      s.latestPerformanceScore != null ? <Badge variant={s.watchListed ? 'red' : 'green'}>{s.latestPerformanceScore}</Badge> : '—',
                      s.prequalified ? <Badge variant="green">Prequalified</Badge> : <Badge variant="amber">Pending</Badge>,
                      s.isRestricted ? <Badge variant="red">Restricted</Badge> : <Badge variant="green">Active</Badge>,
                      <div style={{ display: 'flex', gap: 4 }}>
                        <Btn variant="ghost" size="sm" onClick={() => openSubView(s)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                        {canWrite && <Btn variant="ghost" size="sm" onClick={() => { openSubView(s); setEditingSub(true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>}
                        {canDelete && <Btn variant="danger" size="sm" onClick={() => setDeleteTarget({ type: 'subcontractor', row: s })} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>}
                      </div>,
                    ])}
                  />
                  <Pagination page={subPage} pageSize={PAGE_SIZE} totalCount={subTotalCount} onPageChange={setSubPage} />
                </Card>
              </>
            )}

            {tab === 'prequalifications' && (
              <>
                <SectionHeader title="Prequalification Questionnaires (PQQ)" sub="Approval by Head of Projects automatically updates the ASR"
                  action={canWrite && <Btn onClick={() => setModal('pqq')}>+ New PQQ</Btn>} />
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable
                    headers={['Subcontractor', 'Score', 'Status', 'Submitted', 'Actions']}
                    empty="No prequalifications recorded yet."
                    rows={prequalifications.map(p => [
                      <span onClick={() => openPqqView(p)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{subcontractorName(p.subcontractorId)}</span>,
                      p.score ?? '—',
                      <Badge variant={pqqVariant(p.status)}>{PQQ_STATUS_LABELS[p.status] ?? p.status}</Badge>,
                      p.submittedOn ? fmt.date(p.submittedOn) : '—',
                      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                        <Btn variant="ghost" size="sm" onClick={() => openPqqView(p)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                        {canWrite && <Btn variant="ghost" size="sm" onClick={() => { openPqqView(p); setEditingPqq(true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>}
                        {canDelete && <Btn variant="danger" size="sm" onClick={() => setDeleteTarget({ type: 'pqq', row: p })} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>}
                        {canApprove && (p.status === 'Sent' || p.status === 'Completed' || p.status === 'PendingApproval') && (
                          <>
                            <Btn size="sm" onClick={() => openPqqApprove(p)}>Approve</Btn>
                            <Btn size="sm" variant="ghost" onClick={() => rejectPqq(p.id)}>Reject</Btn>
                          </>
                        )}
                      </div>,
                    ])}
                  />
                  <Pagination page={pqqPage} pageSize={PAGE_SIZE} totalCount={pqqTotalCount} onPageChange={setPqqPage} />
                </Card>
              </>
            )}

            {tab === 'awards' && (
              <>
                <SectionHeader title="Subcontract Awards & Mobilization" sub="Mobilization is hard-gated on an Approved RAMS record confirmed by HSE"
                  action={canWrite && <Btn onClick={() => setModal('award')}>+ New Award</Btn>} />
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable
                    headers={['Subcontractor', 'Project', 'Value', 'Status', 'RAMS', 'Actions']}
                    empty="No awards created yet."
                    rows={awards.map(a => [
                      <span onClick={() => openAwardView(a)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{a.subcontractorName ?? subcontractorName(a.subcontractorId)}</span>,
                      a.projectName || '—',
                      fmt.kes(a.value),
                      <Badge variant={awardVariant(a.status)}>{AWARD_STATUS_LABELS[a.status] ?? a.status}</Badge>,
                      a.ramsApproved ? <Badge variant="green">Approved</Badge> : <Badge variant="amber">Not Confirmed</Badge>,
                      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                        <Btn variant="ghost" size="sm" onClick={() => openAwardView(a)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                        {canWrite && <Btn variant="ghost" size="sm" onClick={() => { openAwardView(a); setEditingAward(true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>}
                        {canDelete && <Btn variant="danger" size="sm" onClick={() => setDeleteTarget({ type: 'award', row: a })} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>}
                        {canApprove && a.status === 'PendingApproval' && <Btn size="sm" onClick={() => setAwardStatus(a.id, 'Approved')}>Approve</Btn>}
                        {canApprove && a.status === 'Approved' && <Btn size="sm" onClick={() => activateMobilization(a.id)}>Activate Mobilization</Btn>}
                        {canApprove && a.status === 'Mobilized' && <Btn size="sm" onClick={() => setAwardStatus(a.id, 'Completed')}>Mark Completed</Btn>}
                        {canApprove && (a.status === 'PendingApproval' || a.status === 'Approved') && <Btn size="sm" variant="ghost" onClick={() => setAwardStatus(a.id, 'Terminated')}>Terminate</Btn>}
                      </div>,
                    ])}
                  />
                  <Pagination page={awardPage} pageSize={PAGE_SIZE} totalCount={awardTotalCount} onPageChange={setAwardPage} />
                </Card>
              </>
            )}

            {tab === 'scorecards' && (
              <>
                <SectionHeader title="Performance Scorecards" sub="Recorded by the Project Manager within 14 days of practical completion — below 6.0 flags the subcontractor for the watch list"
                  action={canWrite && <Btn onClick={() => setModal('scorecard')}>+ Record Scorecard</Btn>} />
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable
                    headers={['Award', 'Project Manager', 'Score', 'Completed', 'Notes', 'Actions']}
                    empty="No scorecards recorded yet."
                    rows={scorecards.map(s => [
                      <span onClick={() => openScorecardView(s)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{awardLabel(s.awardId)}</span>,
                      s.projectManagerName || '—',
                      <Badge variant={s.score >= 6 ? 'green' : 'red'}>{s.score}</Badge>,
                      fmt.date(s.completedOn),
                      s.notes || '—',
                      <div style={{ display: 'flex', gap: 4 }}>
                        <Btn variant="ghost" size="sm" onClick={() => openScorecardView(s)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                        {canWrite && <Btn variant="ghost" size="sm" onClick={() => { openScorecardView(s); setEditingScorecard(true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>}
                        {canDelete && <Btn variant="danger" size="sm" onClick={() => setDeleteTarget({ type: 'scorecard', row: s })} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>}
                      </div>,
                    ])}
                  />
                  <Pagination page={scorecardPage} pageSize={PAGE_SIZE} totalCount={scorecardTotalCount} onPageChange={setScorecardPage} />
                </Card>
              </>
            )}

            {tab === 'retentions' && (
              <>
                <SectionHeader title="Payment Certification & Retentions" sub="Certified invoices, 10% retention balances and WHT deductions per award"
                  action={canWrite && <Btn onClick={() => setModal('retention')}>+ Certify Payment</Btn>} />
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable
                    headers={['Award', 'Certified Amount', 'Retention Held', 'WHT', 'Status', 'Actions']}
                    empty="No payments certified yet."
                    rows={retentions.map(r => [
                      <span onClick={() => openRetentionView(r)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{awardLabel(r.awardId)}</span>,
                      fmt.kes(r.certifiedAmount),
                      fmt.kes(r.retentionHeld),
                      fmt.kes(r.wht),
                      <Badge variant={r.status === 'Paid' ? 'green' : 'amber'}>{r.status}</Badge>,
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
                        <Btn variant="ghost" size="sm" onClick={() => openRetentionView(r)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                        {canWrite && <Btn variant="ghost" size="sm" onClick={() => { openRetentionView(r); setEditingRetention(true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>}
                        {canDelete && <Btn variant="danger" size="sm" onClick={() => setDeleteTarget({ type: 'retention', row: r })} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>}
                        {canWrite && r.status === 'Certified' && <Btn size="sm" onClick={() => markPaid(r.id)}>Mark Paid</Btn>}
                      </div>,
                    ])}
                  />
                  <Pagination page={retentionPage} pageSize={PAGE_SIZE} totalCount={retentionTotalCount} onPageChange={setRetentionPage} />
                </Card>
              </>
            )}
          </>
        )}

        {/* ── Add Subcontractor modal ── */}
        {modal === 'sub' && (
          <Modal title="Add Subcontractor" onClose={() => setModal(null)}>
            <Input label="Name" value={subForm.name} onChange={v => setSubForm({ ...subForm, name: v })} required />
            <Input label="Trade Category" value={subForm.tradeCategory} onChange={v => setSubForm({ ...subForm, tradeCategory: v })} required placeholder="e.g. Electrical, Plumbing, Civil Works" />
            <Input label="Insurance Expiry" type="date" value={subForm.insuranceExpiry} onChange={v => setSubForm({ ...subForm, insuranceExpiry: v })} />
            <Input label="Tax Compliance Cert Expiry" type="date" value={subForm.tccExpiry} onChange={v => setSubForm({ ...subForm, tccExpiry: v })} />
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 10 }}>
              <input type="checkbox" checked={subForm.hasDeclaredRelationship} onChange={e => setSubForm({ ...subForm, hasDeclaredRelationship: e.target.checked })} />
              Has a declared Director/Shareholder/employee relationship
            </label>
            {subForm.hasDeclaredRelationship && <Input label="Relationship Details" value={subForm.relationshipDetails} onChange={v => setSubForm({ ...subForm, relationshipDetails: v })} />}
            <Input label="Notes" value={subForm.notes} onChange={v => setSubForm({ ...subForm, notes: v })} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitSubcontractor} disabled={saving || !subForm.name || !subForm.tradeCategory}>{saving ? 'Saving…' : 'Add Subcontractor'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── New PQQ modal ── */}
        {modal === 'pqq' && (
          <Modal title="New Prequalification" onClose={() => setModal(null)}>
            <Select label="Subcontractor" value={pqqForm.subcontractorId} onChange={v => setPqqForm({ ...pqqForm, subcontractorId: v })}
              options={[{ value: '', label: '— Select —' }, ...allSubcontractors.map(s => ({ value: s.id, label: s.name }))]} required />
            <FileInput label="PQQ Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
              uploading={uploading === 'prequalifications'} fileUrl={pqqForm.documentUrl}
              onFileSelected={async file => { const url = await uploadSubcontractsFile('prequalifications', file); if (url) setPqqForm(f => ({ ...f, documentUrl: url })) }} />
            <Input label="Submitted On" type="date" value={pqqForm.submittedOn} onChange={v => setPqqForm({ ...pqqForm, submittedOn: v })} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitPqq} disabled={saving || !pqqForm.subcontractorId}>{saving ? 'Saving…' : 'Save'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── Approve PQQ modal ── */}
        {modal === 'pqqApprove' && (
          <Modal title="Approve Prequalification" onClose={() => setModal(null)}>
            <Input label="Score" type="number" value={pqqApproveForm.score} onChange={v => setPqqApproveForm({ ...pqqApproveForm, score: v })} required />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitPqqApprove} disabled={saving}>{saving ? 'Saving…' : 'Approve'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── New Award modal ── */}
        {modal === 'award' && (
          <Modal title="New Subcontract Award" onClose={() => setModal(null)}>
            <Select label="Subcontractor" value={awardForm.subcontractorId} onChange={v => setAwardForm({ ...awardForm, subcontractorId: v })}
              options={[{ value: '', label: '— Select —' }, ...allSubcontractors.map(s => ({ value: s.id, label: s.name }))]} required />
            <Select label="Project" value={awardForm.projectId} onChange={v => setAwardForm({ ...awardForm, projectId: v })}
              options={[{ value: '', label: '— No project linked —' }, ...projects.map(p => ({ value: p.id, label: p.name }))]} />
            <Input label="Value" type="number" value={awardForm.value} onChange={v => setAwardForm({ ...awardForm, value: v })} required />
            <FileInput label="Signed Agreement" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
              uploading={uploading === 'awards'} fileUrl={awardForm.signedAgreementUrl}
              onFileSelected={async file => { const url = await uploadSubcontractsFile('awards', file); if (url) setAwardForm(f => ({ ...f, signedAgreementUrl: url })) }} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitAward} disabled={saving || !awardForm.subcontractorId || !awardForm.value}>{saving ? 'Saving…' : 'Create Award'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── Record Scorecard modal ── */}
        {modal === 'scorecard' && (
          <Modal title="Record Performance Scorecard" onClose={() => setModal(null)}>
            <Select label="Award" value={scorecardForm.awardId} onChange={v => setScorecardForm({ ...scorecardForm, awardId: v })}
              options={[{ value: '', label: '— Select —' }, ...allAwards.map(a => ({ value: a.id, label: awardLabel(a.id) }))]} required />
            <Input label="Project Manager Name" value={scorecardForm.projectManagerName} onChange={v => setScorecardForm({ ...scorecardForm, projectManagerName: v })} />
            <Input label="Score (0–10)" type="number" value={scorecardForm.score} onChange={v => setScorecardForm({ ...scorecardForm, score: v })} required />
            <Input label="Completed On" type="date" value={scorecardForm.completedOn} onChange={v => setScorecardForm({ ...scorecardForm, completedOn: v })} />
            <Input label="Notes" value={scorecardForm.notes} onChange={v => setScorecardForm({ ...scorecardForm, notes: v })} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitScorecard} disabled={saving || !scorecardForm.awardId || !scorecardForm.score}>{saving ? 'Saving…' : 'Save'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── Certify Payment modal ── */}
        {modal === 'retention' && (
          <Modal title="Certify Payment" onClose={() => setModal(null)}>
            <Select label="Award" value={retentionForm.awardId} onChange={v => setRetentionForm({ ...retentionForm, awardId: v })}
              options={[{ value: '', label: '— Select —' }, ...allAwards.map(a => ({ value: a.id, label: awardLabel(a.id) }))]} required />
            <Input label="Certified Amount" type="number" value={retentionForm.certifiedAmount} onChange={v => setRetentionForm({ ...retentionForm, certifiedAmount: v })} required />
            <Input label="Retention Held (10%)" type="number" value={retentionForm.retentionHeld} onChange={v => setRetentionForm({ ...retentionForm, retentionHeld: v })} />
            <Input label="WHT" type="number" value={retentionForm.wht} onChange={v => setRetentionForm({ ...retentionForm, wht: v })} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={submitRetention} disabled={saving || !retentionForm.awardId || !retentionForm.certifiedAmount}>{saving ? 'Saving…' : 'Certify'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── View / Edit Subcontractor modal ── */}
        {viewingSub && (
          <Modal title={editingSub ? 'Edit Subcontractor' : 'Subcontractor Details'} onClose={() => { setViewingSub(null); setEditingSub(false) }} width={620}>
            {!editingSub ? (
              <>
                <div style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewingSub.name}</span>
                    {viewingSub.isRestricted ? <Badge variant="red">Restricted</Badge> : <Badge variant="green">Active</Badge>}
                  </div>
                  <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingSub.tradeCategory || '—'}</div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                  <MiniStat label="Safety Score" value={<Badge variant={viewingSub.safetyScore >= 80 ? 'green' : viewingSub.safetyScore >= 50 ? 'amber' : 'red'}>{viewingSub.safetyScore}</Badge>} />
                  <MiniStat label="Performance" value={viewingSub.latestPerformanceScore != null ? <Badge variant={viewingSub.watchListed ? 'red' : 'green'}>{viewingSub.latestPerformanceScore}</Badge> : '—'} />
                  <MiniStat label="Insurance Expiry" value={viewingSub.insuranceExpiry ? fmt.date(viewingSub.insuranceExpiry) : '—'} />
                  <MiniStat label="TCC Expiry" value={viewingSub.tccExpiry ? fmt.date(viewingSub.tccExpiry) : '—'} />
                </div>

                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Declared Relationship</div>
                  <div style={{ fontSize: 13, color: T.dgrey }}>{viewingSub.hasDeclaredRelationship ? (viewingSub.relationshipDetails || 'Yes') : 'None declared'}</div>
                </div>

                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Notes</div>
                  <div style={{ fontSize: 13, color: T.dgrey }}>{viewingSub.notes || 'No notes.'}</div>
                </div>

                <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>
                  {viewingSub.prequalified ? 'Prequalified' : 'Prequalification pending'} — set via the Prequalifications approval workflow, not here.
                </p>

                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                  <Btn variant="ghost" onClick={() => { setViewingSub(null); setEditingSub(false) }}>Close</Btn>
                  {canWrite && <Btn variant="gold" onClick={() => setEditingSub(true)}>Edit</Btn>}
                </div>
              </>
            ) : (
              <>
                <Input label="Name" value={subEditForm.name} onChange={v => setSubEditForm({ ...subEditForm, name: v })} required />
                <Input label="Trade Category" value={subEditForm.tradeCategory} onChange={v => setSubEditForm({ ...subEditForm, tradeCategory: v })} required />
                <Input label="Insurance Expiry" type="date" value={subEditForm.insuranceExpiry} onChange={v => setSubEditForm({ ...subEditForm, insuranceExpiry: v })} />
                <Input label="Tax Compliance Cert Expiry" type="date" value={subEditForm.tccExpiry} onChange={v => setSubEditForm({ ...subEditForm, tccExpiry: v })} />
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 10 }}>
                  <input type="checkbox" checked={subEditForm.hasDeclaredRelationship} onChange={e => setSubEditForm({ ...subEditForm, hasDeclaredRelationship: e.target.checked })} />
                  Has a declared Director/Shareholder/employee relationship
                </label>
                {subEditForm.hasDeclaredRelationship && <Input label="Relationship Details" value={subEditForm.relationshipDetails} onChange={v => setSubEditForm({ ...subEditForm, relationshipDetails: v })} />}
                <Input label="Notes" value={subEditForm.notes} onChange={v => setSubEditForm({ ...subEditForm, notes: v })} />
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                  <Btn variant="ghost" onClick={() => setEditingSub(false)}>Cancel</Btn>
                  <Btn onClick={submitSubEdit} disabled={saving || !subEditForm.name || !subEditForm.tradeCategory}>{saving ? 'Saving…' : 'Save'}</Btn>
                </div>
              </>
            )}
          </Modal>
        )}

        {/* ── View / Edit Prequalification modal ── */}
        {viewingPqq && (
          <Modal title={editingPqq ? 'Edit Prequalification' : 'Prequalification Details'} onClose={() => { setViewingPqq(null); setEditingPqq(false) }} width={620}>
            {!editingPqq ? (
              <>
                <div style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{subcontractorName(viewingPqq.subcontractorId)}</span>
                    <Badge variant={pqqVariant(viewingPqq.status)}>{PQQ_STATUS_LABELS[viewingPqq.status] ?? viewingPqq.status}</Badge>
                  </div>
                  <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingPqq.submittedOn ? `Submitted ${fmt.date(viewingPqq.submittedOn)}` : 'Not yet submitted'}</div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                  <MiniStat label="Score" value={viewingPqq.score ?? '—'} />
                  <MiniStat label="Approved By" value={viewingPqq.approvedByName ?? '—'} />
                  <MiniStat label="Approved On" value={viewingPqq.approvedOn ? fmt.date(viewingPqq.approvedOn) : '—'} />
                </div>

                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>PQQ Document</div>
                  {viewingPqq.documentUrl
                    ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewingPqq.documentUrl)}>👁 View document</Btn>
                    : <span style={{ fontSize: 13, color: T.mgrey }}>No document uploaded.</span>}
                </div>

                <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Approve/Reject, not here.</p>

                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                  <Btn variant="ghost" onClick={() => { setViewingPqq(null); setEditingPqq(false) }}>Close</Btn>
                  {canWrite && <Btn variant="gold" onClick={() => setEditingPqq(true)}>Edit</Btn>}
                </div>
              </>
            ) : (
              <>
                <FileInput label="PQQ Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
                  uploading={uploading === 'prequalifications'} fileUrl={pqqEditForm.documentUrl}
                  onFileSelected={async file => { const url = await uploadSubcontractsFile('prequalifications', file); if (url) setPqqEditForm(f => ({ ...f, documentUrl: url })) }} />
                <Input label="Submitted On" type="date" value={pqqEditForm.submittedOn} onChange={v => setPqqEditForm({ ...pqqEditForm, submittedOn: v })} />
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                  <Btn variant="ghost" onClick={() => setEditingPqq(false)}>Cancel</Btn>
                  <Btn onClick={submitPqqEdit} disabled={saving || !!uploading}>{saving ? 'Saving…' : 'Save'}</Btn>
                </div>
              </>
            )}
          </Modal>
        )}

        {/* ── View / Edit Award modal ── */}
        {viewingAward && (
          <Modal title={editingAward ? 'Edit Award' : 'Award Details'} onClose={() => { setViewingAward(null); setEditingAward(false) }} width={620}>
            {!editingAward ? (
              <>
                <div style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewingAward.subcontractorName ?? subcontractorName(viewingAward.subcontractorId)}</span>
                    <Badge variant={awardVariant(viewingAward.status)}>{AWARD_STATUS_LABELS[viewingAward.status] ?? viewingAward.status}</Badge>
                  </div>
                  <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingAward.projectName || 'No project linked'}</div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                  <MiniStat label="Value" value={fmt.kes(viewingAward.value)} />
                  <MiniStat label="RAMS" value={viewingAward.ramsApproved ? <Badge variant="green">Approved</Badge> : <Badge variant="amber">Not Confirmed</Badge>} />
                  <MiniStat label="Mobilization Activated" value={viewingAward.mobilizationActivatedAt ? fmt.date(viewingAward.mobilizationActivatedAt) : '—'} />
                </div>

                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Signed Agreement</div>
                  {viewingAward.signedAgreementUrl
                    ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewingAward.signedAgreementUrl)}>👁 View document</Btn>
                    : <span style={{ fontSize: 13, color: T.mgrey }}>No document uploaded.</span>}
                </div>

                <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via the action buttons, not here.</p>

                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                  <Btn variant="ghost" onClick={() => { setViewingAward(null); setEditingAward(false) }}>Close</Btn>
                  {canWrite && <Btn variant="gold" onClick={() => setEditingAward(true)}>Edit</Btn>}
                </div>
              </>
            ) : (
              <>
                <Select label="Subcontractor" value={awardEditForm.subcontractorId} onChange={v => setAwardEditForm({ ...awardEditForm, subcontractorId: v })}
                  options={[{ value: '', label: '— Select —' }, ...allSubcontractors.map(s => ({ value: s.id, label: s.name }))]} required />
                <Select label="Project" value={awardEditForm.projectId} onChange={v => setAwardEditForm({ ...awardEditForm, projectId: v })}
                  options={[{ value: '', label: '— No project linked —' }, ...projects.map(p => ({ value: p.id, label: p.name }))]} />
                <Input label="Value" type="number" value={awardEditForm.value} onChange={v => setAwardEditForm({ ...awardEditForm, value: v })} required />
                <FileInput label="Signed Agreement" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
                  uploading={uploading === 'awards'} fileUrl={awardEditForm.signedAgreementUrl}
                  onFileSelected={async file => { const url = await uploadSubcontractsFile('awards', file); if (url) setAwardEditForm(f => ({ ...f, signedAgreementUrl: url })) }} />
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                  <Btn variant="ghost" onClick={() => setEditingAward(false)}>Cancel</Btn>
                  <Btn onClick={submitAwardEdit} disabled={saving || !!uploading || !awardEditForm.subcontractorId || !awardEditForm.value}>{saving ? 'Saving…' : 'Save'}</Btn>
                </div>
              </>
            )}
          </Modal>
        )}

        {/* ── View / Edit Scorecard modal ── */}
        {viewingScorecard && (
          <Modal title={editingScorecard ? 'Edit Scorecard' : 'Scorecard Details'} onClose={() => { setViewingScorecard(null); setEditingScorecard(false) }} width={620}>
            {!editingScorecard ? (
              <>
                <div style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{awardLabel(viewingScorecard.awardId)}</span>
                    <Badge variant={viewingScorecard.score >= 6 ? 'green' : 'red'}>{viewingScorecard.score}</Badge>
                  </div>
                  <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingScorecard.projectManagerName || 'No project manager on file'}</div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                  <MiniStat label="Completed On" value={fmt.date(viewingScorecard.completedOn)} />
                </div>

                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Notes</div>
                  <div style={{ fontSize: 13, color: T.dgrey }}>{viewingScorecard.notes || 'No notes.'}</div>
                </div>

                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                  <Btn variant="ghost" onClick={() => { setViewingScorecard(null); setEditingScorecard(false) }}>Close</Btn>
                  {canWrite && <Btn variant="gold" onClick={() => setEditingScorecard(true)}>Edit</Btn>}
                </div>
              </>
            ) : (
              <>
                <Input label="Project Manager Name" value={scorecardEditForm.projectManagerName} onChange={v => setScorecardEditForm({ ...scorecardEditForm, projectManagerName: v })} />
                <Input label="Score (0–10)" type="number" value={scorecardEditForm.score} onChange={v => setScorecardEditForm({ ...scorecardEditForm, score: v })} required />
                <Input label="Completed On" type="date" value={scorecardEditForm.completedOn} onChange={v => setScorecardEditForm({ ...scorecardEditForm, completedOn: v })} />
                <Input label="Notes" value={scorecardEditForm.notes} onChange={v => setScorecardEditForm({ ...scorecardEditForm, notes: v })} />
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                  <Btn variant="ghost" onClick={() => setEditingScorecard(false)}>Cancel</Btn>
                  <Btn onClick={submitScorecardEdit} disabled={saving || !scorecardEditForm.score}>{saving ? 'Saving…' : 'Save'}</Btn>
                </div>
              </>
            )}
          </Modal>
        )}

        {/* ── View / Edit Payment Retention modal ── */}
        {viewingRetention && (
          <Modal title={editingRetention ? 'Edit Payment Record' : 'Payment Record Details'} onClose={() => { setViewingRetention(null); setEditingRetention(false) }} width={620}>
            {!editingRetention ? (
              <>
                <div style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{awardLabel(viewingRetention.awardId)}</span>
                    <Badge variant={viewingRetention.status === 'Paid' ? 'green' : 'amber'}>{viewingRetention.status}</Badge>
                  </div>
                  <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingRetention.paidOn ? `Paid ${fmt.date(viewingRetention.paidOn)}` : 'Not yet paid'}</div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                  <MiniStat label="Certified Amount" value={fmt.kes(viewingRetention.certifiedAmount)} />
                  <MiniStat label="Retention Held" value={fmt.kes(viewingRetention.retentionHeld)} />
                  <MiniStat label="WHT" value={fmt.kes(viewingRetention.wht)} />
                </div>

                <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Mark Paid, not here.</p>

                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                  <Btn variant="ghost" onClick={() => { setViewingRetention(null); setEditingRetention(false) }}>Close</Btn>
                  {canWrite && <Btn variant="gold" onClick={() => setEditingRetention(true)}>Edit</Btn>}
                </div>
              </>
            ) : (
              <>
                <Input label="Certified Amount" type="number" value={retentionEditForm.certifiedAmount} onChange={v => setRetentionEditForm({ ...retentionEditForm, certifiedAmount: v })} required />
                <Input label="Retention Held" type="number" value={retentionEditForm.retentionHeld} onChange={v => setRetentionEditForm({ ...retentionEditForm, retentionHeld: v })} />
                <Input label="WHT" type="number" value={retentionEditForm.wht} onChange={v => setRetentionEditForm({ ...retentionEditForm, wht: v })} />
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                  <Btn variant="ghost" onClick={() => setEditingRetention(false)}>Cancel</Btn>
                  <Btn onClick={submitRetentionEdit} disabled={saving || !retentionEditForm.certifiedAmount}>{saving ? 'Saving…' : 'Save'}</Btn>
                </div>
              </>
            )}
          </Modal>
        )}

        {/* ── Delete confirmation modal ── */}
        {deleteTarget && (
          <Modal title={DELETE_TITLES[deleteTarget.type]} onClose={() => setDeleteTarget(null)} width={380}>
            <p style={{ fontSize: 13, color: T.dgrey }}>
              Are you sure you want to delete <strong>"{deleteTargetLabel()}"</strong>? This action cannot be undone.
            </p>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Btn>
              <Btn variant="danger" onClick={confirmDelete} disabled={saving}>{saving ? 'Deleting…' : 'Yes, Delete'}</Btn>
            </div>
          </Modal>
        )}

        {/* ── Document preview modal ── */}
        {previewUrl && (
          <Modal title="Document" onClose={() => setPreviewUrl(null)} width={800}>
            <DocumentPreview url={previewUrl} />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
              <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
              <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
            </div>
          </Modal>
        )}
      </div>
    </>
  )
}
