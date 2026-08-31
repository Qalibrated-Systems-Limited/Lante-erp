import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import {
  exportOverviewPDF, exportOverviewExcel,
  exportCheckInsPDF, exportCheckInsExcel,
  exportSummariesPDF, exportSummariesExcel,
  exportServiceReportPDF, exportServiceReportExcel,
  exportFinancialsPDF, exportFinancialsExcel,
} from '../../utils/assignmentExports.js'
import { RequisitionTypeChooser, default as RequisitionFormModal } from './RequisitionFormModal.jsx'
import TravelVoucherModal from './TravelVoucherModal.jsx'
import { exportTravelVoucherPDF } from '../../utils/assignmentExports.js'
import SignatureModal from '../../components/SignatureModal.jsx'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

function currentUserName(user) {
  return [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.email || 'Unknown user'
}

function getLocation(timeout = 8000) {
  return new Promise((resolve) => {
    if (!navigator.geolocation) { resolve(null); return }
    navigator.geolocation.getCurrentPosition(
      pos => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
      ()  => resolve(null),
      { enableHighAccuracy: true, timeout, maximumAge: 0 }
    )
  })
}

async function uploadAttachments(files, entityType, entityId, assignmentId) {
  for (const file of files) {
    const fd = new FormData()
    fd.append('file', file)
    fd.append('entityType', entityType)
    fd.append('entityId', entityId)
    if (assignmentId) fd.append('assignmentId', assignmentId)
    await api.post('/api/v1/attachments', fd).catch(() => {})
  }
}

const STATUS_BADGE = {
  Pending: 'bg-amber-100 text-amber-700',
  Accepted: 'bg-green-100 text-green-700',
  InProgress: 'bg-blue-100 text-blue-700',
  Completed: 'bg-indigo-100 text-indigo-700',
  Cancelled: 'bg-red-100 text-red-600',
  Declined: 'bg-gray-100 text-gray-500',
  AwaitingProjectLink: 'bg-purple-100 text-purple-700',
  Archived: 'bg-gray-100 text-gray-500',
}

const PRIORITY_TEXT = {
  Low: 'text-green-600',
  Normal: 'text-amber-600',
  High: 'text-orange-500',
  Urgent: 'text-red-500',
}

const FIN_STATUS_BADGE = {
  Pending: 'bg-amber-100 text-amber-700',
  TmApproved: 'bg-blue-100 text-blue-700',
  CfoApproved: 'bg-green-100 text-green-700',
  TmRejected: 'bg-red-100 text-red-600',
  CfoRejected: 'bg-red-100 text-red-600',
  CfoProcessing: 'bg-purple-100 text-purple-700',
  Paid: 'bg-emerald-100 text-emerald-700',
  ManagerApproved: 'bg-blue-100 text-blue-700',
  ManagerRejected: 'bg-red-100 text-red-600',
  Disbursed: 'bg-emerald-100 text-emerald-700',
  Approved: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-600',
  CfoReceived: 'bg-purple-100 text-purple-700',
  Draft: 'bg-gray-100 text-gray-500',
  Submitted: 'bg-blue-100 text-blue-700',
  UnderReview: 'bg-orange-100 text-orange-600',
}

const BASE_TABS = [
  { id: 'overview', label: 'Overview' },
  { id: 'checkins', label: 'Check-ins' },
  { id: 'summaries', label: 'Daily Summaries' },
  { id: 'service-report', label: 'Service Report' },
  { id: 'financials', label: 'Financials' },
  { id: 'photos', label: 'Photos' },
  { id: 'vehicles', label: 'Vehicles' },
]
const LAB_TAB           = { id: 'lab-work-order',  label: 'Lab Work Order' }
const PREDEPLOYMENT_TAB = { id: 'pre-deployment', label: 'Pre-Deployment' }
const DEVIATIONS_TAB    = { id: 'deviations',     label: 'Deviations' }
const FEEDBACK_TAB      = { id: 'feedback',        label: 'Feedback' }

const FIN_SUBTABS = ['requisitions', 'travel-voucher', 'claims', 'petty-cash', 'per-diem', 'advance-returns', 'refunds']

export default function AssignmentDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { hasPermission, user } = useAuth()
  const branding = useCompanyBranding()
  const canApprove = hasPermission('operations.approve')
  // Approving/rejecting a vehicle DISPATCH is a fleet.write action on fleet-service's backend,
  // not an operations.approve one — operations.approve is for this same page's financial claim
  // approvals below. Gating the dispatch buttons on canApprove let a Tech/Construction Manager
  // see and click Approve/Reject only to get a silent 403, since none of those roles hold any
  // fleet.* permission. See FleetDispatchRequestsPage.jsx for the fleet-manager-facing queue.
  const canApproveDispatch = hasPermission('fleet.write')
  const canWrite = hasPermission('operations.write')
  const canAct = canWrite || hasPermission('operations.read.own')
  const canExport = hasPermission('reports.export')

  const [assignment, setAssignment] = useState(null)
  const [checkIns, setCheckIns] = useState([])
  const [photos, setPhotos] = useState([])
  const [attachments, setAttachments] = useState([])
  const [summaries, setSummaries] = useState([])
  const [serviceReport, setServiceReport] = useState(null)
  const [financials, setFinancials] = useState({ requisitions: [], travelVouchers: [], claims: [], pettyCash: [], perDiem: [], advanceReturns: [], refunds: [] })
  const [showTravelVoucher, setShowTravelVoucher] = useState(false)
  const [tab, setTab] = useState('overview')
  const [finTab, setFinTab] = useState('requisitions')
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState(false)
  const [photoUploading, setPhotoUploading] = useState(false)
  const [signModalOpen, setSignModalOpen] = useState(false)
  const [signLoading, setSignLoading] = useState(false)
  const [lightboxPhoto, setLightboxPhoto] = useState(null)

  const [modal, setModal] = useState(null)
  const [pendingFiles, setPendingFiles] = useState([])

  const [showLinkModal, setShowLinkModal] = useState(false)
  const [projects, setProjects] = useState([])
  const [linkForm, setLinkForm] = useState({ projectId: '', milestoneId: '' })
  const [milestones, setMilestones] = useState([])
  const [archiveReason, setArchiveReason] = useState('')
  const [showArchiveModal, setShowArchiveModal] = useState(false)
  const [linkLoading, setLinkLoading] = useState(false)

  const closeModal = () => { setModal(null); setPendingFiles([]); setGeoStatus('idle') }

  const [checkInForm, setCheckInForm] = useState({ notes: '', latitude: '', longitude: '' })
  const [geoStatus, setGeoStatus] = useState('idle') // 'idle' | 'loading' | 'ok' | 'error'
  const todayStr = () => new Date().toISOString().slice(0, 10)
  const [summaryForm, setSummaryForm] = useState({ date: todayStr(), summary: '', hoursWorked: '', challenges: '', nextDayPlan: '', expensesIncurred: '' })
  const [cancelReason, setCancelReason] = useState('')
  const [reqForm, setReqForm] = useState({ type: 'CashAdvance', description: '', amount: '', justification: '' })
  const [claimForm, setClaimForm] = useState({
    pcvNo: '', date: new Date().toISOString().split('T')[0],
    payeeName: '', accountName: '', beingAnAdvanceFor: '',
    particulars: [{ description: '', kshs: '', cts: '' }],
    amountInWords: '', amount: '', totalCts: '',
    preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
  })
  const [pcForm, setPcForm] = useState({
    pcvNo: '', date: new Date().toISOString().split('T')[0],
    payeeName: '', projectAccount: '', beingAnAdvanceFor: '',
    sum: '', amountInWords: '',
    preparedBy: '', authorisedBy: '', checkedBy: '', receivedBy: '', receivedDate: '',
    approvedBy: '',
  })
  const [pdForm, setPdForm] = useState({
    refNo: '', date: new Date().toISOString().split('T')[0],
    employeeName: '', periodFrom: '', periodTo: '',
    projectAccount: '', daysCount: '', perDiemRate: '',
    totalAdvanced: '', totalSpent: '', notes: '',
    preparedBy: '', checkedBy: '', approvedBy: '', signedBy: '',
  })
  const [arForm, setArForm] = useState({
    refNo: '', date: new Date().toISOString().split('T')[0],
    returnedBy: '', projectAccount: '',
    totalAdvanced: '', amountReturned: '', amountInWords: '', notes: '',
    preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
  })
  const [refundForm, setRefundForm] = useState({
    refNo: '', date: new Date().toISOString().split('T')[0],
    payeeName: '', projectAccount: '',
    reason: '', amount: '', amountInWords: '',
    preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
  })
  const [reviewItem, setReviewItem] = useState(null)
  const [reviewComment, setReviewComment] = useState('')
  const [reqFormType, setReqFormType] = useState(null)

  // Pre-deployment checklist
  const [preDeployment, setPreDeployment] = useState(null)
  const [preDeployLoading, setPreDeployLoading] = useState(false)
  const [preDeployActionLoading, setPreDeployActionLoading] = useState(false)

  // Deviations (NCRs)
  const [ncrs, setNcrs] = useState([])
  const [ncrsLoading, setNcrsLoading] = useState(false)
  const [ncrActionLoading, setNcrActionLoading] = useState(false)

  // Feedback (risks + customer satisfaction)
  const [risks, setRisks] = useState([])
  const [customerFeedback, setCustomerFeedback] = useState(null)
  const [feedbackLoading, setFeedbackLoading] = useState(false)
  const [feedbackActionLoading, setFeedbackActionLoading] = useState(false)

  // Lab work order
  const [labWorkOrder, setLabWorkOrder] = useState(null)
  const [lwoLoading, setLwoLoading] = useState(false)
  const [lwoActionLoading, setLwoActionLoading] = useState(false)
  const [lwoIntakeForm, setLwoIntakeForm] = useState({
    customerName: '', customerAddress: '', contactPersonName: '', contactPersonPhone: '',
    deliveryPersonName: '', deliveryPersonId: '',
    conditionOnReceipt: '', jobDescription: '', accessoriesReceived: '',
    calibrationSubType: '',
    location: '', stickerNumber: '',
  })
  const [lwoBenchForm, setLwoBenchForm] = useState({ notes: '' })
  const [lwoCertForm, setLwoCertForm] = useState({ certificateNumber: '', jobNumber: '', notes: '' })
  const [lwoDispatchForm, setLwoDispatchForm] = useState({ dispatchMethod: 'ClientPickup', notes: '', receivedBy: '' })
  const [lwoDataSheetSaving, setLwoDataSheetSaving] = useState(false)

  // Vehicle dispatches
  const [dispatches, setDispatches] = useState([])
  const [dispatchesLoading, setDispatchesLoading] = useState(false)
  const [availableVehicles, setAvailableVehicles] = useState([])
  const [dispatchForm, setDispatchForm] = useState({ fieldVehicleId: '', driverName: '', departureDatetime: '', fuelLevelOut: 'Full', notes: '' })
  const [showDispatchModal, setShowDispatchModal] = useState(false)
  const [dispatchSaving, setDispatchSaving] = useState(false)
  const [returnTarget, setReturnTarget] = useState(null)
  const [returnForm, setReturnForm] = useState({ returnDatetime: '', returnOdometer: '', fuelLevelIn: 'Full', notes: '' })
  const [returnSaving, setReturnSaving] = useState(false)
  const [fuelTarget, setFuelTarget] = useState(null)
  const [fuelForm, setFuelForm] = useState({ amountLitres: '', costKes: '', location: '', notes: '' })
  const [fuelSaving, setFuelSaving] = useState(false)

  const fetchCore = useCallback(async () => {
    try {
      const [aRes, ciRes, phRes, attRes, sumRes, srRes] = await Promise.all([
        api.get(`/api/v1/assignments/${id}`),
        api.get(`/api/v1/assignments/${id}/checkins`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/assignments/${id}/photos`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/attachments/by-assignment/${id}`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/assignments/${id}/summaries`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/service-reports/by-assignment/${id}`).catch(() => ({ data: { data: null } })),
      ])
      setAssignment(aRes.data?.data)
      setCheckIns(ciRes.data?.data ?? [])
      setPhotos(phRes.data?.data ?? [])
      setAttachments(attRes.data?.data ?? [])
      setSummaries(sumRes.data?.data ?? [])
      setServiceReport(srRes.data?.data)
    } catch { setAssignment(null) } finally { setLoading(false) }
  }, [id])

  const fetchFinancials = useCallback(async () => {
    try {
      const [reqRes, clRes, pcRes, pdRes, arRes, rfRes] = await Promise.all([
        api.get(`/api/v1/financials/requisitions?assignmentId=${id}&pageSize=50`).catch(() => ({ data: { data: { items: [] } } })),
        api.get(`/api/v1/financials/claims?assignmentId=${id}&pageSize=50`).catch(() => ({ data: { data: { items: [] } } })),
        api.get(`/api/v1/financials/petty-cash/assignment/${id}`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/financials/per-diem/assignment/${id}`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/financials/advance-returns/assignment/${id}`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/financials/refunds/assignment/${id}`).catch(() => ({ data: { data: [] } })),
      ])
      const allReqs = reqRes.data?.data?.items ?? reqRes.data?.data ?? []
      setFinancials({
        requisitions:   allReqs.filter(r => r.justification !== 'Travelling Expenses Voucher'),
        travelVouchers: allReqs.filter(r => r.justification === 'Travelling Expenses Voucher'),
        claims: clRes.data?.data?.items ?? clRes.data?.data ?? [],
        pettyCash: pcRes.data?.data ?? [],
        perDiem: pdRes.data?.data ?? [],
        advanceReturns: arRes.data?.data ?? [],
        refunds: rfRes.data?.data ?? [],
      })
    } catch {}
  }, [id])

  useEffect(() => { fetchCore(); fetchFinancials() }, [fetchCore, fetchFinancials])

  // Auto-load projects for approvers on standalone assignments
  useEffect(() => {
    if (!assignment || assignment.sourceType !== 'Standalone' || !canApprove) return
    api.get('/api/v1/projects?pageSize=200&status=Planning')
      .then(res => setProjects(res.data?.data?.items ?? []))
      .catch(() => setProjects([]))
  }, [assignment?.id])

  // Load vehicle dispatches when Vehicles tab is active
  useEffect(() => {
    if (tab !== 'vehicles' || !id) return
    setDispatchesLoading(true)
    Promise.all([
      api.get(`/api/v1/fleet/field-vehicles/assignments/${id}/dispatches`).catch(() => ({ data: { data: [] } })),
      api.get('/api/v1/fleet/field-vehicles?pageSize=100&status=Available&excludeOpenDispatch=true').catch(() => ({ data: { data: [] } })),
    ]).then(([dRes, vRes]) => {
      setDispatches(dRes.data?.data ?? [])
      setAvailableVehicles(vRes.data?.data ?? [])
    }).finally(() => setDispatchesLoading(false))
  }, [tab, id])

  // Load pre-deployment checklist when tab is active
  useEffect(() => {
    if (tab !== 'pre-deployment' || !id) return
    setPreDeployLoading(true)
    api.get(`/api/v1/assignments/${id}/pre-deployment`)
      .then(r => setPreDeployment(r.data?.data ?? r.data))
      .catch(() => setPreDeployment(null))
      .finally(() => setPreDeployLoading(false))
  }, [tab, id])

  // Load lab work order when tab is active
  useEffect(() => {
    if (tab !== 'lab-work-order' || !id) return
    setLwoLoading(true)
    api.get(`/api/v1/assignments/${id}/lab-work-order`)
      .then(r => setLabWorkOrder(r.data ?? r.data?.data))
      .catch(() => setLabWorkOrder(null))
      .finally(() => setLwoLoading(false))
  }, [tab, id])

  // Load NCRs when Deviations tab is active
  useEffect(() => {
    if (tab !== 'deviations' || !id) return
    setNcrsLoading(true)
    api.get(`/api/v1/assignments/${id}/ncrs`)
      .then(r => setNcrs(r.data?.data ?? r.data ?? []))
      .catch(() => setNcrs([]))
      .finally(() => setNcrsLoading(false))
  }, [tab, id])

  // Load risks + feedback when Feedback tab is active
  useEffect(() => {
    if (tab !== 'feedback' || !id) return
    setFeedbackLoading(true)
    Promise.all([
      api.get(`/api/v1/assignments/${id}/risks`).catch(() => ({ data: [] })),
      api.get(`/api/v1/assignments/${id}/feedback`).catch(() => ({ data: null })),
    ]).then(([rRes, fRes]) => {
      setRisks(rRes.data?.data ?? rRes.data ?? [])
      setCustomerFeedback(fRes.data?.data ?? fRes.data ?? null)
    }).finally(() => setFeedbackLoading(false))
  }, [tab, id])

  const doAction = async (action, body) => {
    setActionLoading(true)
    try {
      if (action === 'accept') await api.post(`/api/v1/assignments/${id}/accept`)
      else if (action === 'start') await api.post(`/api/v1/assignments/${id}/start`)
      else if (action === 'complete') await api.post(`/api/v1/assignments/${id}/complete`)
      else if (action === 'cancel') await api.post(`/api/v1/assignments/${id}/cancel`, body)
      await fetchCore()
    } finally { setActionLoading(false) }
  }

  const handleSubmitForLinking = async () => {
    setActionLoading(true)
    try { await api.post(`/api/v1/assignments/${id}/submit-for-linking`); await fetchCore() }
    finally { setActionLoading(false) }
  }

  const handleLinkToProject = async (e) => {
    e.preventDefault()
    setLinkLoading(true)
    try {
      await api.post(`/api/v1/assignments/${id}/link-to-project`, linkForm)
      setShowLinkModal(false)
      setLinkForm({ projectId: '', milestoneId: '' })
      setMilestones([])
      await fetchCore()
    } finally { setLinkLoading(false) }
  }

  const handleArchive = async (e) => {
    e.preventDefault()
    setLinkLoading(true)
    try {
      await api.post(`/api/v1/assignments/${id}/archive`, { reason: archiveReason })
      setShowArchiveModal(false)
      setArchiveReason('')
      await fetchCore()
    } finally { setLinkLoading(false) }
  }

  const openLinkModal = async () => {
    try {
      const res = await api.get('/api/v1/projects?pageSize=200&status=Planning')
      setProjects(res.data?.data?.items ?? [])
    } catch { setProjects([]) }
    setShowLinkModal(true)
  }

  const onProjectPicked = async (projectId) => {
    setLinkForm(f => ({ ...f, projectId, milestoneId: '' }))
    if (!projectId) { setMilestones([]); return }
    try {
      const res = await api.get(`/api/v1/projects/${projectId}/milestones`)
      setMilestones(res.data?.data ?? [])
    } catch { setMilestones([]) }
  }

  const reloadDispatches = async () => {
    const [dRes, vRes] = await Promise.all([
      api.get(`/api/v1/fleet/field-vehicles/assignments/${id}/dispatches`).catch(() => ({ data: { data: [] } })),
      api.get('/api/v1/fleet/field-vehicles?pageSize=100&status=Available&excludeOpenDispatch=true').catch(() => ({ data: { data: [] } })),
    ])
    setDispatches(dRes.data?.data ?? [])
    setAvailableVehicles(vRes.data?.data ?? [])
  }

  const handleCreateDispatch = async (e) => {
    e.preventDefault()
    setDispatchSaving(true)
    try {
      await api.post('/api/v1/fleet/field-vehicles/dispatches', {
        assignmentId: id,
        fieldVehicleId: dispatchForm.fieldVehicleId,
        driverName: dispatchForm.driverName,
        departureDatetime: new Date(dispatchForm.departureDatetime).toISOString(),
        fuelLevelOut: dispatchForm.fuelLevelOut,
        notes: dispatchForm.notes || null,
      })
      setShowDispatchModal(false)
      setDispatchForm({ fieldVehicleId: '', driverName: '', departureDatetime: '', fuelLevelOut: 'Full', notes: '' })
      await reloadDispatches()
    } catch (err) { alert(err.response?.data?.message || 'Failed to request the vehicle.') }
    finally { setDispatchSaving(false) }
  }

  const handleLogReturn = async (e) => {
    e.preventDefault()
    if (!returnTarget) return
    if (returnTarget.departureOdometer != null && Number(returnForm.returnOdometer) < Number(returnTarget.departureOdometer)) {
      alert(`Return odometer can't be less than the departure odometer (${Number(returnTarget.departureOdometer).toLocaleString()} km).`)
      return
    }
    setReturnSaving(true)
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${returnTarget.id}/return`, {
        returnDatetime: new Date(returnForm.returnDatetime).toISOString(),
        returnOdometer: Number(returnForm.returnOdometer),
        fuelLevelIn: returnForm.fuelLevelIn,
        notes: returnForm.notes || null,
      })
      setReturnTarget(null)
      setReturnForm({ returnDatetime: '', returnOdometer: '', fuelLevelIn: 'Full', notes: '' })
      await reloadDispatches()
    } catch { alert('Failed to log return.') }
    finally { setReturnSaving(false) }
  }

  const handleAddFuelLog = async (e) => {
    e.preventDefault()
    if (!fuelTarget) return
    setFuelSaving(true)
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${fuelTarget.id}/fuel-logs`, {
        amountLitres: Number(fuelForm.amountLitres),
        costKes: Number(fuelForm.costKes),
        location: fuelForm.location || null,
        notes: fuelForm.notes || null,
      })
      setFuelTarget(null)
      setFuelForm({ amountLitres: '', costKes: '', location: '', notes: '' })
      await reloadDispatches()
    } catch { alert('Failed to add fuel log.') }
    finally { setFuelSaving(false) }
  }

  const handleCancelDispatch = async (dispatchId) => {
    if (!confirm('Cancel this dispatch?')) return
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${dispatchId}/cancel`)
      await reloadDispatches()
    } catch { alert('Failed to cancel dispatch.') }
  }

  const handleApproveDispatch = async (dispatchId) => {
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${dispatchId}/approve`, { approvedBy: currentUserName(user) })
      await reloadDispatches()
    } catch { alert('Failed to approve dispatch request.') }
  }

  const handleRejectDispatch = async (dispatchId) => {
    if (!confirm('Reject this vehicle request?')) return
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${dispatchId}/reject`)
      await reloadDispatches()
    } catch { alert('Failed to reject dispatch request.') }
  }

  const handleCheckIn = async (e) => {
    e.preventDefault()
    try {
      const res = await api.post(`/api/v1/assignments/${id}/checkins`, {
        ...checkInForm,
        latitude: parseFloat(checkInForm.latitude) || 0,
        longitude: parseFloat(checkInForm.longitude) || 0,
      })
      await uploadAttachments(pendingFiles, 'CheckIn', res.data?.data?.id ?? id, id)
      closeModal()
      setCheckInForm({ notes: '', latitude: '', longitude: '' })
      fetchCore()
    } catch {
      alert('Failed to check in. Please try again.')
    }
  }
  const handleCheckOut = async (checkInId) => {
    setActionLoading(true)
    try {
      const loc = await getLocation(6000)
      await api.post(`/api/v1/assignments/${id}/checkins/${checkInId}/checkout`, {
        latitude: loc?.latitude ?? null,
        longitude: loc?.longitude ?? null,
      })
      fetchCore()
    } finally {
      setActionLoading(false)
    }
  }
  const handleSummary = async (e) => {
    e.preventDefault()
    try {
      const res = await api.post(`/api/v1/assignments/${id}/summaries`, {
        ...summaryForm,
        date: new Date(summaryForm.date).toISOString(),
        hoursWorked: parseInt(summaryForm.hoursWorked) || 0,
        expensesIncurred: parseFloat(summaryForm.expensesIncurred) || 0,
      })
      await uploadAttachments(pendingFiles, 'DailySummary', res.data?.data?.id ?? id, id)
      closeModal()
      setSummaryForm({ date: todayStr(), summary: '', hoursWorked: '', challenges: '', nextDayPlan: '', expensesIncurred: '' })
      fetchCore()
    } catch {
      alert('Failed to save summary. Please try again.')
    }
  }
  const handleCancel = async (e) => {
    e.preventDefault()
    await doAction('cancel', { reason: cancelReason })
    closeModal(); setCancelReason('')
  }
  const handlePhotoUpload = async (e) => {
    const file = e.target.files?.[0]; if (!file) return
    setPhotoUploading(true)
    const form = new FormData(); form.append('file', file); form.append('caption', file.name)
    try { await api.post(`/api/v1/assignments/${id}/photos`, form, { headers: { 'Content-Type': 'multipart/form-data' } }); fetchCore() }
    finally { setPhotoUploading(false); e.target.value = '' }
  }
  const handleSignSubmit = async (sigData) => {
    if (!serviceReport) return
    setSignLoading(true)
    try {
      await api.post(`/api/v1/service-reports/${serviceReport.id}/sign`, sigData)
      setSignModalOpen(false)
      fetchCore()
    } finally {
      setSignLoading(false)
    }
  }
  const handleRequisition = async (e) => {
    e.preventDefault()
    try {
      const res = await api.post('/api/v1/financials/requisitions', { ...reqForm, assignmentId: id, amount: parseFloat(reqForm.amount) || 0 })
      await uploadAttachments(pendingFiles, 'Requisition', res.data?.data?.id ?? id, id)
      closeModal()
      setReqForm({ type: 'CashAdvance', description: '', amount: '', justification: '' })
      fetchFinancials()
    } catch {
      alert('Failed to submit requisition. Please try again.')
    }
  }
  const handleRequisitionForm = async ({ type, description, amount, justification }) => {
    const res = await api.post('/api/v1/financials/requisitions', {
      type, description, amount: parseFloat(amount) || 0, justification, assignmentId: id,
    })
    await uploadAttachments(pendingFiles, 'Requisition', res.data?.data?.id ?? id, id)
    fetchFinancials()
  }

  const handleClaim = async (e) => {
    e.preventDefault()
    const particularsText = claimForm.particulars
      .filter(r => r.description.trim())
      .map(r => `${r.description} — KShs ${r.kshs}${r.cts ? `.${r.cts}` : ''}`)
      .join('\n')
    const description = [claimForm.beingAnAdvanceFor, particularsText].filter(Boolean).join('\n')
    const justification = JSON.stringify({
      pcvNo: claimForm.pcvNo, date: claimForm.date,
      payeeName: claimForm.payeeName, accountName: claimForm.accountName,
      beingAnAdvanceFor: claimForm.beingAnAdvanceFor,
      particulars: claimForm.particulars,
      amountInWords: claimForm.amountInWords, totalCts: claimForm.totalCts,
      preparedBy: claimForm.preparedBy, checkedBy: claimForm.checkedBy,
      approvedBy: claimForm.approvedBy, receivedBy: claimForm.receivedBy,
    })
    try {
      const res = await api.post('/api/v1/financials/claims', {
        assignmentId: id, description, justification,
        amount: parseFloat(claimForm.amount) || 0,
      })
      await uploadAttachments(pendingFiles, 'Claim', res.data?.data?.id ?? id, id)
      closeModal()
      setClaimForm({
        pcvNo: '', date: new Date().toISOString().split('T')[0],
        payeeName: '', accountName: '', beingAnAdvanceFor: '',
        particulars: [{ description: '', kshs: '', cts: '' }],
        amountInWords: '', amount: '', totalCts: '',
        preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
      })
      fetchFinancials()
    } catch {
      alert('Failed to submit claim. Please try again.')
    }
  }
  const resetPcForm = () => setPcForm({
    pcvNo: '', date: new Date().toISOString().split('T')[0],
    payeeName: '', projectAccount: '', beingAnAdvanceFor: '',
    sum: '', amountInWords: '',
    preparedBy: '', authorisedBy: '', checkedBy: '', receivedBy: '', receivedDate: '', approvedBy: '',
  })
  const handlePettyCash = async (e) => {
    e.preventDefault()
    const meta = JSON.stringify({
      pcvNo: pcForm.pcvNo, date: pcForm.date, payeeName: pcForm.payeeName,
      projectAccount: pcForm.projectAccount, beingAnAdvanceFor: pcForm.beingAnAdvanceFor,
      amountInWords: pcForm.amountInWords, preparedBy: pcForm.preparedBy,
      authorisedBy: pcForm.authorisedBy, checkedBy: pcForm.checkedBy,
      receivedBy: pcForm.receivedBy, receivedDate: pcForm.receivedDate, approvedBy: pcForm.approvedBy,
    })
    try {
      const res = await api.post('/api/v1/financials/petty-cash', {
        assignmentId: id, amount: parseFloat(pcForm.sum) || 0,
        purpose: pcForm.beingAnAdvanceFor || pcForm.projectAccount, description: meta,
      })
      await uploadAttachments(pendingFiles, 'PettyCash', res.data?.data?.id ?? id, id)
      closeModal()
      resetPcForm()
      fetchFinancials()
    } catch {
      alert('Failed to submit petty cash advance. Please try again.')
    }
  }

  const resetPdForm = () => setPdForm({
    refNo: '', date: new Date().toISOString().split('T')[0],
    employeeName: '', periodFrom: '', periodTo: '',
    projectAccount: '', daysCount: '', perDiemRate: '',
    totalAdvanced: '', totalSpent: '', notes: '',
    preparedBy: '', checkedBy: '', approvedBy: '', signedBy: '',
  })
  const handlePerDiem = async (e) => {
    e.preventDefault()
    try {
      const res = await api.post('/api/v1/financials/per-diem', {
        assignmentId: id,
        totalAdvanced: parseFloat(pdForm.totalAdvanced) || 0,
        totalSpent: parseFloat(pdForm.totalSpent) || 0,
        notes: pdForm.notes || null,
        details: JSON.stringify({
          refNo: pdForm.refNo, date: pdForm.date, employeeName: pdForm.employeeName,
          periodFrom: pdForm.periodFrom, periodTo: pdForm.periodTo,
          projectAccount: pdForm.projectAccount, daysCount: pdForm.daysCount, perDiemRate: pdForm.perDiemRate,
          preparedBy: pdForm.preparedBy, checkedBy: pdForm.checkedBy,
          approvedBy: pdForm.approvedBy, signedBy: pdForm.signedBy,
        }),
        lineItems: [],
      })
      await uploadAttachments(pendingFiles, 'PerDiem', res.data?.data?.id ?? id, id)
      closeModal()
      resetPdForm()
      fetchFinancials()
    } catch {
      alert('Failed to submit per diem return. Please try again.')
    }
  }

  const resetArForm = () => setArForm({
    refNo: '', date: new Date().toISOString().split('T')[0],
    returnedBy: '', projectAccount: '',
    totalAdvanced: '', amountReturned: '', amountInWords: '', notes: '',
    preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
  })
  const handleAdvanceReturn = async (e) => {
    e.preventDefault()
    try {
      const res = await api.post('/api/v1/financials/advance-returns', {
        assignmentId: id,
        totalAdvanced: parseFloat(arForm.totalAdvanced) || 0,
        notes: arForm.notes || null,
        details: JSON.stringify({
          refNo: arForm.refNo, date: arForm.date, returnedBy: arForm.returnedBy,
          projectAccount: arForm.projectAccount, amountReturned: arForm.amountReturned,
          amountInWords: arForm.amountInWords, preparedBy: arForm.preparedBy,
          checkedBy: arForm.checkedBy, approvedBy: arForm.approvedBy, receivedBy: arForm.receivedBy,
        }),
        lineItems: [],
      })
      await uploadAttachments(pendingFiles, 'AdvanceReturn', res.data?.data?.id ?? id, id)
      closeModal()
      resetArForm()
      fetchFinancials()
    } catch {
      alert('Failed to submit advance return. Please try again.')
    }
  }

  const resetRefundForm = () => setRefundForm({
    refNo: '', date: new Date().toISOString().split('T')[0],
    payeeName: '', projectAccount: '',
    reason: '', amount: '', amountInWords: '',
    preparedBy: '', checkedBy: '', approvedBy: '', receivedBy: '',
  })
  const handleRefund = async (e) => {
    e.preventDefault()
    const meta = JSON.stringify({
      refNo: refundForm.refNo, date: refundForm.date, payeeName: refundForm.payeeName,
      projectAccount: refundForm.projectAccount, amountInWords: refundForm.amountInWords,
      preparedBy: refundForm.preparedBy, checkedBy: refundForm.checkedBy,
      approvedBy: refundForm.approvedBy, receivedBy: refundForm.receivedBy,
    })
    try {
      const res = await api.post('/api/v1/financials/refunds', {
        assignmentId: id, amount: parseFloat(refundForm.amount) || 0,
        reason: refundForm.reason ? `${refundForm.reason}\n${meta}` : meta,
      })
      await uploadAttachments(pendingFiles, 'Refund', res.data?.data?.id ?? id, id)
      closeModal()
      resetRefundForm()
      fetchFinancials()
    } catch {
      alert('Failed to submit refund. Please try again.')
    }
  }

  const handleReview = async (approved) => {
    if (!reviewItem) return
    try {
      await api.post(reviewItem.endpoint, { approved, comments: reviewComment })
      setReviewItem(null); setReviewComment(''); fetchFinancials()
    } catch { /* server error surfaced via global handler */ }
  }

  if (loading) return <><div className="p-10 text-sm text-gray-400">Loading…</div></>
  if (!assignment) return <><div className="p-10 text-sm text-red-500">Assignment not found.</div></>

  const openCheckin = checkIns.find(c => !c.checkedOutAt)
  const canAccept = assignment.status === 'Pending'
  const canStart = assignment.status === 'Accepted'
  const canComplete = assignment.status === 'InProgress'
  const canCancel = !['Completed', 'Cancelled', 'AwaitingProjectLink', 'Archived'].includes(assignment.status)
  const isActive = assignment.status === 'InProgress'
  const isStandalone = assignment.sourceType === 'Standalone'
  const canSubmitForLinking = isStandalone && assignment.status === 'Completed'
  const canLinkOrArchive = isStandalone && canApprove

  const parseReqDesc = (description) => {
    try {
      const p = JSON.parse(description)
      if (p.formType && Array.isArray(p.items)) {
        const filled = p.items.filter(i => i.description?.trim()).length
        const label = p.formType === 'A1' ? 'Form A-1' : 'Form A-2'
        return `${label} · ${p.projectName || ''}${filled ? ` · ${filled} item${filled !== 1 ? 's' : ''}` : ''}`
      }
    } catch {}
    return description
  }

  const finTotals = {
    requisitions: financials.requisitions.reduce((s, r) => s + (r.amount ?? 0), 0),
    claims: financials.claims.reduce((s, c) => s + (c.amount ?? 0), 0),
    pettyCash: financials.pettyCash.reduce((s, p) => s + (p.sum ?? p.amount ?? 0), 0),
  }

  const finBadgeCount = financials.requisitions.length + financials.travelVouchers.length +
    financials.claims.length + financials.pettyCash.length + financials.perDiem.length +
    financials.advanceReturns.length + financials.refunds.length

  const isInLab = assignment.natureOfVisit === 'Calibration' || assignment.serviceRequestDataJson?.includes('"serviceLocation":"InLab"') || assignment.serviceRequestDataJson?.includes('"serviceLocation": "InLab"')
  const isOnSite = !isInLab
  const isMassCalibration = (() => {
    try {
      const sr = JSON.parse(assignment.serviceRequestDataJson ?? 'null')
      return sr?.instruments?.some(i => i.massNominalValue || i.massAccuracyClass) ?? false
    } catch { return false }
  })()
  const TABS = [
    ...BASE_TABS,
    ...(isOnSite ? [PREDEPLOYMENT_TAB] : []),
    ...(isInLab  ? [LAB_TAB]          : []),
    DEVIATIONS_TAB,
    FEEDBACK_TAB,
  ]

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => navigate('/modules/operations/assignments')}
          className="text-sm text-gray-500 hover:text-gray-700 mb-5 transition-colors flex items-center gap-1">
          ← Back to Assignments
        </button>

        {/* Header */}
        <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
          <div>
            <div className="flex items-center gap-2.5 flex-wrap">
              <h1 className="text-2xl font-extrabold text-zinc-950">{assignment.title}</h1>
              <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[assignment.status] ?? 'bg-gray-100 text-gray-600'}`}>
                {assignment.status}
              </span>
              <span className={`text-xs font-bold ${PRIORITY_TEXT[assignment.priority] ?? 'text-gray-400'}`}>
                {assignment.priority} Priority
              </span>
              {assignment.sourceType === 'ProjectTask' && (
                <span className="text-xs font-semibold text-blue-600 bg-blue-50 border border-blue-200 rounded-md px-2 py-0.5">↗ From Project Task</span>
              )}
              {assignment.sourceType === 'Ticket' && (
                <span className="text-xs font-semibold text-orange-500 bg-orange-50 border border-orange-200 rounded-md px-2 py-0.5">↗ From Ticket</span>
              )}
            </div>
            <p className="text-sm text-gray-500 mt-1">
              {assignment.departmentType} · {assignment.natureOfVisit}
              {assignment.locationName && ` · ${assignment.locationName}`}
            </p>
          </div>
          <div className="flex gap-2 flex-wrap">
            {canAccept && canAct && <Btn label="Accept" variant="green" onClick={() => doAction('accept')} loading={actionLoading} />}
            {canStart && canAct && <Btn label="Start" variant="blue" onClick={() => doAction('start')} loading={actionLoading} />}
            {canComplete && canAct && <Btn label="Mark Complete" variant="indigo" onClick={() => doAction('complete')} loading={actionLoading} />}
            {isActive && !openCheckin && canAct && <Btn label="Check In" variant="amber" onClick={async () => {
              setModal('checkin')
              setGeoStatus('loading')
              const loc = await getLocation()
              if (loc) {
                setCheckInForm(f => ({ ...f, latitude: loc.latitude.toString(), longitude: loc.longitude.toString() }))
                setGeoStatus('ok')
              } else {
                setGeoStatus('error')
              }
            }} />}
            {openCheckin && canAct && <Btn label="Check Out" variant="orange" onClick={() => handleCheckOut(openCheckin.id)} loading={actionLoading} />}
            {canSubmitForLinking && canAct && <Btn label="Submit for Project Linking" variant="purple" onClick={handleSubmitForLinking} loading={actionLoading} />}
            {canLinkOrArchive && <Btn label="Link to Project" variant="dark" onClick={openLinkModal} />}
            {canLinkOrArchive && <Btn label="Archive" variant="gray" onClick={() => setShowArchiveModal(true)} />}
            {canCancel && (canWrite || canApprove) && <Btn label="Cancel" variant="red" onClick={() => setModal('cancel')} />}
          </div>
        </div>

        {/* Tabs */}
        <TabBar tabs={TABS} active={tab} onChange={setTab}
          badges={{ financials: finBadgeCount || null, checkins: checkIns.length || null, photos: (photos.length + attachments.length) || null, summaries: summaries.length || null }} />

        {/* ── OVERVIEW ── */}
        {tab === 'overview' && (
          <div>
            {/* Inline linkage action panel */}
            {canLinkOrArchive && (
              <div className="mb-6 bg-purple-50 border border-purple-200 rounded-xl p-5">
                <div className="flex items-center gap-2 mb-4">
                  <svg className="w-4 h-4 text-purple-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  <div>
                    <p className="text-sm font-bold text-purple-900">Link to Project</p>
                    <p className="text-xs text-purple-600 mt-0.5">Select a project to link this standalone assignment to, or archive it.</p>
                  </div>
                </div>
                <form onSubmit={handleLinkToProject} className="flex flex-col sm:flex-row gap-3 items-end">
                  <div className="flex-1 min-w-0">
                    <label className="text-xs font-semibold text-purple-700 block mb-1">Project *</label>
                    <select required value={linkForm.projectId} onChange={e => onProjectPicked(e.target.value)} className="input">
                      <option value="">— Select a project —</option>
                      {projects.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </div>
                  {milestones.length > 0 && (
                    <div className="flex-1 min-w-0">
                      <label className="text-xs font-semibold text-purple-700 block mb-1">Milestone (optional)</label>
                      <select value={linkForm.milestoneId} onChange={e => setLinkForm(f => ({ ...f, milestoneId: e.target.value }))} className="input">
                        <option value="">— None —</option>
                        {milestones.map(m => <option key={m.id} value={m.id}>{m.name ?? m.title}</option>)}
                      </select>
                    </div>
                  )}
                  <div className="flex gap-2 shrink-0">
                    <button type="submit" disabled={linkLoading}
                      className="px-4 py-2 rounded-lg bg-purple-600 hover:bg-purple-700 text-white text-sm font-semibold transition-colors disabled:opacity-70">
                      {linkLoading ? 'Linking…' : 'Link to Project'}
                    </button>
                    <button type="button" onClick={() => setShowArchiveModal(true)}
                      className="px-4 py-2 rounded-lg bg-white border border-gray-200 text-gray-600 text-sm font-semibold hover:bg-gray-50 transition-colors">
                      Archive
                    </button>
                  </div>
                </form>
              </div>
            )}

            {canExport && (
              <div className="flex justify-end mb-4 gap-2">
                <ExportButtons onPDF={() => exportOverviewPDF(assignment, branding.legalName)} onExcel={() => exportOverviewExcel(assignment, branding.legalName)} />
              </div>
            )}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <InfoCard title="Assignment Info">
                <InfoRow label="Department" value={assignment.departmentType} />
                <InfoRow label="Nature of Visit" value={assignment.natureOfVisit} />
                <InfoRow label="Source" value={
                  assignment.sourceType === 'ProjectTask' ? '↗ Project Task'
                  : assignment.sourceType === 'Ticket' ? '↗ Ticket'
                  : 'Standalone'
                } />
                <InfoRow label="Priority" value={assignment.priority} />
                {assignment.linkedProjectTaskId && <InfoRow label="Task ID" value={assignment.linkedProjectTaskId.slice(0, 8) + '…'} />}
                {assignment.linkedTicketId && <InfoRow label="Linked Ticket" value={assignment.linkedTicketId.slice(0, 8) + '…'} />}
                {assignment.linkedProjectId && <InfoRow label="Linked Project" value={assignment.linkedProjectId.slice(0, 8) + '…'} />}
                {assignment.linkedMilestoneId && <InfoRow label="Linked Milestone" value={assignment.linkedMilestoneId.slice(0, 8) + '…'} />}
                {assignment.linkedAt && <InfoRow label="Linked On" value={new Date(assignment.linkedAt).toLocaleDateString()} />}
                {assignment.archiveReason && <InfoRow label="Archive Reason" value={assignment.archiveReason} />}
                <InfoRow label="Created" value={new Date(assignment.createdAt).toLocaleDateString()} />
                {assignment.deadline && <InfoRow label="Deadline" value={new Date(assignment.deadline).toLocaleDateString()} />}
                {assignment.acceptedAt && <InfoRow label="Accepted" value={new Date(assignment.acceptedAt).toLocaleDateString()} />}
                {assignment.startedAt && <InfoRow label="Started" value={new Date(assignment.startedAt).toLocaleDateString()} />}
                {assignment.completedAt && <InfoRow label="Completed" value={new Date(assignment.completedAt).toLocaleDateString()} />}
              </InfoCard>
              <InfoCard title="Location & Team">
                <InfoRow label="Location" value={assignment.locationName ?? '—'} />
                <InfoRow label="Address" value={assignment.locationAddress ?? '—'} />
                {assignment.locationLatitude && <InfoRow label="Coordinates" value={`${assignment.locationLatitude?.toFixed(4)}, ${assignment.locationLongitude?.toFixed(4)}`} />}
                <div className="mt-3 pt-3 border-t border-gray-100">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">Assigned Technicians</p>
                  {assignment.technicians?.length > 0
                    ? assignment.technicians.map(t => <div key={t.userId} className="text-sm text-gray-900 py-0.5">{t.userName}</div>)
                    : <span className="text-sm text-gray-400">Unassigned</span>}
                </div>
              </InfoCard>
              <div className="grid grid-cols-3 gap-3 lg:col-span-2">
                <StatCard label="Requisitions" value={`KES ${finTotals.requisitions.toLocaleString()}`} sub={`${financials.requisitions.length} item${financials.requisitions.length !== 1 ? 's' : ''}`} color="text-amber-600" />
                <StatCard label="Claims" value={`KES ${finTotals.claims.toLocaleString()}`} sub={`${financials.claims.length} item${financials.claims.length !== 1 ? 's' : ''}`} color="text-blue-600" />
                <StatCard label="Petty Cash" value={`KES ${finTotals.pettyCash.toLocaleString()}`} sub={`${financials.pettyCash.length} item${financials.pettyCash.length !== 1 ? 's' : ''}`} color="text-purple-600" />
              </div>
              {assignment.description && (
                <div className="lg:col-span-2">
                  <InfoCard title="Description"><p className="text-sm text-gray-700 leading-relaxed">{assignment.description}</p></InfoCard>
                </div>
              )}
              {assignment.notes && (
                <div className="lg:col-span-2">
                  <InfoCard title="Notes"><p className="text-sm text-gray-700 leading-relaxed">{assignment.notes}</p></InfoCard>
                </div>
              )}
              {assignment.serviceRequestDataJson && (() => {
                let sr = null
                try { sr = JSON.parse(assignment.serviceRequestDataJson) } catch { return null }
                if (!sr) return null
                return (
                  <div className="lg:col-span-2">
                    <InfoCard title={`Service Request — ${sr.referenceNumber ?? ''}`}>
                      <div className="grid grid-cols-2 sm:grid-cols-3 gap-4 mb-4">
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Reference</p><p className="text-sm font-mono font-bold text-zinc-800">{sr.referenceNumber}</p></div>
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Form Type</p><p className="text-sm text-gray-800">{sr.formType}</p></div>
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Client</p><p className="text-sm text-gray-800">{sr.clientName}{sr.clientOrg ? ` (${sr.clientOrg})` : ''}</p></div>
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Email</p><p className="text-sm text-gray-800">{sr.clientEmail ?? '—'}</p></div>
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Phone</p><p className="text-sm text-gray-800">{sr.clientPhone ?? '—'}</p></div>
                        <div><p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">Site</p><p className="text-sm text-gray-800">{sr.siteLocation ?? '—'}</p></div>
                      </div>
                      {sr.instruments?.length > 0 && (
                        <>
                          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">Instruments ({sr.instrumentCount ?? sr.instruments.length})</p>
                          <div className="rounded-lg border border-gray-200 overflow-hidden">
                            <table className="w-full text-sm">
                              <thead className="bg-gray-50 border-b border-gray-200">
                                <tr>
                                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-500">#</th>
                                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-500">Manufacturer / Model</th>
                                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-500">Serial No.</th>
                                  <th className="px-3 py-2 text-left text-xs font-semibold text-gray-500 hidden sm:table-cell">Service / Type</th>
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-gray-100">
                                {sr.instruments.map((inst, i) => (
                                  <tr key={i} className="hover:bg-gray-50">
                                    <td className="px-3 py-2 text-gray-400 text-xs">{inst.rowNumber ?? i + 1}</td>
                                    <td className="px-3 py-2">
                                      <p className="font-medium text-gray-900">{[inst.manufacturer, inst.model].filter(Boolean).join(' ') || '—'}</p>
                                      {inst.nawiInstrumentType && <p className="text-xs text-gray-400">{inst.nawiInstrumentType}</p>}
                                      {inst.massNominalValue && <p className="text-xs text-gray-400">Nominal: {inst.massNominalValue}</p>}
                                    </td>
                                    <td className="px-3 py-2 font-mono text-xs text-gray-600">{inst.serialNumber || '—'}</td>
                                    <td className="px-3 py-2 text-gray-600 text-xs hidden sm:table-cell">{inst.serviceType || inst.massAccuracyClass || '—'}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </>
                      )}
                      {sr.description && <p className="text-sm text-gray-600 mt-3 pt-3 border-t border-gray-100">{sr.description}</p>}
                      {sr.specialInstructions && (
                        <div className="mt-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                          <p className="text-xs font-semibold text-amber-700 mb-0.5">Special Instructions</p>
                          <p className="text-xs text-amber-800">{sr.specialInstructions}</p>
                        </div>
                      )}
                    </InfoCard>
                  </div>
                )
              })()}
            </div>
          </div>
        )}

        {/* ── CHECK-INS ── */}
        {tab === 'checkins' && (
          <div>
            {canExport && checkIns.length > 0 && (
              <div className="flex justify-end mb-4 gap-2">
                <ExportButtons onPDF={() => exportCheckInsPDF(checkIns, assignment, branding.legalName)} onExcel={() => exportCheckInsExcel(checkIns, assignment, branding.legalName)} />
              </div>
            )}
            {checkIns.length === 0 ? <Empty message="No check-ins recorded yet." /> : (
              <div className="flex flex-col gap-3">
                {checkIns.map(c => (
                  <div key={c.id} className="bg-white border border-gray-100 rounded-xl p-4 flex justify-between items-start">
                    <div>
                      <p className="text-sm font-semibold text-gray-900">Check-in</p>
                      {c.notes && <p className="text-sm text-gray-500 mt-0.5">{c.notes}</p>}
                      <p className="text-xs text-gray-400 mt-1">{c.checkInLatitude?.toFixed(4)}, {c.checkInLongitude?.toFixed(4)}</p>
                    </div>
                    <div className="text-right shrink-0 ml-4">
                      <p className="text-sm text-gray-700">In: {new Date(c.checkedInAt).toLocaleString()}</p>
                      {c.checkedOutAt
                        ? <p className="text-sm text-gray-700">Out: {new Date(c.checkedOutAt).toLocaleString()}</p>
                        : <span className="inline-block mt-1 px-2 py-0.5 rounded-full text-xs font-bold bg-green-100 text-green-700">Active</span>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ── DAILY SUMMARIES ── */}
        {tab === 'summaries' && (
          <div>
            <div className="flex justify-end items-center mb-4 gap-2">
              {canExport && summaries.length > 0 && <ExportButtons onPDF={() => exportSummariesPDF(summaries, assignment, branding.legalName)} onExcel={() => exportSummariesExcel(summaries, assignment, branding.legalName)} />}
              {canAct && <Btn label="+ Add Summary" variant="amber" onClick={() => setModal('summary')} />}
            </div>
            {summaries.length === 0 ? <Empty message="No daily summaries yet." /> : (
              <div className="flex flex-col gap-3">
                {summaries.map(s => (
                  <div key={s.id} className="bg-white border border-gray-100 rounded-xl p-4">
                    <div className="flex justify-between mb-3">
                      <span className="text-sm font-bold text-gray-700">{s.submittedByName ?? '—'}</span>
                      <span className="text-sm text-gray-400">{new Date(s.date ?? s.createdAt).toLocaleDateString()} · {s.hoursWorked}h worked</span>
                    </div>
                    <SummarySection label="Summary" text={s.summary} />
                    <SummarySection label="Challenges" text={s.challenges} color="text-red-500" />
                    <SummarySection label="Next Day Plan" text={s.nextDayPlan} color="text-blue-500" />
                    {s.expensesIncurred > 0 && <p className="text-sm text-amber-600 font-semibold mt-2">Expenses: KES {s.expensesIncurred?.toLocaleString()}</p>}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ── SERVICE REPORT ── */}
        {tab === 'service-report' && (
          <div>
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-base font-bold text-gray-900">Service Report</h3>
              <div className="flex gap-2 items-center">
                {serviceReport && canExport && <ExportButtons onPDF={() => exportServiceReportPDF(serviceReport, assignment, branding.legalName)} onExcel={() => exportServiceReportExcel(serviceReport, assignment, branding.legalName)} />}
                {serviceReport && serviceReport.status === 'Draft' && canAct && <Btn label="Sign & Submit" variant="green" onClick={() => setSignModalOpen(true)} />}
                {serviceReport && canAct && <Btn label="Edit Report" variant="amber" onClick={() => navigate(`/modules/operations/assignments/${id}/report/${serviceReport.id}/edit`)} />}
                {serviceReport && <Btn label="Open FSR" variant="indigo" onClick={() => navigate(`/modules/operations/service-reports/${serviceReport.id}`)} />}
                {!serviceReport && canAct && <Btn label="Create Report" variant="amber" onClick={() => navigate(`/modules/operations/assignments/${id}/report/new`)} />}
              </div>
            </div>
            {!serviceReport ? (
              <Empty message="No service report yet. Create one to capture site work details." />
            ) : (
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <InfoCard title="Report Details">
                  <InfoRow label="Status" value={<span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${FIN_STATUS_BADGE[serviceReport.status] ?? 'bg-gray-100 text-gray-500'}`}>{serviceReport.status}</span>} />
                  <InfoRow label="Department" value={serviceReport.departmentType} />
                  <InfoRow label="Nature of Visit" value={serviceReport.natureOfVisit} />
                  <InfoRow label="Technician" value={serviceReport.technicianName} />
                  {serviceReport.startDay && <InfoRow label="Start" value={new Date(serviceReport.startDay).toLocaleDateString()} />}
                  {serviceReport.endDay && <InfoRow label="End" value={new Date(serviceReport.endDay).toLocaleDateString()} />}
                  {serviceReport.totalMinutes && <InfoRow label="Duration" value={`${Math.floor(serviceReport.totalMinutes / 60)}h ${serviceReport.totalMinutes % 60}m`} />}
                </InfoCard>
                <InfoCard title="Customer Details">
                  <InfoRow label="Customer" value={serviceReport.customerName} />
                  <InfoRow label="Location" value={serviceReport.locationName} />
                  <InfoRow label="Address" value={serviceReport.locationAddress ?? '—'} />
                  <InfoRow label="Contact" value={serviceReport.contactPerson ?? '—'} />
                  <InfoRow label="Phone" value={serviceReport.contactPhone ?? '—'} />
                  <InfoRow label="Email" value={serviceReport.contactEmail ?? '—'} />
                </InfoCard>
                {serviceReport.customerComments && (
                  <div className="lg:col-span-2">
                    <InfoCard title="Customer Comments">
                      <p className="text-sm text-gray-700">{serviceReport.customerComments}</p>
                    </InfoCard>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* ── FINANCIALS ── */}
        {tab === 'financials' && (
          <div>
            <div className="flex justify-between items-center mb-4">
              <div className="flex gap-0 border-b-2 border-gray-100 overflow-x-auto">
                {FIN_SUBTABS.map(t => {
                  const count = { requisitions: financials.requisitions.length, 'travel-voucher': financials.travelVouchers.length, claims: financials.claims.length, 'petty-cash': financials.pettyCash.length, 'per-diem': financials.perDiem.length, 'advance-returns': financials.advanceReturns.length, refunds: financials.refunds.length }[t]
                  return (
                    <button key={t} onClick={() => setFinTab(t)}
                      className={`px-4 py-2 text-sm whitespace-nowrap border-b-2 -mb-0.5 transition-colors capitalize ${finTab === t ? 'font-bold text-amber-600 border-amber-500' : 'font-medium text-gray-500 border-transparent hover:text-gray-700'}`}>
                      {t.replace(/-/g, ' ')}
                      {finTab !== t && count > 0 && (
                        <span className="ml-1.5 text-xs bg-amber-100 text-amber-600 rounded-full px-1.5 py-0.5">{count}</span>
                      )}
                    </button>
                  )
                })}
              </div>
              {canExport && (
                <ExportButtons onPDF={() => exportFinancialsPDF(financials, assignment, branding.legalName)} onExcel={() => exportFinancialsExcel(financials, assignment, branding.legalName)} />
              )}
            </div>

            {finTab === 'requisitions' && (
              <FinSection items={financials.requisitions} onAdd={canAct ? () => setModal('req-type-chooser') : null}
                addLabel="+ New Requisition"
                columns={['Type', 'Description', 'Amount', 'Status']}
                renderRow={r => [r.type, parseReqDesc(r.description), `KES ${(r.amount ?? 0).toLocaleString()}`, <FinBadge status={r.status} />]}
                emptyMsg="No requisitions yet."
                rowActions={canApprove ? r => r.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/requisitions/${r.id}/review/manager`, label: parseReqDesc(r.description) })} />
                  : r.status === 'TmApproved'
                  ? <Btn small label="CFO Review" variant="purple" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/requisitions/${r.id}/review/cfo`, label: parseReqDesc(r.description) })} />
                  : null : null} />
            )}

            {finTab === 'travel-voucher' && (
              <FinSection
                items={financials.travelVouchers}
                onAdd={canAct ? () => setShowTravelVoucher(true) : null}
                addLabel="+ New Travel Voucher"
                columns={['Employee', 'Date From', 'Total (KES)', 'Status']}
                renderRow={r => {
                  let parsed = null
                  try { parsed = JSON.parse(r.description) } catch { /* not JSON */ }
                  return [
                    parsed?.name || r.justification || '—',
                    parsed?.dateFrom || new Date(r.createdAt).toLocaleDateString(),
                    `KES ${(r.amount ?? 0).toLocaleString()}`,
                    <FinBadge status={r.status} />,
                  ]
                }}
                emptyMsg="No travel vouchers yet."
                rowActions={r => {
                  let parsed = null
                  try { parsed = JSON.parse(r.description) } catch { /* not JSON */ }
                  return (
                    <div className="flex gap-1.5">
                      {canExport && parsed && (
                        <Btn small label="↓ PDF" variant="slate" onClick={() => exportTravelVoucherPDF(parsed, assignment, branding.docPrefix)} />
                      )}
                      {canApprove && r.status === 'Pending' && (
                        <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/requisitions/${r.id}/review/manager`, label: 'Travel Voucher' })} />
                      )}
                    </div>
                  )
                }} />
            )}
            {finTab === 'claims' && (
              <FinSection items={financials.claims} onAdd={canAct ? () => setModal('claim') : null}
                addLabel="+ New Claim"
                columns={['Description', 'Amount', 'Status']}
                renderRow={c => [c.description, `KES ${(c.amount ?? 0).toLocaleString()}`, <FinBadge status={c.status} />]}
                emptyMsg="No claims yet."
                rowActions={canApprove ? c => c.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/claims/${c.id}/review/manager`, label: c.description })} />
                  : c.status === 'ManagerApproved'
                  ? <Btn small label="CFO Review" variant="purple" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/claims/${c.id}/review/cfo`, label: c.description })} />
                  : null : null} />
            )}
            {finTab === 'petty-cash' && (
              <FinSection items={financials.pettyCash} onAdd={canAct ? () => setModal('petty-cash') : null}
                addLabel="+ New Petty Cash"
                columns={['Description', 'Amount', 'Status']}
                renderRow={p => [p.description || p.purpose, `KES ${(p.sum ?? p.amount ?? 0).toLocaleString()}`, <FinBadge status={p.status} />]}
                emptyMsg="No petty cash advances yet."
                rowActions={canApprove ? p => p.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/petty-cash/${p.id}/review/manager`, label: p.description || p.purpose })} />
                  : p.status === 'Approved'
                  ? <Btn small label="CFO Review" variant="purple" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/petty-cash/${p.id}/review/cfo`, label: p.description || p.purpose })} />
                  : null : null} />
            )}
            {finTab === 'per-diem' && (
              <FinSection items={financials.perDiem} onAdd={canAct ? () => setModal('per-diem') : null}
                addLabel="+ New Per Diem Return"
                columns={['Notes', 'Advanced', 'Spent', 'Status']}
                renderRow={p => [p.notes ?? '—', `KES ${(p.totalAdvanced ?? 0).toLocaleString()}`, `KES ${(p.totalSpent ?? 0).toLocaleString()}`, <FinBadge status={p.status} />]}
                emptyMsg="No per diem returns yet."
                rowActions={canApprove ? p => p.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/per-diem/${p.id}/review/manager`, label: p.notes || 'Per Diem' })} />
                  : p.status === 'Approved'
                  ? <Btn small label="CFO Review" variant="purple" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/per-diem/${p.id}/review/cfo`, label: p.notes || 'Per Diem' })} />
                  : null : null} />
            )}
            {finTab === 'advance-returns' && (
              <FinSection items={financials.advanceReturns} onAdd={canAct ? () => setModal('advance-return') : null}
                addLabel="+ New Advance Return"
                columns={['Notes', 'Total Advanced', 'Status']}
                renderRow={a => [a.notes ?? '—', `KES ${(a.totalAdvanced ?? 0).toLocaleString()}`, <FinBadge status={a.status} />]}
                emptyMsg="No advance returns yet."
                rowActions={canApprove ? a => a.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/advance-returns/${a.id}/review/manager`, label: a.notes || 'Advance Return' })} />
                  : a.status === 'Approved'
                  ? <Btn small label="CFO Review" variant="purple" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/advance-returns/${a.id}/review/cfo`, label: a.notes || 'Advance Return' })} />
                  : null : null} />
            )}
            {finTab === 'refunds' && (
              <FinSection items={financials.refunds} onAdd={canAct ? () => setModal('refund') : null}
                addLabel="+ New Refund"
                columns={['Reason', 'Amount', 'Status']}
                renderRow={r => [r.reason, `KES ${(r.amount ?? 0).toLocaleString()}`, <FinBadge status={r.status} />]}
                emptyMsg="No refunds yet."
                rowActions={canApprove ? r => r.status === 'Pending'
                  ? <Btn small label="Review" variant="amber" onClick={() => setReviewItem({ endpoint: `/api/v1/financials/refunds/${r.id}/review`, label: r.reason })} />
                  : null : null} />
            )}
          </div>
        )}

        {/* ── PHOTOS ── */}
        {tab === 'photos' && (
          <div>
            {canAct && (
              <div className="flex justify-end mb-4">
                <label className="bg-amber-400 hover:bg-amber-500 text-black text-sm font-semibold rounded-lg px-4 py-2 cursor-pointer transition-colors">
                  {photoUploading ? 'Uploading…' : '+ Upload Photo'}
                  <input type="file" accept="image/*" className="hidden" onChange={handlePhotoUpload} disabled={photoUploading} />
                </label>
              </div>
            )}
            {(photos.length === 0 && attachments.length === 0) ? <Empty message="No photos uploaded yet." /> : (
              <div>
                {photos.length > 0 && (
                  <div className={attachments.length > 0 ? 'mb-6' : ''}>
                    <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-3">Assignment Photos</p>
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                      {[...photos].sort((a, b) => new Date(b.uploadedAt ?? b.createdAt) - new Date(a.uploadedAt ?? a.createdAt)).map(p => (
                        <div key={p.id} className="bg-white border border-gray-100 rounded-xl overflow-hidden cursor-pointer hover:shadow-md transition-shadow"
                          onClick={() => setLightboxPhoto({ src: `${import.meta.env.VITE_API_URL || ''}${p.url}`, caption: p.caption })}>
                          <img src={`${import.meta.env.VITE_API_URL || ''}${p.url}`} alt={p.caption ?? 'Photo'}
                            className="w-full h-36 object-cover block"
                            onError={e => { e.target.style.display = 'none' }} />
                          <div className="p-2">
                            {p.caption && <p className="text-xs font-medium text-gray-700 truncate">{p.caption}</p>}
                            <p className="text-xs text-gray-400">{new Date(p.uploadedAt ?? p.createdAt).toLocaleDateString()}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                {attachments.length > 0 && (
                  <div>
                    <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-3">Form Attachments</p>
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                      {[...attachments].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).map(a => (
                        <div key={a.id} className="bg-white border border-gray-100 rounded-xl overflow-hidden cursor-pointer hover:shadow-md transition-shadow"
                          onClick={() => setLightboxPhoto({ src: `${import.meta.env.VITE_API_URL || ''}${a.url}`, caption: a.fileName })}>
                          <img src={`${import.meta.env.VITE_API_URL || ''}${a.url}`} alt={a.fileName ?? 'Attachment'}
                            className="w-full h-36 object-cover block"
                            onError={e => { e.target.style.display = 'none' }} />
                          <div className="p-2">
                            <p className="text-xs text-gray-500 font-medium">{a.entityType}</p>
                            <p className="text-xs text-gray-400">{new Date(a.createdAt).toLocaleDateString()}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {tab === 'vehicles' && (
          <VehiclesTab
            dispatches={dispatches}
            loading={dispatchesLoading}
            canAct={canAct}
            canApprove={canApproveDispatch}
            availableVehicles={availableVehicles}
            onAddDispatch={() => { setDispatchForm(f => ({ ...f, driverName: currentUserName(user) })); setShowDispatchModal(true) }}
            onLogReturn={d => { setReturnTarget(d); setReturnForm({ returnDatetime: new Date().toISOString().slice(0,16), returnOdometer: d.departureOdometer, fuelLevelIn: 'Full', notes: '' }) }}
            onAddFuel={d => setFuelTarget(d)}
            onCancel={handleCancelDispatch}
            onApprove={handleApproveDispatch}
            onReject={handleRejectDispatch}
          />
        )}

        {tab === 'pre-deployment' && (
          <PreDeploymentTab
            assignmentId={id}
            data={preDeployment}
            loading={preDeployLoading}
            actionLoading={preDeployActionLoading}
            canAct={canAct}
            onSave={async (checklistJson, notes, submit) => {
              setPreDeployActionLoading(true)
              try {
                const endpoint = submit
                  ? `/api/v1/assignments/${id}/pre-deployment/submit`
                  : `/api/v1/assignments/${id}/pre-deployment`
                const res = await api.post(endpoint, { checklistJson, notes })
                setPreDeployment(res.data?.data ?? res.data)
              } finally { setPreDeployActionLoading(false) }
            }}
          />
        )}

        {tab === 'lab-work-order' && (
          <LabWorkOrderTab
            assignmentId={id}
            assignment={assignment}
            labWorkOrder={labWorkOrder}
            docPrefix={branding.docPrefix}
            isInLab={isInLab}
            isMassCalibration={isMassCalibration}
            loading={lwoLoading}
            actionLoading={lwoActionLoading}
            dsActionLoading={lwoDataSheetSaving}
            canAct={canAct}
            intakeForm={lwoIntakeForm}
            setIntakeForm={setLwoIntakeForm}
            benchForm={lwoBenchForm}
            setBenchForm={setLwoBenchForm}
            certForm={lwoCertForm}
            setCertForm={setLwoCertForm}
            dispatchForm={lwoDispatchForm}
            setDispatchForm={setLwoDispatchForm}
            onAction={async (stage, payload) => {
              setLwoActionLoading(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/lab-work-order/${stage}`, payload)
                const updated = res.data?.data ?? res.data
                setLabWorkOrder(updated)
                return updated
              } finally { setLwoActionLoading(false) }
            }}
            onDataSheet={async (rawDataJson, submit) => {
              setLwoDataSheetSaving(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/lab-work-order/data-sheet`, { rawDataJson, submit })
                const sheet = res.data?.data ?? res.data
                setLabWorkOrder(prev => prev ? { ...prev, dataSheet: sheet, status: submit ? 'AwaitingTmReview' : prev.status } : prev)
                return sheet
              } finally { setLwoDataSheetSaving(false) }
            }}
            onInit={async () => {
              setLwoActionLoading(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/lab-work-order`)
                setLabWorkOrder(res.data?.data ?? res.data)
              } finally { setLwoActionLoading(false) }
            }}
            onRecalculate={async () => {
              setLwoDataSheetSaving(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/lab-work-order/data-sheet/recalculate`)
                const sheet = res.data?.data ?? res.data
                setLabWorkOrder(prev => prev ? { ...prev, dataSheet: sheet } : prev)
              } finally { setLwoDataSheetSaving(false) }
            }}
          />
        )}

        {tab === 'deviations' && (
          <DeviationsTab
            assignmentId={id}
            ncrs={ncrs}
            loading={ncrsLoading}
            actionLoading={ncrActionLoading}
            canAct={canAct}
            onCreated={ncr => setNcrs(prev => [ncr, ...prev])}
            onUpdated={ncr => setNcrs(prev => prev.map(n => n.id === ncr.id ? ncr : n))}
            onAction={async (ncrId, action, payload) => {
              setNcrActionLoading(true)
              try {
                const url = action === 'close'
                  ? `/api/v1/assignments/${id}/ncrs/${ncrId}/close`
                  : `/api/v1/assignments/${id}/ncrs/${ncrId}`
                const method = action === 'close' ? 'post' : 'put'
                const res = await api[method](url, payload)
                const updated = res.data?.data ?? res.data
                setNcrs(prev => prev.map(n => n.id === ncrId ? updated : n))
              } finally { setNcrActionLoading(false) }
            }}
            onCreate={async payload => {
              setNcrActionLoading(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/ncrs`, payload)
                const ncr = res.data?.data ?? res.data
                setNcrs(prev => [ncr, ...prev])
              } finally { setNcrActionLoading(false) }
            }}
          />
        )}

        {tab === 'feedback' && (
          <FeedbackTab
            assignmentId={id}
            risks={risks}
            customerFeedback={customerFeedback}
            loading={feedbackLoading}
            actionLoading={feedbackActionLoading}
            canAct={canAct}
            onRiskCreated={r => setRisks(prev => [r, ...prev])}
            onRiskUpdated={r => setRisks(prev => prev.map(x => x.id === r.id ? r : x))}
            onRiskDeleted={id2 => setRisks(prev => prev.filter(x => x.id !== id2))}
            onFeedbackSaved={fb => setCustomerFeedback(fb)}
            onRiskAction={async (riskId, action, payload) => {
              setFeedbackActionLoading(true)
              try {
                let res
                if (action === 'create') res = await api.post(`/api/v1/assignments/${id}/risks`, payload)
                else if (action === 'update') res = await api.put(`/api/v1/assignments/${id}/risks/${riskId}`, payload)
                else if (action === 'delete') { await api.delete(`/api/v1/assignments/${id}/risks/${riskId}`); return null }
                return res?.data?.data ?? res?.data
              } finally { setFeedbackActionLoading(false) }
            }}
            onSaveFeedback={async payload => {
              setFeedbackActionLoading(true)
              try {
                const res = await api.post(`/api/v1/assignments/${id}/feedback`, payload)
                setCustomerFeedback(res.data?.data ?? res.data)
              } finally { setFeedbackActionLoading(false) }
            }}
          />
        )}
      </main>

      {/* ── MODALS ── */}

      {modal === 'checkin' && (
        <Modal title="Check In" onClose={closeModal}>
          <form onSubmit={handleCheckIn} className="flex flex-col gap-3.5">
            <FF label="Notes" value={checkInForm.notes} onChange={v => setCheckInForm(f => ({ ...f, notes: v }))} textarea />
            <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2.5 flex items-center gap-2.5 text-sm">
              {geoStatus === 'loading' && <>
                <svg className="animate-spin h-4 w-4 text-amber-500" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/></svg>
                <span className="text-gray-500">Acquiring location…</span>
              </>}
              {geoStatus === 'ok' && <>
                <svg className="h-4 w-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M17.657 16.657L13.414 20.9a2 2 0 01-2.828 0l-4.243-4.243a8 8 0 1111.314 0z"/><path strokeLinecap="round" strokeLinejoin="round" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
                <span className="text-gray-700 font-medium">Location captured</span>
                <span className="text-gray-400 ml-auto text-xs">{parseFloat(checkInForm.latitude).toFixed(5)}, {parseFloat(checkInForm.longitude).toFixed(5)}</span>
              </>}
              {geoStatus === 'error' && <>
                <svg className="h-4 w-4 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M12 3a9 9 0 100 18A9 9 0 0012 3z"/></svg>
                <span className="text-red-500">Location unavailable — enter manually</span>
              </>}
              {geoStatus === 'idle' && <span className="text-gray-400">Waiting for location…</span>}
            </div>
            {(geoStatus === 'error' || geoStatus === 'ok') && (
              <div className="grid grid-cols-2 gap-3">
                <FF label="Latitude" value={checkInForm.latitude} onChange={v => setCheckInForm(f => ({ ...f, latitude: v }))} type="number" />
                <FF label="Longitude" value={checkInForm.longitude} onChange={v => setCheckInForm(f => ({ ...f, longitude: v }))} type="number" />
              </div>
            )}
            <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
            <MA onCancel={closeModal} submitLabel="Check In" />
          </form>
        </Modal>
      )}

      {modal === 'summary' && (
        <Modal title="Add Daily Summary" onClose={closeModal}>
          <form onSubmit={handleSummary} className="flex flex-col gap-3.5">
            <FF label="Date *" value={summaryForm.date} onChange={v => setSummaryForm(f => ({ ...f, date: v }))} type="date" required />
            <FF label="Summary *" value={summaryForm.summary} onChange={v => setSummaryForm(f => ({ ...f, summary: v }))} textarea required />
            <div className="grid grid-cols-2 gap-3">
              <FF label="Hours Worked" value={summaryForm.hoursWorked} onChange={v => setSummaryForm(f => ({ ...f, hoursWorked: v }))} type="number" />
              <FF label="Expenses (KES)" value={summaryForm.expensesIncurred} onChange={v => setSummaryForm(f => ({ ...f, expensesIncurred: v }))} type="number" />
            </div>
            <FF label="Challenges" value={summaryForm.challenges} onChange={v => setSummaryForm(f => ({ ...f, challenges: v }))} textarea />
            <FF label="Next Day Plan" value={summaryForm.nextDayPlan} onChange={v => setSummaryForm(f => ({ ...f, nextDayPlan: v }))} textarea />
            <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
            <MA onCancel={closeModal} submitLabel="Save Summary" />
          </form>
        </Modal>
      )}

      {modal === 'cancel' && (
        <Modal title="Cancel Assignment" onClose={closeModal}>
          <form onSubmit={handleCancel} className="flex flex-col gap-3.5">
            <FF label="Reason *" value={cancelReason} onChange={setCancelReason} textarea required />
            <MA onCancel={closeModal} submitLabel="Confirm Cancel" submitVariant="red" />
          </form>
        </Modal>
      )}

      {modal === 'req-type-chooser' && (
        <RequisitionTypeChooser
          onChoose={type => { setReqFormType(type); setModal(null) }}
          onClose={closeModal}
        />
      )}

      {reqFormType && (
        <RequisitionFormModal
          formType={reqFormType}
          assignment={assignment}
          onClose={() => setReqFormType(null)}
          onSubmitForm={handleRequisitionForm}
        />
      )}

      {showTravelVoucher && (
        <TravelVoucherModal
          assignment={assignment}s
          onClose={() => setShowTravelVoucher(false)}
          onSubmitForm={handleRequisitionForm}
        />
      )}

      {modal === 'claim' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl w-full max-w-2xl max-h-[92vh] overflow-y-auto shadow-xl">
            <div className="bg-amber-400 px-6 py-3.5 rounded-t-2xl flex justify-between items-center">
              <span className="text-sm font-extrabold text-black tracking-wide">RETURNS / CLAIM VOUCHER</span>
              <button onClick={closeModal} className="text-black text-xl leading-none">&times;</button>
            </div>
            <form onSubmit={handleClaim} className="p-6 flex flex-col gap-3.5">
              <div className="grid grid-cols-2 gap-3">
                <ClaimField label="PCV NO." value={claimForm.pcvNo} onChange={v => setClaimForm(f => ({ ...f, pcvNo: v }))} placeholder="e.g. PCV-001" />
                <ClaimField label="DATE" value={claimForm.date} onChange={v => setClaimForm(f => ({ ...f, date: v }))} type="date" />
              </div>
              <ClaimField label="PAYEE'S NAME / PAY" value={claimForm.payeeName} onChange={v => setClaimForm(f => ({ ...f, payeeName: v }))} placeholder="Full name of payee" required />
              <ClaimField label="ALLOCATION / A/C NAME / PROJECT" value={claimForm.accountName} onChange={v => setClaimForm(f => ({ ...f, accountName: v }))} placeholder="Account or project name" />
              <ClaimField label="BEING AN ADVANCE FOR" value={claimForm.beingAnAdvanceFor} onChange={v => setClaimForm(f => ({ ...f, beingAnAdvanceFor: v }))} textarea placeholder="Describe what this advance is for" />

              <div>
                <p className="text-xs font-bold text-gray-600 uppercase tracking-wider mb-1.5">Particulars</p>
                <div className="border border-gray-200 rounded-lg overflow-hidden">
                  <div className="grid grid-cols-[1fr_100px_60px_32px] bg-gray-50 border-b border-gray-200">
                    {['Description', 'KShs', 'Cts', ''].map(h => (
                      <div key={h} className="px-2.5 py-1.5 text-xs font-bold text-gray-500 uppercase">{h}</div>
                    ))}
                  </div>
                  {claimForm.particulars.map((row, i) => (
                    <div key={i} className={`grid grid-cols-[1fr_100px_60px_32px] ${i < claimForm.particulars.length - 1 ? 'border-b border-gray-100' : ''}`}>
                      <input value={row.description} onChange={e => setClaimForm(f => { const p = [...f.particulars]; p[i] = { ...p[i], description: e.target.value }; return { ...f, particulars: p } })}
                        placeholder="Particulars…" className="border-none px-2.5 py-2 text-sm outline-none border-r border-gray-100" />
                      <input value={row.kshs} onChange={e => setClaimForm(f => { const p = [...f.particulars]; p[i] = { ...p[i], kshs: e.target.value }; return { ...f, particulars: p } })}
                        placeholder="0" type="number" className="border-none px-2.5 py-2 text-sm outline-none text-right border-r border-gray-100" />
                      <input value={row.cts} onChange={e => setClaimForm(f => { const p = [...f.particulars]; p[i] = { ...p[i], cts: e.target.value }; return { ...f, particulars: p } })}
                        placeholder="00" type="number" min="0" max="99" className="border-none px-2.5 py-2 text-sm outline-none text-right border-r border-gray-100" />
                      <button type="button" onClick={() => setClaimForm(f => ({ ...f, particulars: f.particulars.filter((_, j) => j !== i) }))}
                        disabled={claimForm.particulars.length === 1}
                        className="text-red-400 text-base disabled:opacity-30">×</button>
                    </div>
                  ))}
                </div>
                <button type="button" onClick={() => setClaimForm(f => ({ ...f, particulars: [...f.particulars, { description: '', kshs: '', cts: '' }] }))}
                  className="mt-1.5 text-sm text-amber-600 font-semibold">+ Add Row</button>
              </div>

              <ClaimField label="AMOUNT IN WORDS" value={claimForm.amountInWords} onChange={v => setClaimForm(f => ({ ...f, amountInWords: v }))} placeholder="e.g. Five Thousand Shillings Only" />
              <div className="grid grid-cols-[1fr_120px] gap-3 items-end">
                <ClaimField label="TOTAL (KShs) *" value={claimForm.amount} onChange={v => setClaimForm(f => ({ ...f, amount: v }))} type="number" required placeholder="0.00" />
                <ClaimField label="CTS" value={claimForm.totalCts} onChange={v => setClaimForm(f => ({ ...f, totalCts: v }))} type="number" placeholder="00" />
              </div>

              <VoucherSignatories>
                <ClaimField label="PREPARED BY" value={claimForm.preparedBy} onChange={v => setClaimForm(f => ({ ...f, preparedBy: v }))} placeholder="Name" />
                <ClaimField label="CHECKED BY" value={claimForm.checkedBy} onChange={v => setClaimForm(f => ({ ...f, checkedBy: v }))} placeholder="Name" />
                <ClaimField label="APPROVED / AUTHORISED BY (C.A)" value={claimForm.approvedBy} onChange={v => setClaimForm(f => ({ ...f, approvedBy: v }))} placeholder="Name" />
                <ClaimField label="SIGNATURE OF PAYEE / RECEIVED BY" value={claimForm.receivedBy} onChange={v => setClaimForm(f => ({ ...f, receivedBy: v }))} placeholder="Name" />
              </VoucherSignatories>
              <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
              <div className="flex gap-2.5 justify-end mt-1">
                <button type="button" onClick={closeModal} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
                <button type="submit" className="px-5 py-2 rounded-lg bg-amber-400 hover:bg-amber-500 text-black text-sm font-bold transition-colors">Submit Claim</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {modal === 'petty-cash' && (
        <VoucherModal title="PETTY CASH ADVANCE" onClose={closeModal} onSubmit={handlePettyCash} submitLabel="Submit Advance">
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="PCV NO." value={pcForm.pcvNo} onChange={v => setPcForm(f => ({ ...f, pcvNo: v }))} placeholder="e.g. PCA-001" />
            <ClaimField label="DATE" value={pcForm.date} onChange={v => setPcForm(f => ({ ...f, date: v }))} type="date" />
          </div>
          <ClaimField label="PAY (Payee's Name) *" value={pcForm.payeeName} onChange={v => setPcForm(f => ({ ...f, payeeName: v }))} placeholder="Full name of payee" required />
          <ClaimField label="PROJECT / ACCOUNT" value={pcForm.projectAccount} onChange={v => setPcForm(f => ({ ...f, projectAccount: v }))} placeholder="Project or account name" />
          <ClaimField label="BEING AN ADVANCE FOR *" value={pcForm.beingAnAdvanceFor} onChange={v => setPcForm(f => ({ ...f, beingAnAdvanceFor: v }))} textarea placeholder="Describe what this advance is for" required />
          <div className="grid grid-cols-[1fr_160px] gap-3 items-end">
            <ClaimField label="AMOUNT (KShs) *" value={pcForm.sum} onChange={v => setPcForm(f => ({ ...f, sum: v }))} type="number" required placeholder="0.00" />
            <ClaimField label="APPROVED BY (KShs)" value={pcForm.approvedBy} onChange={v => setPcForm(f => ({ ...f, approvedBy: v }))} placeholder="Name" />
          </div>
          <ClaimField label="THE SUM OF KShs (Amount in Words)" value={pcForm.amountInWords} onChange={v => setPcForm(f => ({ ...f, amountInWords: v }))} placeholder="e.g. Five Thousand Shillings Only" />
          <VoucherSignatories>
            <ClaimField label="PREPARED BY" value={pcForm.preparedBy} onChange={v => setPcForm(f => ({ ...f, preparedBy: v }))} placeholder="Name" />
            <ClaimField label="AUTHORISED BY" value={pcForm.authorisedBy} onChange={v => setPcForm(f => ({ ...f, authorisedBy: v }))} placeholder="Name" />
            <ClaimField label="CHECKED BY" value={pcForm.checkedBy} onChange={v => setPcForm(f => ({ ...f, checkedBy: v }))} placeholder="Name" />
            <ClaimField label="RECEIVED BY" value={pcForm.receivedBy} onChange={v => setPcForm(f => ({ ...f, receivedBy: v }))} placeholder="Name" />
            <ClaimField label="DATE RECEIVED" value={pcForm.receivedDate} onChange={v => setPcForm(f => ({ ...f, receivedDate: v }))} type="date" />
          </VoucherSignatories>
          <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
        </VoucherModal>
      )}

      {modal === 'per-diem' && (
        <VoucherModal title="PER DIEM RETURN" onClose={closeModal} onSubmit={handlePerDiem} submitLabel="Submit Per Diem">
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="REF NO." value={pdForm.refNo} onChange={v => setPdForm(f => ({ ...f, refNo: v }))} placeholder="e.g. PD-001" />
            <ClaimField label="DATE" value={pdForm.date} onChange={v => setPdForm(f => ({ ...f, date: v }))} type="date" />
          </div>
          <ClaimField label="EMPLOYEE NAME *" value={pdForm.employeeName} onChange={v => setPdForm(f => ({ ...f, employeeName: v }))} placeholder="Full name" required />
          <ClaimField label="PROJECT / ACCOUNT" value={pdForm.projectAccount} onChange={v => setPdForm(f => ({ ...f, projectAccount: v }))} placeholder="Project or account name" />
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="PERIOD FROM" value={pdForm.periodFrom} onChange={v => setPdForm(f => ({ ...f, periodFrom: v }))} type="date" />
            <ClaimField label="PERIOD TO" value={pdForm.periodTo} onChange={v => setPdForm(f => ({ ...f, periodTo: v }))} type="date" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="NO. OF DAYS" value={pdForm.daysCount} onChange={v => setPdForm(f => ({ ...f, daysCount: v }))} type="number" placeholder="0" />
            <ClaimField label="DAILY RATE (KShs)" value={pdForm.perDiemRate} onChange={v => setPdForm(f => ({ ...f, perDiemRate: v }))} type="number" placeholder="0.00" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="TOTAL ADVANCED (KShs) *" value={pdForm.totalAdvanced} onChange={v => setPdForm(f => ({ ...f, totalAdvanced: v }))} type="number" required placeholder="0.00" />
            <ClaimField label="TOTAL SPENT (KShs) *" value={pdForm.totalSpent} onChange={v => setPdForm(f => ({ ...f, totalSpent: v }))} type="number" required placeholder="0.00" />
          </div>
          {(parseFloat(pdForm.totalAdvanced) > 0 || parseFloat(pdForm.totalSpent) > 0) && (
            <div className="bg-gray-50 rounded-lg px-3.5 py-2.5 text-sm text-gray-700">
              Balance: <strong className={(parseFloat(pdForm.totalAdvanced) - parseFloat(pdForm.totalSpent)) >= 0 ? 'text-green-600' : 'text-red-500'}>
                KShs {(parseFloat(pdForm.totalAdvanced) - parseFloat(pdForm.totalSpent)).toLocaleString('en-KE')}
              </strong>
            </div>
          )}
          <ClaimField label="NOTES / DETAILS" value={pdForm.notes} onChange={v => setPdForm(f => ({ ...f, notes: v }))} textarea placeholder="Additional notes" />
          <VoucherSignatories>
            <ClaimField label="PREPARED BY" value={pdForm.preparedBy} onChange={v => setPdForm(f => ({ ...f, preparedBy: v }))} placeholder="Name" />
            <ClaimField label="CHECKED BY" value={pdForm.checkedBy} onChange={v => setPdForm(f => ({ ...f, checkedBy: v }))} placeholder="Name" />
            <ClaimField label="APPROVED BY" value={pdForm.approvedBy} onChange={v => setPdForm(f => ({ ...f, approvedBy: v }))} placeholder="Name" />
            <ClaimField label="EMPLOYEE SIGNATURE" value={pdForm.signedBy} onChange={v => setPdForm(f => ({ ...f, signedBy: v }))} placeholder="Name" />
          </VoucherSignatories>
          <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
        </VoucherModal>
      )}

      {modal === 'advance-return' && (
        <VoucherModal title="ADVANCE RETURN" onClose={closeModal} onSubmit={handleAdvanceReturn} submitLabel="Submit Return">
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="REF NO." value={arForm.refNo} onChange={v => setArForm(f => ({ ...f, refNo: v }))} placeholder="e.g. AR-001" />
            <ClaimField label="DATE" value={arForm.date} onChange={v => setArForm(f => ({ ...f, date: v }))} type="date" />
          </div>
          <ClaimField label="RETURNED BY *" value={arForm.returnedBy} onChange={v => setArForm(f => ({ ...f, returnedBy: v }))} placeholder="Full name of person returning funds" required />
          <ClaimField label="PROJECT / ACCOUNT" value={arForm.projectAccount} onChange={v => setArForm(f => ({ ...f, projectAccount: v }))} placeholder="Project or account name" />
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="TOTAL ADVANCED (KShs) *" value={arForm.totalAdvanced} onChange={v => setArForm(f => ({ ...f, totalAdvanced: v }))} type="number" required placeholder="0.00" />
            <ClaimField label="AMOUNT RETURNED (KShs) *" value={arForm.amountReturned} onChange={v => setArForm(f => ({ ...f, amountReturned: v }))} type="number" required placeholder="0.00" />
          </div>
          {(parseFloat(arForm.totalAdvanced) > 0 || parseFloat(arForm.amountReturned) > 0) && (
            <div className="bg-gray-50 rounded-lg px-3.5 py-2.5 text-sm text-gray-700">
              Balance outstanding: <strong className={(parseFloat(arForm.totalAdvanced) - parseFloat(arForm.amountReturned)) > 0 ? 'text-red-500' : 'text-green-600'}>
                KShs {(parseFloat(arForm.totalAdvanced) - parseFloat(arForm.amountReturned)).toLocaleString('en-KE')}
              </strong>
            </div>
          )}
          <ClaimField label="AMOUNT IN WORDS" value={arForm.amountInWords} onChange={v => setArForm(f => ({ ...f, amountInWords: v }))} placeholder="e.g. Five Thousand Shillings Only" />
          <ClaimField label="NOTES" value={arForm.notes} onChange={v => setArForm(f => ({ ...f, notes: v }))} textarea placeholder="Additional notes" />
          <VoucherSignatories>
            <ClaimField label="PREPARED BY" value={arForm.preparedBy} onChange={v => setArForm(f => ({ ...f, preparedBy: v }))} placeholder="Name" />
            <ClaimField label="CHECKED BY" value={arForm.checkedBy} onChange={v => setArForm(f => ({ ...f, checkedBy: v }))} placeholder="Name" />
            <ClaimField label="APPROVED BY" value={arForm.approvedBy} onChange={v => setArForm(f => ({ ...f, approvedBy: v }))} placeholder="Name" />
            <ClaimField label="RECEIVED BY" value={arForm.receivedBy} onChange={v => setArForm(f => ({ ...f, receivedBy: v }))} placeholder="Name" />
          </VoucherSignatories>
          <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
        </VoucherModal>
      )}

      {modal === 'refund' && (
        <VoucherModal title="REFUND REQUEST" onClose={closeModal} onSubmit={handleRefund} submitLabel="Submit Refund">
          <div className="grid grid-cols-2 gap-3">
            <ClaimField label="REF NO." value={refundForm.refNo} onChange={v => setRefundForm(f => ({ ...f, refNo: v }))} placeholder="e.g. RF-001" />
            <ClaimField label="DATE" value={refundForm.date} onChange={v => setRefundForm(f => ({ ...f, date: v }))} type="date" />
          </div>
          <ClaimField label="PAYEE'S NAME *" value={refundForm.payeeName} onChange={v => setRefundForm(f => ({ ...f, payeeName: v }))} placeholder="Full name of payee" required />
          <ClaimField label="PROJECT / ACCOUNT" value={refundForm.projectAccount} onChange={v => setRefundForm(f => ({ ...f, projectAccount: v }))} placeholder="Project or account name" />
          <ClaimField label="REASON FOR REFUND *" value={refundForm.reason} onChange={v => setRefundForm(f => ({ ...f, reason: v }))} textarea required placeholder="Describe the reason for this refund" />
          <div className="grid grid-cols-2 gap-3 items-end">
            <ClaimField label="AMOUNT (KShs) *" value={refundForm.amount} onChange={v => setRefundForm(f => ({ ...f, amount: v }))} type="number" required placeholder="0.00" />
            <ClaimField label="AMOUNT IN WORDS" value={refundForm.amountInWords} onChange={v => setRefundForm(f => ({ ...f, amountInWords: v }))} placeholder="e.g. One Thousand Only" />
          </div>
          <VoucherSignatories>
            <ClaimField label="PREPARED BY" value={refundForm.preparedBy} onChange={v => setRefundForm(f => ({ ...f, preparedBy: v }))} placeholder="Name" />
            <ClaimField label="CHECKED BY" value={refundForm.checkedBy} onChange={v => setRefundForm(f => ({ ...f, checkedBy: v }))} placeholder="Name" />
            <ClaimField label="APPROVED BY" value={refundForm.approvedBy} onChange={v => setRefundForm(f => ({ ...f, approvedBy: v }))} placeholder="Name" />
            <ClaimField label="RECEIVED BY" value={refundForm.receivedBy} onChange={v => setRefundForm(f => ({ ...f, receivedBy: v }))} placeholder="Name" />
          </VoucherSignatories>
          <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
        </VoucherModal>
      )}

      {reviewItem && (
        <Modal title="Review Financial Request" onClose={() => { setReviewItem(null); setReviewComment('') }}>
          <div className="flex flex-col gap-3.5">
            <p className="text-sm text-gray-700"><strong>{reviewItem.label}</strong></p>
            <FF label="Comments / Notes" value={reviewComment} onChange={setReviewComment} textarea />
            <div className="flex gap-2.5 justify-end mt-1">
              <button type="button" onClick={() => { setReviewItem(null); setReviewComment('') }}
                className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
              <button type="button" onClick={() => handleReview(false)}
                className="px-4 py-2 rounded-lg bg-red-500 hover:bg-red-600 text-white text-sm font-semibold transition-colors">Reject</button>
              <button type="button" onClick={() => handleReview(true)}
                className="px-4 py-2 rounded-lg bg-green-500 hover:bg-green-600 text-white text-sm font-semibold transition-colors">Approve</button>
            </div>
          </div>
        </Modal>
      )}

      {showLinkModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <h2 className="text-base font-bold text-gray-900 mb-5">Link to Project</h2>
            <form onSubmit={handleLinkToProject} className="flex flex-col gap-3.5">
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Project *</label>
                <select required value={linkForm.projectId} onChange={e => onProjectPicked(e.target.value)} className="input">
                  <option value="">— Select project —</option>
                  {projects.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              {milestones.length > 0 && (
                <div>
                  <label className="text-xs font-semibold text-gray-600 block mb-1">Milestone (optional)</label>
                  <select value={linkForm.milestoneId} onChange={e => setLinkForm(f => ({ ...f, milestoneId: e.target.value }))} className="input">
                    <option value="">— None —</option>
                    {milestones.map(m => <option key={m.id} value={m.id}>{m.name ?? m.title}</option>)}
                  </select>
                </div>
              )}
              <p className="text-sm text-gray-500">A task will be auto-created from this assignment and linked to the selected project.</p>
              <div className="flex gap-2.5 justify-end mt-2">
                <button type="button" onClick={() => { setShowLinkModal(false); setMilestones([]) }}
                  className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
                <button type="submit" disabled={linkLoading}
                  className="px-4 py-2 rounded-lg bg-gray-900 hover:bg-gray-800 text-white text-sm font-semibold transition-colors disabled:opacity-70">
                  {linkLoading ? 'Linking…' : 'Link to Project'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showArchiveModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <h2 className="text-base font-bold text-gray-900 mb-5">Archive Assignment</h2>
            <form onSubmit={handleArchive} className="flex flex-col gap-3.5">
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Reason *</label>
                <textarea required rows={3} value={archiveReason} onChange={e => setArchiveReason(e.target.value)}
                  placeholder="Why is this assignment being archived without linking to a project?"
                  className="input resize-vertical" />
              </div>
              <div className="flex gap-2.5 justify-end">
                <button type="button" onClick={() => setShowArchiveModal(false)}
                  className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
                <button type="submit" disabled={linkLoading}
                  className="px-4 py-2 rounded-lg bg-gray-400 hover:bg-gray-500 text-white text-sm font-semibold transition-colors disabled:opacity-70">
                  {linkLoading ? 'Archiving…' : 'Archive'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Dispatch Modal ── */}
      {showDispatchModal && (
        <VModal title="Request Vehicle" onClose={() => setShowDispatchModal(false)}>
          <form onSubmit={handleCreateDispatch} className="flex flex-col gap-3.5">
            <p className="text-xs text-gray-500 -mt-1">This sends a request to the fleet manager — the vehicle isn't marked in use until it's approved.</p>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Vehicle *</label>
              <select required value={dispatchForm.fieldVehicleId} onChange={e => setDispatchForm(f => ({ ...f, fieldVehicleId: e.target.value }))} className="input">
                <option value="">Select vehicle…</option>
                {availableVehicles.map(v => (
                  <option key={v.id} value={v.id}>{v.registrationNumber} — {v.make} {v.model} ({v.type})</option>
                ))}
              </select>
              {availableVehicles.length === 0 && <p className="text-xs text-amber-600 mt-1">No available vehicles. Add vehicles in Field Vehicles.</p>}
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Driver Name *</label>
              <input required value={dispatchForm.driverName} onChange={e => setDispatchForm(f => ({ ...f, driverName: e.target.value }))} className="input" placeholder="Name of driver" />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Departure Date *</label>
              <input required type="date" value={dispatchForm.departureDatetime} onChange={e => setDispatchForm(f => ({ ...f, departureDatetime: e.target.value }))} className="input" />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Fuel Level Out *</label>
              <select value={dispatchForm.fuelLevelOut} onChange={e => setDispatchForm(f => ({ ...f, fuelLevelOut: e.target.value }))} className="input">
                {['Full', '3/4', '1/2', '1/4', 'Empty'].map(l => <option key={l}>{l}</option>)}
              </select>
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Notes</label>
              <textarea rows={2} value={dispatchForm.notes} onChange={e => setDispatchForm(f => ({ ...f, notes: e.target.value }))} className="input resize-none" />
            </div>
            <div className="flex gap-2.5 justify-end">
              <button type="button" onClick={() => setShowDispatchModal(false)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
              <button type="submit" disabled={dispatchSaving} className="px-5 py-2 rounded-lg bg-blue-500 hover:bg-blue-600 text-white text-sm font-semibold disabled:opacity-70">
                {dispatchSaving ? 'Requesting…' : 'Request Vehicle'}
              </button>
            </div>
          </form>
        </VModal>
      )}

      {/* ── Return Modal ── */}
      {returnTarget && (
        <VModal title={`Log Return — ${returnTarget.vehicleRegistration}`} onClose={() => setReturnTarget(null)}>
          <form onSubmit={handleLogReturn} className="flex flex-col gap-3.5">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Return Date/Time *</label>
                <input required type="datetime-local" value={returnForm.returnDatetime} onChange={e => setReturnForm(f => ({ ...f, returnDatetime: e.target.value }))} className="input" />
              </div>
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Return Odometer (km) *</label>
                <input required type="number" min={returnTarget.departureOdometer ?? 0} value={returnForm.returnOdometer} onChange={e => setReturnForm(f => ({ ...f, returnOdometer: e.target.value }))} className="input" />
                {returnTarget.departureOdometer != null && (
                  <p className="text-xs text-gray-400 mt-1">Departure was {Number(returnTarget.departureOdometer).toLocaleString()} km</p>
                )}
              </div>
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Fuel Level In *</label>
              <select value={returnForm.fuelLevelIn} onChange={e => setReturnForm(f => ({ ...f, fuelLevelIn: e.target.value }))} className="input">
                {['Full', '3/4', '1/2', '1/4', 'Empty'].map(l => <option key={l}>{l}</option>)}
              </select>
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Notes</label>
              <textarea rows={2} value={returnForm.notes} onChange={e => setReturnForm(f => ({ ...f, notes: e.target.value }))} className="input resize-none" />
            </div>
            <div className="flex gap-2.5 justify-end">
              <button type="button" onClick={() => setReturnTarget(null)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
              <button type="submit" disabled={returnSaving} className="px-5 py-2 rounded-lg bg-green-500 hover:bg-green-600 text-white text-sm font-semibold disabled:opacity-70">
                {returnSaving ? 'Saving…' : 'Log Return'}
              </button>
            </div>
          </form>
        </VModal>
      )}

      {/* ── Fuel Log Modal ── */}
      {fuelTarget && (
        <VModal title={`Add Fuel Log — ${fuelTarget.vehicleRegistration}`} onClose={() => setFuelTarget(null)}>
          <form onSubmit={handleAddFuelLog} className="flex flex-col gap-3.5">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Litres *</label>
                <input required type="number" step="0.01" min="0" value={fuelForm.amountLitres} onChange={e => setFuelForm(f => ({ ...f, amountLitres: e.target.value }))} className="input" />
              </div>
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Cost (KES) *</label>
                <input required type="number" step="0.01" min="0" value={fuelForm.costKes} onChange={e => setFuelForm(f => ({ ...f, costKes: e.target.value }))} className="input" />
              </div>
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Location</label>
              <input value={fuelForm.location} onChange={e => setFuelForm(f => ({ ...f, location: e.target.value }))} className="input" placeholder="e.g. Total Petrol Station, Mombasa Rd" />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 block mb-1">Notes</label>
              <textarea rows={2} value={fuelForm.notes} onChange={e => setFuelForm(f => ({ ...f, notes: e.target.value }))} className="input resize-none" />
            </div>
            <div className="flex gap-2.5 justify-end">
              <button type="button" onClick={() => setFuelTarget(null)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
              <button type="submit" disabled={fuelSaving} className="px-5 py-2 rounded-lg bg-amber-400 hover:bg-amber-500 text-black text-sm font-semibold disabled:opacity-70">
                {fuelSaving ? 'Saving…' : 'Add Fuel Log'}
              </button>
            </div>
          </form>
        </VModal>
      )}

      <SignatureModal
        isOpen={signModalOpen}
        onClose={() => setSignModalOpen(false)}
        onSubmit={handleSignSubmit}
        loading={signLoading}
      />

      {lightboxPhoto && (
        <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4"
          onClick={() => setLightboxPhoto(null)}>
          <div className="relative max-w-3xl w-full" onClick={e => e.stopPropagation()}>
            <button onClick={() => setLightboxPhoto(null)}
              className="absolute -top-8 right-0 text-white text-2xl leading-none hover:text-amber-400">&times;</button>
            <img src={lightboxPhoto.src} alt={lightboxPhoto.caption ?? 'Photo'}
              className="w-full rounded-xl max-h-[80vh] object-contain" />
            {lightboxPhoto.caption && (
              <p className="text-center text-white/80 text-sm mt-2">{lightboxPhoto.caption}</p>
            )}
          </div>
        </div>
      )}
    </>
  )
}

// ── Shared helpers ────────────────────────────────────────────────────────────

function ExportButtons({ onPDF, onExcel }) {
  return (
    <>
      <button onClick={onPDF} className="px-3 py-1.5 text-sm font-semibold text-gray-700 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">↓ PDF</button>
      <button onClick={onExcel} className="px-3 py-1.5 text-sm font-semibold text-gray-700 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">↓ Excel</button>
    </>
  )
}

function Btn({ label, variant = 'amber', onClick, loading, small }) {
  const variants = {
    green:  'bg-green-500 hover:bg-green-600 text-white',
    blue:   'bg-blue-500 hover:bg-blue-600 text-white',
    indigo: 'bg-indigo-500 hover:bg-indigo-600 text-white',
    amber:  'bg-amber-400 hover:bg-amber-500 text-black',
    orange: 'bg-orange-500 hover:bg-orange-600 text-white',
    purple: 'bg-purple-500 hover:bg-purple-600 text-white',
    dark:   'bg-gray-900 hover:bg-gray-800 text-white',
    gray:   'bg-gray-400 hover:bg-gray-500 text-white',
    red:    'bg-red-500 hover:bg-red-600 text-white',
    slate:  'bg-gray-600 hover:bg-gray-700 text-white',
  }
  return (
    <button onClick={onClick} disabled={loading}
      className={`${variants[variant] ?? variants.amber} ${small ? 'px-2.5 py-1 text-xs' : 'px-3.5 py-2 text-sm'} font-semibold rounded-lg transition-colors disabled:opacity-70`}>
      {loading ? '…' : label}
    </button>
  )
}

function FinBadge({ status }) {
  return (
    <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${FIN_STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-500'}`}>
      {status}
    </span>
  )
}

function TabBar({ tabs, active, onChange, badges = {} }) {
  return (
    <div className="flex gap-0 border-b-2 border-gray-100 mb-6 overflow-x-auto">
      {tabs.map(t => (
        <button key={t.id} onClick={() => onChange(t.id)}
          className={`px-4 py-2.5 text-sm whitespace-nowrap border-b-2 -mb-0.5 transition-colors ${active === t.id ? 'font-bold text-amber-600 border-amber-500' : 'font-medium text-gray-500 border-transparent hover:text-gray-700'}`}>
          {t.label}{badges[t.id] ? ` (${badges[t.id]})` : ''}
        </button>
      ))}
    </div>
  )
}

function InfoCard({ title, children }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5">
      <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wider mb-4">{title}</h3>
      <div className="flex flex-col gap-2">{children}</div>
    </div>
  )
}

function InfoRow({ label, value }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-sm text-gray-500 shrink-0">{label}</span>
      <span className="text-sm text-gray-900 font-medium text-right">{value}</span>
    </div>
  )
}

function StatCard({ label, value, sub, color }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <p className={`text-lg font-bold ${color}`}>{value}</p>
      <p className="text-sm text-gray-500 mt-0.5">{label}</p>
      {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
    </div>
  )
}

function SummarySection({ label, text, color = 'text-gray-700' }) {
  if (!text) return null
  return (
    <div className="mb-2">
      <p className={`text-xs font-bold mb-0.5 ${color}`}>{label}</p>
      <p className="text-sm text-gray-900 leading-relaxed">{text}</p>
    </div>
  )
}

function Empty({ message }) {
  return <div className="py-8 text-center text-sm text-gray-400">{message}</div>
}

function FinSection({ items, onAdd, addLabel, columns, renderRow, emptyMsg, rowActions }) {
  return (
    <div>
      {onAdd && (
        <div className="flex justify-end mb-3">
          <Btn label={addLabel} variant="amber" onClick={onAdd} />
        </div>
      )}
      {items.length === 0 ? <Empty message={emptyMsg} /> : (
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <table className="w-full border-collapse">
            <thead>
              <tr className="bg-gray-50">
                {columns.map(h => <th key={h} className="px-4 py-2.5 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">{h}</th>)}
                {rowActions && <th className="px-4 py-2.5 text-xs font-bold text-gray-500 uppercase tracking-wider"></th>}
              </tr>
            </thead>
            <tbody>
              {items.map((item, i) => (
                <tr key={item.id ?? i} className="border-t border-gray-100">
                  {renderRow(item).map((cell, j) => <td key={j} className="px-4 py-2.5 text-sm text-gray-700">{cell}</td>)}
                  {rowActions && <td className="px-4 py-2 whitespace-nowrap">{rowActions(item)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function Modal({ title, onClose, children, wide }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className={`bg-white rounded-2xl shadow-xl w-full ${wide ? 'max-w-xl' : 'max-w-md'} max-h-[90vh] overflow-y-auto p-6`}>
        <div className="flex justify-between items-center mb-5">
          <h2 className="text-base font-bold text-gray-900">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>
        {children}
      </div>
    </div>
  )
}

function MA({ onCancel, submitLabel, submitVariant = 'amber' }) {
  const variants = {
    amber: 'bg-amber-400 hover:bg-amber-500 text-black',
    red: 'bg-red-500 hover:bg-red-600 text-white',
    green: 'bg-green-500 hover:bg-green-600 text-white',
  }
  return (
    <div className="flex gap-2.5 mt-2 justify-end">
      <button type="button" onClick={onCancel} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
      <button type="submit" className={`px-4 py-2 rounded-lg text-sm font-semibold transition-colors ${variants[submitVariant] ?? variants.amber}`}>{submitLabel}</button>
    </div>
  )
}

function FF({ label, value, onChange, required, type = 'text', textarea }) {
  return (
    <div>
      <label className="text-xs font-semibold text-gray-600 block mb-1">{label}</label>
      {textarea
        ? <textarea value={value} onChange={e => onChange(e.target.value)} rows={3} className="input resize-y" required={required} />
        : <input type={type} value={value} onChange={e => onChange(e.target.value)} required={required} className="input" />}
    </div>
  )
}

function VoucherModal({ title, onClose, onSubmit, submitLabel, children }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl w-full max-w-xl max-h-[92vh] overflow-y-auto shadow-xl">
        <div className="bg-amber-400 px-6 py-3.5 rounded-t-2xl flex justify-between items-center">
          <span className="text-sm font-extrabold text-black tracking-wide">{title}</span>
          <button onClick={onClose} className="text-black text-xl leading-none">&times;</button>
        </div>
        <form onSubmit={onSubmit} className="p-6 flex flex-col gap-3.5">
          {children}
          <div className="flex gap-2.5 justify-end mt-1">
            <button type="button" onClick={onClose} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
            <button type="submit" className="px-5 py-2 rounded-lg bg-amber-400 hover:bg-amber-500 text-black text-sm font-bold transition-colors">{submitLabel}</button>
          </div>
        </form>
      </div>
    </div>
  )
}

function VoucherSignatories({ children }) {
  return (
    <div className="border-t border-dashed border-gray-200 pt-3.5">
      <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-2.5">Signatories</p>
      <div className="grid grid-cols-2 gap-3">{children}</div>
    </div>
  )
}

function ClaimField({ label, value, onChange, required, type = 'text', textarea, placeholder }) {
  return (
    <div>
      <label className="text-xs font-bold text-gray-500 uppercase tracking-wider block mb-1">{label}{required && ' *'}</label>
      {textarea
        ? <textarea value={value} onChange={e => onChange(e.target.value)} rows={2} placeholder={placeholder}
            className="w-full px-2.5 py-1.5 rounded-md border border-gray-200 text-sm outline-none bg-gray-50 focus:ring-2 focus:ring-amber-400 focus:border-transparent resize-y" required={required} />
        : <input type={type} value={value} onChange={e => onChange(e.target.value)} required={required} placeholder={placeholder}
            className="w-full px-2.5 py-1.5 rounded-md border border-gray-200 text-sm outline-none bg-gray-50 focus:ring-2 focus:ring-amber-400 focus:border-transparent" />}
    </div>
  )
}

// ── Vehicle Dispatch Components ────────────────────────────────────────────────

const DISPATCH_STATUS_BADGE = {
  Pending: 'bg-amber-100 text-amber-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  Returned: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-600',
  Cancelled: 'bg-gray-100 text-gray-500',
}

function VModal({ title, onClose, children }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md max-h-[92vh] overflow-y-auto">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-base font-bold text-gray-800">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>
        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  )
}

function VehiclesTab({ dispatches, loading, canAct, canApprove, availableVehicles, onAddDispatch, onLogReturn, onAddFuel, onCancel, onApprove, onReject }) {
  if (loading) return <div className="flex items-center justify-center py-16 text-gray-400 text-sm">Loading…</div>

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-gray-500">{dispatches.length} request{dispatches.length !== 1 ? 's' : ''} for this assignment</p>
        {canAct && (
          <button onClick={onAddDispatch}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-blue-500 hover:bg-blue-600 text-white text-sm font-semibold transition-colors">
            + Request Vehicle
          </button>
        )}
      </div>

      {dispatches.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <div className="text-4xl mb-2">🚗</div>
          <p className="font-medium">No vehicles requested yet</p>
          {canAct && <p className="text-sm mt-1">Use "Request Vehicle" to ask for a field vehicle for this assignment.</p>}
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          {dispatches.map(d => (
            <div key={d.id} className="bg-white border border-gray-200 rounded-xl p-5">
              <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3 mb-4">
                <div>
                  <div className="flex items-center gap-2 mb-0.5">
                    <span className="font-bold text-gray-800 font-mono">{d.vehicleRegistration}</span>
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${DISPATCH_STATUS_BADGE[d.status] ?? 'bg-gray-100 text-gray-500'}`}>{d.status}</span>
                  </div>
                  <p className="text-sm text-gray-500">{d.vehicleMake} {d.vehicleModel} · {d.vehicleType}</p>
                </div>
                {d.status === 'Pending' && (canApprove || canAct) && (
                  <div className="flex items-center gap-2 shrink-0">
                    {canApprove && (
                      <>
                        <button onClick={() => onApprove(d.id)}
                          className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-green-500 hover:bg-green-600 text-white transition-colors">
                          Approve
                        </button>
                        <button onClick={() => onReject(d.id)}
                          className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-100 hover:bg-red-200 text-red-700 transition-colors">
                          Reject
                        </button>
                      </>
                    )}
                    <button onClick={() => onCancel(d.id)}
                      className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-600 transition-colors">
                      Withdraw
                    </button>
                  </div>
                )}
                {canAct && d.status === 'Dispatched' && (
                  <div className="flex items-center gap-2 shrink-0">
                    <button onClick={() => onLogReturn(d)}
                      className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-green-500 hover:bg-green-600 text-white transition-colors">
                      Log Return
                    </button>
                    <button onClick={() => onAddFuel(d)}
                      className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-400 hover:bg-amber-500 text-black transition-colors">
                      + Fuel
                    </button>
                    <button onClick={() => onCancel(d.id)}
                      className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-600 transition-colors">
                      Cancel
                    </button>
                  </div>
                )}
                {canAct && d.status === 'Returned' && (
                  <button onClick={() => onAddFuel(d)}
                    className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-400 hover:bg-amber-500 text-black transition-colors shrink-0">
                    + Fuel Log
                  </button>
                )}
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm mb-4">
                <div>
                  <p className="text-xs text-gray-400 font-medium mb-0.5">Driver</p>
                  <p className="text-gray-800 font-medium">{d.driverName}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 font-medium mb-0.5">Departed</p>
                  <p className="text-gray-800">{new Date(d.departureDatetime).toLocaleString()}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 font-medium mb-0.5">Departure Odo</p>
                  <p className="text-gray-800">{Number(d.departureOdometer).toLocaleString()} km</p>
                </div>
                <div>
                  <p className="text-xs text-gray-400 font-medium mb-0.5">Fuel Out</p>
                  <p className="text-gray-800">{d.fuelLevelOut}</p>
                </div>
                {d.returnDatetime && (
                  <>
                    <div>
                      <p className="text-xs text-gray-400 font-medium mb-0.5">Returned</p>
                      <p className="text-gray-800">{new Date(d.returnDatetime).toLocaleString()}</p>
                    </div>
                    <div>
                      <p className="text-xs text-gray-400 font-medium mb-0.5">Return Odo</p>
                      <p className="text-gray-800">{Number(d.returnOdometer).toLocaleString()} km</p>
                    </div>
                    <div>
                      <p className="text-xs text-gray-400 font-medium mb-0.5">Distance</p>
                      <p className="text-gray-800 font-semibold">{(Number(d.returnOdometer) - Number(d.departureOdometer)).toLocaleString()} km</p>
                    </div>
                    <div>
                      <p className="text-xs text-gray-400 font-medium mb-0.5">Fuel In</p>
                      <p className="text-gray-800">{d.fuelLevelIn}</p>
                    </div>
                  </>
                )}
              </div>

              {/* Fuel logs */}
              {d.fuelLogs && d.fuelLogs.length > 0 && (
                <div className="border-t border-gray-100 pt-3">
                  <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Fuel Logs</p>
                  <div className="flex flex-col gap-1.5">
                    {d.fuelLogs.map(f => (
                      <div key={f.id} className="flex items-center gap-4 text-xs text-gray-600 bg-gray-50 rounded-lg px-3 py-2">
                        <span className="font-semibold text-gray-800">{f.amountLitres}L</span>
                        <span>KES {Number(f.costKes).toLocaleString()}</span>
                        {f.location && <span className="text-gray-400">{f.location}</span>}
                        <span className="text-gray-400 ml-auto">{new Date(f.loggedAt).toLocaleString()}</span>
                      </div>
                    ))}
                    <div className="flex justify-end text-xs font-semibold text-gray-700 px-3 py-1">
                      Total fuel: {d.fuelLogs.reduce((s, f) => s + Number(f.amountLitres), 0).toFixed(1)}L
                      · KES {d.fuelLogs.reduce((s, f) => s + Number(f.costKes), 0).toLocaleString()}
                    </div>
                  </div>
                </div>
              )}

              {d.notes && <p className="text-xs text-gray-400 mt-2 italic">{d.notes}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

const LWO_STAGES = [
  { key: 'Pending',           label: 'Pending',           desc: 'Awaiting intake' },
  { key: 'IntakeComplete',    label: 'Intake',            desc: 'Instruments received' },
  { key: 'InBench',           label: 'In Bench',          desc: 'Calibration in progress' },
  { key: 'AwaitingTmReview',  label: 'TM Review',         desc: 'Awaiting approval' },
  { key: 'CertificateIssued', label: 'Certificate',       desc: 'Certificate issued' },
  { key: 'Dispatched',        label: 'Dispatched',        desc: 'Returned to client' },
]

const BENCH_CHECKLIST_ITEMS = [
  'Instrument identification verified (serial/tag/model)',
  'Environmental conditions recorded (temp, humidity within limits)',
  'Reference/working standards identified and within calibration validity',
  'Calibration procedure referenced and followed',
  'Measurement uncertainty evaluated and documented',
  'All calibration data recorded in workbook / data sheet',
  'Calibration label / sticker applied to instrument',
  'Certificate / report prepared and reviewed',
  'Customer-specific requirements addressed',
  'Any deviations or non-conformances noted',
]

const initBenchChecklist = () => BENCH_CHECKLIST_ITEMS.map(item => ({ item, answer: '', notes: '' }))

function LabWorkOrderTab({ assignmentId, assignment, labWorkOrder: lwo, loading, actionLoading, dsActionLoading, canAct,
  intakeForm, setIntakeForm, benchForm, setBenchForm, certForm, setCertForm,
  dispatchForm, setDispatchForm, onAction, onDataSheet, onInit, isInLab, isMassCalibration, onRecalculate, docPrefix }) {

  const [benchChecklist, setBenchChecklist] = useState(initBenchChecklist)
  const [photoFiles, setPhotoFiles] = useState([])
  const [photoUploading, setPhotoUploading] = useState(false)
  const [tmRejectReason, setTmRejectReason] = useState('')
  const [tmCertForm, setTmCertForm] = useState({ certificateNumber: '', jobNumber: '', tmName: '', notes: '' })

  const stageIdx = lwo ? LWO_STAGES.findIndex(s => s.key === lwo.status) : -1

  const uploadDataSheets = async (lwoId) => {
    if (!photoFiles.length) return
    setPhotoUploading(true)
    for (const file of photoFiles) {
      const fd = new FormData()
      fd.append('file', file)
      fd.append('entityType', 'LabWorkOrder')
      fd.append('entityId', lwoId)
      fd.append('assignmentId', assignmentId)
      await api.post('/api/v1/attachments', fd).catch(() => {})
    }
    setPhotoFiles([])
    setPhotoUploading(false)
  }

  if (loading) return <div className="p-10 text-sm text-gray-400 text-center">Loading lab work order…</div>

  if (!lwo) return (
    <div className="flex flex-col items-center justify-center py-16 gap-4">
      <div className="text-5xl">🔬</div>
      <p className="text-gray-500 text-sm">No lab work order yet for this assignment.</p>
      {canAct && (
        <button onClick={onInit} disabled={actionLoading}
          className="px-5 py-2 rounded-lg bg-purple-600 hover:bg-purple-700 text-white text-sm font-semibold transition-colors disabled:opacity-70">
          {actionLoading ? 'Creating…' : 'Initialise Lab Work Order'}
        </button>
      )}
    </div>
  )

  const prevBenchChecklist = (() => {
    if (!lwo.benchChecklistJson) return null
    try { return JSON.parse(lwo.benchChecklistJson) } catch { return null }
  })()

  return (
    <div className="space-y-6">
      {/* TM rejection banner */}
      {lwo.tmRejectionReason && lwo.status === 'InBench' && (
        <div className="bg-red-50 border border-red-200 rounded-xl px-5 py-3">
          <p className="text-sm font-semibold text-red-700">TM Sent Back for Rework</p>
          <p className="text-sm text-red-600 mt-0.5">{lwo.tmRejectionReason}</p>
        </div>
      )}

      {/* Stepper */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <div className="flex items-center overflow-x-auto">
          {LWO_STAGES.map((s, i) => {
            const done = i < stageIdx + 1
            const active = i === stageIdx
            return (
              <div key={s.key} className="flex items-center min-w-0">
                <div className={`flex flex-col items-center min-w-[72px] px-1 ${active ? 'opacity-100' : done ? 'opacity-90' : 'opacity-35'}`}>
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold border-2 mb-1
                    ${s.key === 'AwaitingTmReview' && active ? 'border-amber-500 bg-amber-500 text-white'
                      : active ? 'border-purple-600 bg-purple-600 text-white'
                      : done   ? 'border-green-500 bg-green-500 text-white'
                               : 'border-gray-300 bg-white text-gray-400'}`}>
                    {done && !active ? '✓' : i + 1}
                  </div>
                  <p className="text-[11px] font-semibold text-center text-gray-700 leading-tight">{s.label}</p>
                  <p className="text-[9px] text-center text-gray-400 leading-tight">{s.desc}</p>
                </div>
                {i < LWO_STAGES.length - 1 && (
                  <div className={`h-0.5 flex-1 min-w-[12px] mx-0.5 ${i < stageIdx ? 'bg-green-400' : 'bg-gray-200'}`} />
                )}
              </div>
            )
          })}
        </div>
      </div>

      {/* Stage cards grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">

        {/* ── Intake ── */}
        <div className="lg:col-span-2 bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Intake</p>
          {lwo.intakeDate ? (
            <IntakeDisplay intakeFormJson={lwo.intakeFormJson} intakeDate={lwo.intakeDate} technicianName={lwo.intakeTechnicianName} calibrationSubType={lwo.calibrationSubType} />
          ) : lwo.status === 'Pending' && canAct ? (
            <IntakeForm
              form={intakeForm}
              onChange={setIntakeForm}
              isNawi={isInLab && !isMassCalibration}
              loading={actionLoading}
              onSubmit={async () => {
                await onAction('intake', {
                  customerName:       intakeForm.customerName,
                  customerAddress:    intakeForm.customerAddress,
                  contactPersonName:  intakeForm.contactPersonName,
                  contactPersonPhone: intakeForm.contactPersonPhone,
                  deliveryPersonName: intakeForm.deliveryPersonName,
                  deliveryPersonId:   intakeForm.deliveryPersonId,
                  conditionOnReceipt: intakeForm.conditionOnReceipt,
                  jobDescription:     intakeForm.jobDescription,
                  accessoriesReceived:intakeForm.accessoriesReceived,
                  calibrationSubType: isMassCalibration ? 'Mass' : (intakeForm.calibrationSubType || undefined),
                  location:           intakeForm.location || undefined,
                  stickerNumber:      intakeForm.stickerNumber || undefined,
                })
              }}
            />
          ) : <p className="text-sm text-gray-400">Not yet recorded.</p>}
        </div>

        {/* ── Bench Start ── */}
        <div className="bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Bench Start</p>
          {lwo.benchStartDate ? (
            <div className="space-y-1.5 text-sm">
              <InfoRow label="Technician" value={lwo.benchTechnicianName ?? '—'} />
              <InfoRow label="Started"    value={new Date(lwo.benchStartDate).toLocaleDateString()} />
              {lwo.benchCompletedDate && <InfoRow label="Completed" value={new Date(lwo.benchCompletedDate).toLocaleDateString()} />}
            </div>
          ) : lwo.status === 'IntakeComplete' && canAct ? (
            <form onSubmit={async e => { e.preventDefault(); await onAction('bench', { notes: benchForm.notes }) }} className="flex flex-col gap-3">
              <textarea value={benchForm.notes}
                onChange={e => setBenchForm(f => ({ ...f, notes: e.target.value }))}
                placeholder="Initial bench notes…" rows={3}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-400" />
              <button type="submit" disabled={actionLoading}
                className="self-start px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold disabled:opacity-70">
                {actionLoading ? 'Saving…' : 'Start Bench Work'}
              </button>
            </form>
          ) : <p className="text-sm text-gray-400">Not yet started.</p>}
        </div>

        {/* ── Bench Completion + Checklist + Photos (full width) ── */}
        {(lwo.status === 'InBench' || lwo.benchSubmittedAt) && (
          <div className="lg:col-span-2 bg-white border border-gray-200 rounded-2xl p-5 space-y-5">
            <div className="flex items-center justify-between">
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">Bench Completion Checklist</p>
              {lwo.benchSubmittedAt && (
                <span className="text-xs text-gray-400">Submitted {new Date(lwo.benchSubmittedAt).toLocaleDateString()}</span>
              )}
            </div>

            {/* Show completed checklist if already submitted */}
            {prevBenchChecklist ? (
              <div className="border border-gray-200 rounded-xl overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="bg-gray-50 border-b border-gray-100">
                      <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500 w-[55%]">Item</th>
                      <th className="text-center px-3 py-2 text-xs font-semibold text-gray-500 w-20">Answer</th>
                      <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {prevBenchChecklist.map((row, i) => (
                      <tr key={i} className="border-b border-gray-50 last:border-0">
                        <td className="px-4 py-2.5 text-gray-700 text-xs">{row.item}</td>
                        <td className="px-3 py-2.5 text-center">
                          <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                            row.answer === 'Yes' ? 'bg-green-100 text-green-700' :
                            row.answer === 'No'  ? 'bg-red-100 text-red-600'    :
                            row.answer === 'N/A' ? 'bg-gray-100 text-gray-500'  : 'bg-yellow-50 text-yellow-600'
                          }`}>{row.answer || '—'}</span>
                        </td>
                        <td className="px-4 py-2.5 text-gray-400 text-xs">{row.notes || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : lwo.status === 'InBench' && canAct ? (
              /* Fill in checklist */
              <div className="space-y-4">
                <div className="border border-gray-200 rounded-xl overflow-hidden">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-gray-50 border-b border-gray-100">
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-600 w-[50%]">Calibration Step</th>
                        <th className="text-center px-2 py-2.5 text-xs font-semibold text-gray-600">Yes</th>
                        <th className="text-center px-2 py-2.5 text-xs font-semibold text-gray-600">No</th>
                        <th className="text-center px-2 py-2.5 text-xs font-semibold text-gray-600">N/A</th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-600">Notes</th>
                      </tr>
                    </thead>
                    <tbody>
                      {benchChecklist.map((row, i) => (
                        <tr key={i} className="border-b border-gray-100 last:border-0">
                          <td className="px-4 py-3 text-xs text-gray-700">{row.item}</td>
                          {['Yes', 'No', 'N/A'].map(opt => (
                            <td key={opt} className="text-center px-2 py-3">
                              <input type="radio" name={`bcl-${i}`} value={opt}
                                checked={row.answer === opt}
                                onChange={() => setBenchChecklist(prev => prev.map((r, j) => j === i ? { ...r, answer: opt } : r))}
                                className="accent-amber-500 w-4 h-4" />
                            </td>
                          ))}
                          <td className="px-4 py-2">
                            <input value={row.notes}
                              onChange={e => setBenchChecklist(prev => prev.map((r, j) => j === i ? { ...r, notes: e.target.value } : r))}
                              placeholder="Optional…"
                              className="w-full border border-gray-200 rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-amber-400" />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Data sheet photo upload */}
                <div>
                  <p className="text-xs font-semibold text-gray-600 mb-2">Calibration Data Sheet Photos</p>
                  <label className="flex items-center gap-2 cursor-pointer w-fit px-4 py-2 border-2 border-dashed border-gray-300 rounded-lg hover:border-amber-400 transition-colors text-sm text-gray-500 hover:text-amber-600">
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                    </svg>
                    {photoFiles.length > 0 ? `${photoFiles.length} photo${photoFiles.length > 1 ? 's' : ''} selected` : 'Attach data sheet photos'}
                    <input type="file" multiple accept="image/*" className="hidden"
                      onChange={e => setPhotoFiles(Array.from(e.target.files))} />
                  </label>
                  {photoFiles.length > 0 && (
                    <div className="flex flex-wrap gap-2 mt-2">
                      {photoFiles.map((f, i) => (
                        <div key={i} className="text-xs bg-amber-50 text-amber-700 border border-amber-200 rounded-md px-2 py-1 flex items-center gap-1">
                          {f.name}
                          <button type="button" onClick={() => setPhotoFiles(prev => prev.filter((_, j) => j !== i))} className="text-amber-400 hover:text-red-500 ml-1">×</button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <button disabled={actionLoading || photoUploading}
                  onClick={async () => {
                    const res = await onAction('bench-complete', { notes: benchForm.notes || undefined, checklistJson: JSON.stringify(benchChecklist) })
                    if (res?.id) await uploadDataSheets(res.id)
                    else if (lwo?.id) await uploadDataSheets(lwo.id)
                  }}
                  className="px-5 py-2.5 rounded-lg bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-70 transition-colors">
                  {actionLoading || photoUploading ? 'Submitting…' : 'Submit for TM Review'}
                </button>
              </div>
            ) : null}
          </div>
        )}

        {/* ── Data Sheet ── */}
        {(lwo.status === 'InBench' || lwo.status === 'AwaitingTmReview' || lwo.dataSheet) && (
          <div className="lg:col-span-2">
            <DataSheetSection
              lwo={lwo}
              assignment={assignment}
              canAct={canAct}
              actionLoading={dsActionLoading}
              onSave={onDataSheet}
              isInLab={isInLab}
              isMassCalibration={isMassCalibration}
              onRecalculate={onRecalculate}
              docPrefix={docPrefix}
            />
          </div>
        )}

        {/* ── TM Review card ── */}
        {(lwo.status === 'AwaitingTmReview' || lwo.tmReviewedAt) && (
          <div className="lg:col-span-2 bg-white border border-gray-200 rounded-2xl p-5 space-y-4">
            <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">TM Review</p>
            {lwo.tmReviewedAt && lwo.status !== 'AwaitingTmReview' ? (
              <div className="space-y-1.5 text-sm">
                <InfoRow label="Checked By"  value={lwo.tmReviewedByName ?? '—'} />
                <InfoRow label="Date"        value={new Date(lwo.tmReviewedAt).toLocaleDateString()} />
                {lwo.tmApprovalNotes && <InfoRow label="Notes" value={lwo.tmApprovalNotes} />}
              </div>
            ) : lwo.status === 'AwaitingTmReview' && canAct ? (
              <div className="space-y-4">
                <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
                  Technician has submitted bench work for review. Check the calibration checklist and data sheet photos above, then approve or send back.
                </div>
                {/* Approve form */}
                <div className="border border-green-200 rounded-xl p-4 space-y-3 bg-green-50/40">
                  <p className="text-xs font-bold text-green-700 uppercase tracking-wide">Approve — Issue Certificate</p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <input value={tmCertForm.certificateNumber}
                      onChange={e => setTmCertForm(f => ({ ...f, certificateNumber: e.target.value }))}
                      placeholder="Certificate number *"
                      className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-400" />
                    <input value={tmCertForm.jobNumber}
                      onChange={e => setTmCertForm(f => ({ ...f, jobNumber: e.target.value }))}
                      placeholder="Job number (optional)"
                      className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-400" />
                  </div>
                  <input value={tmCertForm.tmName}
                    onChange={e => setTmCertForm(f => ({ ...f, tmName: e.target.value }))}
                    placeholder="Checked by (name) *"
                    className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-400" />
                  <textarea value={tmCertForm.notes}
                    onChange={e => setTmCertForm(f => ({ ...f, notes: e.target.value }))}
                    placeholder="Approval notes…" rows={2}
                    className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-green-400" />
                  <button disabled={!tmCertForm.certificateNumber.trim() || !tmCertForm.tmName.trim() || actionLoading}
                    onClick={() => onAction('tm-review', { approve: true, certificateNumber: tmCertForm.certificateNumber, jobNumber: tmCertForm.jobNumber || undefined, tmName: tmCertForm.tmName || undefined, notes: tmCertForm.notes || undefined })}
                    className="px-5 py-2 rounded-lg bg-green-600 hover:bg-green-700 text-white text-sm font-bold disabled:opacity-50 transition-colors">
                    {actionLoading ? 'Approving…' : 'Approve & Issue Certificate'}
                  </button>
                </div>
                {/* Reject form */}
                <div className="border border-red-200 rounded-xl p-4 space-y-3 bg-red-50/40">
                  <p className="text-xs font-bold text-red-600 uppercase tracking-wide">Send Back for Rework</p>
                  <textarea value={tmRejectReason}
                    onChange={e => setTmRejectReason(e.target.value)}
                    placeholder="State what needs to be corrected…" rows={2}
                    className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-300" />
                  <button disabled={!tmRejectReason.trim() || actionLoading}
                    onClick={() => onAction('tm-review', { approve: false, rejectionReason: tmRejectReason, certificateNumber: '' })}
                    className="px-5 py-2 rounded-lg bg-red-600 hover:bg-red-700 text-white text-sm font-semibold disabled:opacity-50 transition-colors">
                    {actionLoading ? 'Sending…' : 'Send Back'}
                  </button>
                </div>
              </div>
            ) : null}
          </div>
        )}

        {/* ── Certificate ── */}
        <div className="bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Certificate</p>
          {lwo.certificateNumber ? (
            <div className="space-y-1.5 text-sm">
              <InfoRow label="Certificate #" value={lwo.certificateNumber} />
              {lwo.jobNumber && <InfoRow label="Job #" value={lwo.jobNumber} />}
              <InfoRow label="Issued" value={lwo.certificateIssuedAt ? new Date(lwo.certificateIssuedAt).toLocaleDateString() : '—'} />
              {lwo.certificateNotes && <InfoRow label="Notes" value={lwo.certificateNotes} />}
              <div className="mt-3 pt-3 border-t border-gray-100">
                <InfoRow label="Approved By" value={lwo.tmReviewedByName ?? '—'} />
              </div>
            </div>
          ) : <p className="text-sm text-gray-400">Pending TM approval.</p>}
        </div>

        {/* ── Dispatch ── */}
        <div className="bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Dispatch</p>
          {lwo.dispatchDate ? (
            <div className="space-y-1.5 text-sm">
              <InfoRow label="Date"      value={new Date(lwo.dispatchDate).toLocaleDateString()} />
              <InfoRow label="Method"    value={lwo.dispatchMethod ?? '—'} />
              {lwo.receivedBy && <InfoRow label="Received By" value={lwo.receivedBy} />}
              {lwo.dispatchNotes && <InfoRow label="Notes" value={lwo.dispatchNotes} />}
            </div>
          ) : lwo.status === 'CertificateIssued' && canAct ? (
            <form onSubmit={async e => { e.preventDefault(); await onAction('dispatch', { dispatchMethod: dispatchForm.dispatchMethod, notes: dispatchForm.notes || undefined, receivedBy: dispatchForm.receivedBy || undefined }) }} className="flex flex-col gap-3">
              <select value={dispatchForm.dispatchMethod}
                onChange={e => setDispatchForm(f => ({ ...f, dispatchMethod: e.target.value }))}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-purple-400">
                <option value="ClientPickup">Client Pickup</option>
                <option value="Courier">Courier</option>
                <option value="Delivery">Delivery</option>
              </select>
              <input value={dispatchForm.receivedBy}
                onChange={e => setDispatchForm(f => ({ ...f, receivedBy: e.target.value }))}
                placeholder="Received by (optional)"
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-purple-400" />
              <textarea value={dispatchForm.notes}
                onChange={e => setDispatchForm(f => ({ ...f, notes: e.target.value }))}
                placeholder="Dispatch notes…" rows={2}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-purple-400" />
              <button type="submit" disabled={actionLoading}
                className="self-start px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold disabled:opacity-70">
                {actionLoading ? 'Saving…' : 'Record Dispatch'}
              </button>
            </form>
          ) : <p className="text-sm text-gray-400">Not yet dispatched.</p>}
        </div>
      </div>
    </div>
  )
}

// ── IntakeForm ────────────────────────────────────────────────────────────────
function IntakeForm({ form, onChange, isNawi, loading, onSubmit }) {
  const f = (k, v) => onChange(p => ({ ...p, [k]: v }))
  return (
    <div className="space-y-5">
      {/* Customer */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Customer Details</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <SInput value={form.customerName}    onChange={v => f('customerName', v)}    placeholder="Customer name *" />
          <SInput value={form.customerAddress} onChange={v => f('customerAddress', v)} placeholder="Address" />
          <SInput value={form.contactPersonName}  onChange={v => f('contactPersonName', v)}  placeholder="Contact person name" />
          <SInput value={form.contactPersonPhone} onChange={v => f('contactPersonPhone', v)} placeholder="Contact phone / email" />
        </div>
      </div>
      {/* Delivery */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Delivery Person</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <SInput value={form.deliveryPersonName} onChange={v => f('deliveryPersonName', v)} placeholder="Name" />
          <SInput value={form.deliveryPersonId}   onChange={v => f('deliveryPersonId', v)}   placeholder="ID / phone number" />
        </div>
      </div>
      {/* Equipment */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Equipment Details</p>
        <div className="grid grid-cols-1 gap-3">
          <STextarea value={form.conditionOnReceipt} onChange={v => f('conditionOnReceipt', v)} placeholder="Condition on receipt *" rows={2} />
          <STextarea value={form.jobDescription}     onChange={v => f('jobDescription', v)}     placeholder="Job description / what is to be done *" rows={2} />
          <SInput    value={form.accessoriesReceived} onChange={v => f('accessoriesReceived', v)} placeholder="Accessories received with the equipment (if any)" />
          <SInput    value={form.location}            onChange={v => f('location', v)}            placeholder="Location of the equipment (building / room)" />
          <SInput    value={form.stickerNumber}       onChange={v => f('stickerNumber', v)}       placeholder="Sticker / tag number (if any)" />
        </div>
      </div>
      {/* NAWI sub-type */}
      {isNawi && (
        <div>
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">NAWI Instrument Type</p>
          <div className="flex gap-3">
            {[['BalanceAndPlatform', 'Balance / Platform Scale'], ['Weighbridge', 'Weighbridge']].map(([val, label]) => (
              <button key={val} type="button"
                onClick={() => f('calibrationSubType', val)}
                className={`px-4 py-2 rounded-lg text-sm font-semibold border transition-colors ${form.calibrationSubType === val ? 'bg-amber-500 text-black border-amber-500' : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}>
                {label}
              </button>
            ))}
          </div>
        </div>
      )}
      <button disabled={loading || !form.customerName || !form.conditionOnReceipt || !form.jobDescription || (isNawi && !form.calibrationSubType)}
        onClick={onSubmit}
        className="px-5 py-2.5 rounded-xl bg-purple-600 hover:bg-purple-700 text-white text-sm font-bold disabled:opacity-60 transition-colors">
        {loading ? 'Saving…' : 'Record Intake'}
      </button>
    </div>
  )
}

function IntakeDisplay({ intakeFormJson, intakeDate, technicianName, calibrationSubType }) {
  const d = (() => { try { return JSON.parse(intakeFormJson) } catch { return null } })()
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-1.5 text-sm">
      <InfoRow label="Date"             value={intakeDate ? new Date(intakeDate).toLocaleDateString() : '—'} />
      <InfoRow label="Received by"      value={technicianName ?? d?.receivedByName ?? '—'} />
      {d && <>
        <InfoRow label="Customer"       value={d.customerName} />
        <InfoRow label="Address"        value={d.customerAddress} />
        <InfoRow label="Contact person" value={d.contactPersonName} />
        <InfoRow label="Contact phone"  value={d.contactPersonPhone} />
        <InfoRow label="Delivery person" value={d.deliveryPersonName} />
        <InfoRow label="Delivery ID"    value={d.deliveryPersonId} />
        <InfoRow label="Condition on receipt" value={d.conditionOnReceipt} />
        <InfoRow label="Job description"      value={d.jobDescription} />
        {d.accessoriesReceived && <InfoRow label="Accessories"    value={d.accessoriesReceived} />}
        {d.location            && <InfoRow label="Location"       value={d.location} />}
        {d.stickerNumber       && <InfoRow label="Sticker Number" value={d.stickerNumber} />}
      </>}
      {calibrationSubType && <InfoRow label="Instrument type" value={calibrationSubType === 'Weighbridge' ? 'Weighbridge' : 'Balance / Platform Scale'} />}
    </div>
  )
}

// ── DataSheetSection ──────────────────────────────────────────────────────────
function DataSheetSection({ lwo, assignment, canAct, actionLoading, onSave, isInLab, isMassCalibration, onRecalculate, docPrefix = 'LT' }) {
  const navigate = useNavigate()
  const sheet  = lwo.dataSheet
  const sheetType = lwo.calibrationSubType === 'Weighbridge'        ? 'NawiWeighbridge'
                  : lwo.calibrationSubType === 'BalanceAndPlatform'  ? 'NawiBalance'
                  : isMassCalibration                                ? 'Mass'
                  : isInLab                                          ? 'NawiBalance'
                  : 'Mass'

  const calcResults = (() => {
    if (!sheet?.calculatedResultsJson) return null
    try { return JSON.parse(sheet.calculatedResultsJson) } catch { return null }
  })()

  const rawData = (() => {
    if (!sheet?.rawDataJson) return null
    try { return JSON.parse(sheet.rawDataJson) } catch { return null }
  })()

  const submitted = sheet?.status === 'Submitted'
  const canRecalculate = submitted && sheetType !== 'Mass' && !!onRecalculate
  const hasCertificate = !!lwo.certificateNumber

  return (
    <div className="bg-white border border-gray-200 rounded-2xl p-5 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">Calibration Data Sheet</p>
          <p className="text-xs text-gray-400 mt-0.5">
            {sheetType === 'Mass' ? `MASS — ${docPrefix}/QP/19/DS-MASS` : `NAWI — ${docPrefix}/QP/18/DS-NAWI`}
            {sheet && <span className={`ml-2 px-1.5 py-0.5 rounded-full text-xs font-semibold ${submitted ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'}`}>{sheet.status}</span>}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {submitted && sheet?.submittedAt && (
            <span className="text-xs text-gray-400">Submitted {new Date(sheet.submittedAt).toLocaleDateString()}</span>
          )}
          {canRecalculate && (
            <button onClick={onRecalculate} disabled={actionLoading}
              title="Recompute uncertainty budget with current formulas"
              className="px-3 py-1.5 rounded-lg bg-amber-500 text-white text-xs font-semibold hover:bg-amber-600 disabled:opacity-50">
              {actionLoading ? 'Recalculating…' : 'Recalculate'}
            </button>
          )}
          {hasCertificate && (
            <button onClick={() => navigate(`/modules/operations/assignments/${assignment.id}/certificate`)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold transition-colors">
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              View Certificate
            </button>
          )}
        </div>
      </div>

      {sheetType === 'Mass'
        ? <MassDataSheetForm rawData={rawData} calcResults={calcResults} canAct={canAct && !submitted} actionLoading={actionLoading} onSave={onSave} />
        : <NawiDataSheetForm rawData={rawData} calcResults={calcResults} canAct={canAct && !submitted} actionLoading={actionLoading} isWeighbridge={sheetType === 'NawiWeighbridge'} onSave={onSave} />
      }
    </div>
  )
}

// ── MASS Data Sheet Form ──────────────────────────────────────────────────────
const EMPTY_MASS_BLOCK = () => ({
  nominalValue: '', class: '', serialNo: '', referenceStdSerialNo: '', referenceStdClass: '',
  referenceStdCertificateNo: '', referenceStdCorrectionMg: '', referenceStdCertificateUncertaintyG: '',
  referenceStdCertificateCoverageFactor: '2', itemDensityKgM3: '',
  standard1Ind1: '', standard1Ind2: '', mass1Ind1: '', mass1Ind2: '',
  mass2Ind1: '', mass2Ind2: '', standard2Ind1: '', standard2Ind2: '',
})

function MassDataSheetForm({ rawData, calcResults, canAct, actionLoading, onSave }) {
  const [env, setEnv] = useState(rawData?.environmentalConditions ?? { startTemperature: '', startHumidity: '', endTemperature: '', endHumidity: '' })
  const [comp, setComp] = useState(rawData?.comparatorDetails ?? { model: '', serialNo: '', capacity: '', division: '' })
  const [blocks, setBlocks] = useState(rawData?.measurementBlocks?.length ? rawData.measurementBlocks : [EMPTY_MASS_BLOCK()])
  const [doneBy, setDoneBy] = useState(rawData?.calibrationDoneBy ?? '')
  const [checkedBy, setCheckedBy] = useState(rawData?.checkedBy ?? '')

  const setBlock = (i, k, v) => setBlocks(prev => prev.map((b, j) => j === i ? { ...b, [k]: v } : b))

  const buildPayload = () => JSON.stringify({
    environmentalConditions: { startTemperature: +env.startTemperature || null, startHumidity: +env.startHumidity || null, endTemperature: +env.endTemperature || null, endHumidity: +env.endHumidity || null },
    comparatorDetails: comp,
    measurementBlocks: blocks.map(b => ({
      nominalValue: b.nominalValue, class: b.class, serialNo: b.serialNo || null,
      referenceStdSerialNo: b.referenceStdSerialNo || null, referenceStdClass: b.referenceStdClass || null,
      referenceStdCertificateNo: b.referenceStdCertificateNo || null,
      referenceStdCorrectionMg: +b.referenceStdCorrectionMg || null,
      referenceStdCertificateUncertaintyG: +b.referenceStdCertificateUncertaintyG || null,
      referenceStdCertificateCoverageFactor: +b.referenceStdCertificateCoverageFactor || 2,
      itemDensityKgM3: +b.itemDensityKgM3 || null,
      standard1Ind1: +b.standard1Ind1 || null, standard1Ind2: +b.standard1Ind2 || null,
      mass1Ind1: +b.mass1Ind1 || null, mass1Ind2: +b.mass1Ind2 || null,
      mass2Ind1: +b.mass2Ind1 || null, mass2Ind2: +b.mass2Ind2 || null,
      standard2Ind1: +b.standard2Ind1 || null, standard2Ind2: +b.standard2Ind2 || null,
    })),
    calibrationDoneBy: doneBy || null, checkedBy: checkedBy || null,
  })

  return (
    <div className="space-y-5">
      {/* Environmental Conditions */}
      <EnvConditionsSection env={env} onChange={setEnv} readOnly={!canAct} />

      {/* Comparator Details */}
      <div>
        <p className="text-xs font-semibold text-gray-600 mb-2">Comparator Details</p>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[['model','Model'],['serialNo','Serial No.'],['capacity','Capacity'],['division','Division']].map(([k,lbl]) => (
            <div key={k}>
              <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
              {canAct ? <SInput value={comp[k]} onChange={v => setComp(p => ({...p, [k]: v}))} /> : <p className="text-sm text-gray-700">{comp[k] || '—'}</p>}
            </div>
          ))}
        </div>
        <p className="text-xs text-gray-400 italic mt-2">The Reference Standards used are traceable to the National Standards</p>
      </div>

      {/* Measurement blocks */}
      <div className="space-y-4">
        <p className="text-xs font-semibold text-gray-600">Measurement Results</p>
        {blocks.map((b, i) => (
          <div key={i} className="border border-gray-200 rounded-xl p-4 space-y-3">
            <div className="flex items-center justify-between">
              <p className="text-xs font-bold text-gray-500">Block {i + 1}</p>
              {canAct && blocks.length > 1 && <button onClick={() => setBlocks(prev => prev.filter((_,j) => j !== i))} className="text-xs text-red-400 hover:text-red-600">Remove</button>}
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
              {[
                ['nominalValue','Nominal Value'],['class','Class'],['serialNo','Serial No.'],
                ['referenceStdSerialNo','Ref Std S/No'],['referenceStdClass','Ref Std Class'],
                ['referenceStdCertificateNo','Ref Std Cert No.'],
                ['referenceStdCorrectionMg','Ref Std Correction (mg)'],
                ['referenceStdCertificateUncertaintyG','Ref Std U_cert (g)'],
                ['referenceStdCertificateCoverageFactor','Ref Std k'],
                ['itemDensityKgM3','Item Density (kg/m³)'],
              ].map(([k,lbl]) => (
                <div key={k}>
                  <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
                  {canAct ? <SInput value={b[k]} onChange={v => setBlock(i, k, v)} /> : <p className="text-sm text-gray-700">{b[k] || '—'}</p>}
                </div>
              ))}
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-xs border border-gray-200 rounded-lg overflow-hidden">
                <thead className="bg-gray-50">
                  <tr><th className="px-3 py-2 text-left font-semibold text-gray-500 w-28">Row</th><th className="px-3 py-2 text-center font-semibold text-gray-500">Indication 1</th><th className="px-3 py-2 text-center font-semibold text-gray-500">Indication 2</th></tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {[['Standard (1st)','standard1Ind1','standard1Ind2'],['Mass (1st)','mass1Ind1','mass1Ind2'],['Mass (2nd)','mass2Ind1','mass2Ind2'],['Standard (2nd)','standard2Ind1','standard2Ind2']].map(([row,k1,k2]) => (
                    <tr key={row}>
                      <td className="px-3 py-2 font-medium text-gray-600">{row}</td>
                      {[k1,k2].map(k => (
                        <td key={k} className="px-3 py-2 text-center">
                          {canAct ? <input type="number" step="any" value={b[k]} onChange={e => setBlock(i, k, e.target.value)} className="w-24 border border-gray-200 rounded px-2 py-1 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                          : <span className="text-gray-700">{b[k] || '—'}</span>}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ))}
        {canAct && (
          <button onClick={() => setBlocks(prev => [...prev, EMPTY_MASS_BLOCK()])}
            className="text-sm text-amber-600 hover:text-amber-700 font-semibold border border-dashed border-amber-300 rounded-lg px-4 py-2 w-full hover:bg-amber-50 transition-colors">
            + Add Weight Block
          </button>
        )}
      </div>

      {/* Mass calculation results */}
      {calcResults?.blockResults?.length > 0 && (
        <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 space-y-4">
          <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide">Measurement Results Summary</p>
          <div className="overflow-x-auto">
            <table className="w-full text-xs border border-gray-200 rounded-xl overflow-hidden">
              <thead className="bg-gray-100 border-b border-gray-200">
                <tr>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">Nominal Value</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">Class</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">S̄ (Mean Std)</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">X̄ (Mean Mass)</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">Δ = X̄ − S̄</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">C_buoy (mg)</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">Final Corr. (mg)</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">±MPE (mg)</th>
                  <th className="px-3 py-2 text-center font-semibold text-gray-500">Pass</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {calcResults.blockResults.map((br, i) => (
                  <tr key={i} className={i % 2 === 0 ? 'bg-white' : 'bg-gray-50/40'}>
                    <td className="px-3 py-2 text-center font-medium text-gray-700">{br.nominalValue}</td>
                    <td className="px-3 py-2 text-center text-gray-600">{br.class}</td>
                    <td className="px-3 py-2 text-center font-mono">{br.meanStandardReading ?? '—'}</td>
                    <td className="px-3 py-2 text-center font-mono">{br.meanMassReading ?? '—'}</td>
                    <td className="px-3 py-2 text-center font-mono font-semibold text-gray-800">{br.massDifference ?? '—'}</td>
                    <td className="px-3 py-2 text-center font-mono text-gray-500">{br.buoyancyCorrectionMg != null ? br.buoyancyCorrectionMg : '—'}</td>
                    <td className="px-3 py-2 text-center font-mono font-semibold text-indigo-700">{br.finalCorrectionMg != null ? br.finalCorrectionMg : (br.massDifference ?? '—')}</td>
                    <td className="px-3 py-2 text-center font-mono text-gray-600">{br.mpeMilligrams != null ? `±${br.mpeMilligrams}` : '—'}</td>
                    <td className="px-3 py-2 text-center">
                      {br.withinTolerance == null
                        ? <span className="text-xs text-gray-300">—</span>
                        : br.withinTolerance
                          ? <span className="text-xs font-semibold text-green-600 bg-green-50 px-1.5 py-0.5 rounded">PASS</span>
                          : <span className="text-xs font-semibold text-red-600 bg-red-50 px-1.5 py-0.5 rounded">FAIL</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {calcResults.uncertainty && (
            <div>
              <p className="text-xs text-gray-500 font-semibold mb-2">Uncertainty Budget (GUM, k={calcResults.uncertainty.coverageFactor ?? 2}, {calcResults.uncertainty.confidenceLevel ?? '95%'})</p>
              <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
                {[
                  ['u_balance', calcResults.uncertainty.uBalance],
                  ['u_resolution', calcResults.uncertainty.uResolution],
                  ['u_air buoyancy', calcResults.uncertainty.uAirBuoyancy],
                  ['u_combined', calcResults.uncertainty.uCombined],
                  [`U (k=${calcResults.uncertainty.coverageFactor ?? 2})`, calcResults.uncertainty.uExpanded],
                ].filter(([, v]) => v != null).map(([label, val]) => (
                  <div key={label} className="bg-white rounded-lg p-2 border border-gray-100">
                    <p className="text-xs text-gray-400">{label}</p>
                    <p className="text-xs font-mono font-semibold text-gray-700">{val}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Sign-off */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div><label className="text-xs text-gray-500 mb-1 block">Calibration Done by</label>{canAct ? <SInput value={doneBy} onChange={setDoneBy} /> : <p className="text-sm text-gray-700">{doneBy || '—'}</p>}</div>
        <div><label className="text-xs text-gray-500 mb-1 block">Checked by</label>{canAct ? <SInput value={checkedBy} onChange={setCheckedBy} /> : <p className="text-sm text-gray-700">{checkedBy || '—'}</p>}</div>
      </div>

      {canAct && (
        <div className="flex gap-3 justify-end pt-2">
          <button disabled={actionLoading} onClick={() => onSave(buildPayload(), false)} className="px-4 py-2 rounded-xl border border-gray-300 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {actionLoading ? 'Saving…' : 'Save Draft'}
          </button>
          <button disabled={actionLoading} onClick={() => onSave(buildPayload(), true)} className="px-5 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60">
            {actionLoading ? 'Submitting…' : 'Submit for TM Review'}
          </button>
        </div>
      )}
    </div>
  )
}

// ── NAWI Data Sheet Form (Balance/Platform + Weighbridge) ─────────────────────
const NAWI_EQUIPMENT_TYPES = [
  'Top Loading Electronic Balance',
  'Top Pan Mechanical Balance',
  'Analytical Electronic Balance',
  'Analytical Mechanical Balance',
  'Platform Electronic Balance',
  'Platform Mechanical Balance',
  'Adult Weighing Scale',
  'Baby Weighing Balance',
  'Wheel Chair Weighing Balance',
  'Hydrostatic Balance',
  'Single Pan Balance',
  'Double Pan Balance',
  'Single Beam Mechanical Balance',
  'Double Beam Mechanical Balance',
  'Triple Beam Mechanical Balance',
  'Digital Handheld Scale',
  'Spring Scale',
  'Pull Tester (Spring Scale)',
  'Suspension Weighing Balance',
  'Grammage Scale',
  'Pocket Balance',
]

const NAWI_RANGE_TYPES = [
  'Single range instrument',
  'Multi range instrument',
  'Multi interval instrument',
]

const NAWI_LAB_NO_OPTIONS = [
  'NAWI (Site)',
  'NAWI (Internal)',
]

function NawiDataSheetForm({ rawData, calcResults, canAct, actionLoading, isWeighbridge, onSave }) {
  const repCount = isWeighbridge ? 3 : 5

  const [instrument, setInstrument] = useState(rawData?.instrumentDetails ?? { equipmentType: '', rangeType: '', manufacturer: '', model: '', serialNo: '', maximumCapacity: '', division: '', minimumCapacity: '', accuracyClass: '' })
  const [labNo, setLabNo] = useState(rawData?.labNo ?? '')
  const [testWeights, setTestWeights] = useState(rawData?.testWeights ?? { class: '', serialNumber: '', traceabilityCertificateNo: '', certificateUncertaintyG: '', certificateCoverageFactor: '2', densityKgM3: '', densityUncertaintyKgM3: '' })

  // Class-based density defaults (OIML R 111-1 Table B.7 / EURAMET cg-18 §7.1.2-7)
  const NAWI_CLASS_DENSITY = {
    'E1': { rho: 7950, uHalf: 140 }, 'E2': { rho: 7950, uHalf: 140 },
    'F1': { rho: 7950, uHalf: 140 }, 'F2': { rho: 7950, uHalf: 140 },
    'M1': { rho: 8400, uHalf: 170 }, 'M2': { rho: 7100, uHalf: 600 },
    'M3': { rho: 7100, uHalf: 600 },
  }
  const applyClassDefaults = (cls) => {
    const key = cls?.replace(/class/i, '').trim().toUpperCase()
    const def = NAWI_CLASS_DENSITY[key]
    if (def) setTestWeights(p => ({
      ...p,
      densityKgM3: p.densityKgM3 || String(def.rho),
      densityUncertaintyKgM3: p.densityUncertaintyKgM3 || String(def.uHalf),
    }))
  }
  const [env, setEnv] = useState(rawData?.environmentalConditions ?? { startTemperature: '', startHumidity: '', endTemperature: '', endHumidity: '' })
  const [eccType, setEccType] = useState(rawData?.eccentricityTestType ?? '')
  const [ecc, setEcc] = useState(rawData?.eccentricity ?? { testLoad: '', ind1: '', ind2: '', ind3: '', ind4: '', ind5: '' })
  const [rep, setRep] = useState(() => {
    if (rawData?.repeatability) return rawData.repeatability
    return { testLoad: '', indications: Array(repCount).fill('') }
  })
  const [linearity, setLinearity] = useState(rawData?.linearityRows?.length ? rawData.linearityRows : [{ testLoad: '', asFoundIndication: '', definitiveIndication: '', numberOfReadings: '' }])
  const [doneBy, setDoneBy] = useState(rawData?.calibrationDoneBy ?? '')
  const [checkedBy, setCheckedBy] = useState(rawData?.checkedBy ?? '')

  const eccErrors  = calcResults?.eccentricity?.errors ?? []
  const maxDev     = calcResults?.eccentricity?.maximumDeviation
  const repErrors  = calcResults?.repeatability?.errors ?? []
  const repSD      = calcResults?.repeatability?.standardDeviation
  const repPass    = calcResults?.repeatability?.pass
  const linResults = calcResults?.linearity ?? []

  // Derive recommended test loads from max capacity (same unit as entered)
  const maxCap          = parseFloat(instrument.maximumCapacity) || null
  const eccRecommended  = maxCap ? +(maxCap / 3).toFixed(3) : null   // OIML R 76-1 §3.6.2.2: ≥ max/3
  const repRecommended  = maxCap ? +(maxCap / 2).toFixed(3) : null   // EURAMET cg-18: 0.5 × max ≤ L ≤ max
  const eccLoadOk       = eccRecommended && ecc.testLoad ? +ecc.testLoad >= eccRecommended : null
  const repLoadOk       = repRecommended && rep.testLoad ? +rep.testLoad >= repRecommended : null

  const buildPayload = () => JSON.stringify({
    instrumentDetails: instrument,
    labNo: labNo || null,
    testWeights,
    environmentalConditions: { startTemperature: +env.startTemperature || null, startHumidity: +env.startHumidity || null, endTemperature: +env.endTemperature || null, endHumidity: +env.endHumidity || null },
    eccentricityTestType: isWeighbridge ? (eccType || null) : null,
    eccentricity: { testLoad: +ecc.testLoad || 0, ind1: +ecc.ind1 || null, ind2: +ecc.ind2 || null, ind3: +ecc.ind3 || null, ind4: +ecc.ind4 || null, ind5: +ecc.ind5 || null },
    repeatability: { testLoad: +rep.testLoad || 0, indications: rep.indications.map(v => +v || null) },
    linearityRows: linearity.map(r => ({ testLoad: +r.testLoad || 0, asFoundIndication: +r.asFoundIndication || null, definitiveIndication: +r.definitiveIndication || null, numberOfReadings: +r.numberOfReadings || null })),
    calibrationDoneBy: doneBy || null, checkedBy: checkedBy || null,
  })

  const numInput = (value, onChange) => canAct
    ? <input type="number" step="any" value={value ?? ''} onChange={e => onChange(e.target.value)} className="w-24 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
    : <span className="text-sm text-gray-700">{value ?? '—'}</span>

  const calcBadge = (val) => val != null
    ? <span className={`text-xs font-mono font-bold px-1.5 py-0.5 rounded ${val < 0 ? 'bg-orange-50 text-orange-700' : val > 0 ? 'bg-blue-50 text-blue-700' : 'bg-gray-100 text-gray-500'}`}>{val > 0 ? '+' : ''}{val}</span>
    : <span className="text-xs text-gray-300">—</span>

  return (
    <div className="space-y-6">
      {/* Instrument Details */}
      <div>
        <p className="text-xs font-semibold text-gray-600 mb-2">Instrument Details</p>
        {/* Equipment type + range type + lab no — full-width dropdowns first */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-3">
          <div>
            <label className="text-xs text-gray-500 mb-1 block">Equipment Type</label>
            {canAct
              ? <SSelect value={instrument.equipmentType} onChange={v => setInstrument(p => ({...p, equipmentType: v}))} options={NAWI_EQUIPMENT_TYPES} placeholder="Select equipment type…" />
              : <p className="text-sm text-gray-700">{instrument.equipmentType || '—'}</p>}
          </div>
          <div>
            <label className="text-xs text-gray-500 mb-1 block">Range Type</label>
            {canAct
              ? <SSelect value={instrument.rangeType} onChange={v => setInstrument(p => ({...p, rangeType: v}))} options={NAWI_RANGE_TYPES} placeholder="Select range type…" />
              : <p className="text-sm text-gray-700">{instrument.rangeType || '—'}</p>}
          </div>
          <div>
            <label className="text-xs text-gray-500 mb-1 block">Lab No.</label>
            {canAct
              ? <SSelect value={labNo} onChange={setLabNo} options={NAWI_LAB_NO_OPTIONS} placeholder="Select lab type…" />
              : <p className="text-sm text-gray-700">{labNo || '—'}</p>}
          </div>
        </div>
        {/* Remaining instrument fields */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[['manufacturer','Manufacturer'],['model','Model'],['serialNo','Serial No.'],['maximumCapacity','Max Capacity'],['division','Division (e)'],['minimumCapacity','Min Capacity (20e)'],['accuracyClass','Accuracy Class']].map(([k,lbl]) => (
            <div key={k}>
              <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
              {canAct ? <SInput value={instrument[k]} onChange={v => setInstrument(p => ({...p,[k]:v}))} />
              : <p className="text-sm text-gray-700">{instrument[k] || '—'}</p>}
            </div>
          ))}
        </div>
      </div>

      {/* Test Weights + Environmental */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
        <div>
          <p className="text-xs font-semibold text-gray-600 mb-2">Test Weights Details</p>
          {/* Row 1: Class / Serial / Cert No */}
          <div className="grid grid-cols-3 gap-3 mb-3">
            <div>
              <label className="text-xs text-gray-500 mb-1 block">Class</label>
              {canAct
                ? <SInput value={testWeights.class} onChange={v => { setTestWeights(p => ({...p, class: v})); applyClassDefaults(v) }} />
                : <p className="text-sm text-gray-700">{testWeights.class || '—'}</p>}
            </div>
            {[['serialNumber','Serial Number'],['traceabilityCertificateNo','Certificate No.']].map(([k,lbl]) => (
              <div key={k}>
                <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
                {canAct ? <SInput value={testWeights[k]} onChange={v => setTestWeights(p => ({...p,[k]:v}))} />
                : <p className="text-sm text-gray-700">{testWeights[k] || '—'}</p>}
              </div>
            ))}
          </div>
          {/* Row 2: Cert U / k / Density / Density unc */}
          <div className="grid grid-cols-4 gap-3">
            {[['certificateUncertaintyG','Cert U (g)'],['certificateCoverageFactor','k'],['densityKgM3','Density (kg/m³)'],['densityUncertaintyKgM3','Dens. Unc. (kg/m³)']].map(([k,lbl]) => (
              <div key={k}>
                <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
                {canAct ? <SInput value={testWeights[k]} onChange={v => setTestWeights(p => ({...p,[k]:v}))} />
                : <p className="text-sm text-gray-700">{testWeights[k] || '—'}</p>}
              </div>
            ))}
          </div>
          <p className="text-xs text-gray-400 italic mt-1.5">The Test Weights are traceable to the National Standards</p>
        </div>
        <EnvConditionsSection env={env} onChange={setEnv} readOnly={!canAct} />
      </div>

      {/* Eccentricity Test */}
      <div>
        <p className="text-xs font-semibold text-gray-600 mb-2">Eccentricity Test</p>
        {isWeighbridge && canAct && (
          <div className="flex gap-2 mb-3 flex-wrap">
            {[['EndToEnd','End to End'],['EndMiddleEnd','End–Middle–End'],['EccentricLoading','Eccentric Loading']].map(([val,lbl]) => (
              <button key={val} type="button" onClick={() => setEccType(val)}
                className={`px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors ${eccType === val ? 'bg-amber-500 text-black border-amber-500' : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}>{lbl}</button>
            ))}
          </div>
        )}
        {isWeighbridge && !canAct && eccType && <p className="text-xs text-gray-500 mb-2">Test type: <strong>{eccType}</strong></p>}
        <div className="flex items-center gap-3 mb-1 flex-wrap">
          <label className="text-xs text-gray-500">Test Load</label>
          {canAct ? <input type="number" step="any" value={ecc.testLoad} onChange={e => setEcc(p => ({...p,testLoad:e.target.value}))} className="w-28 border border-gray-200 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-amber-400" />
          : <span className="text-sm font-medium text-gray-700">{ecc.testLoad || '—'}</span>}
          {eccRecommended && (
            <span className="text-xs text-gray-400">Recommended: ≥ <strong>{eccRecommended}</strong> (⅓ max)</span>
          )}
          {eccLoadOk === false && (
            <span className="text-xs font-semibold text-amber-600 bg-amber-50 border border-amber-200 rounded px-2 py-0.5">
              ⚠ Load below recommended minimum
            </span>
          )}
        </div>
        <div className="mb-3" />
        <div className="overflow-x-auto">
          <table className="w-full text-xs border border-gray-200 rounded-xl overflow-hidden">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="px-3 py-2 text-left font-semibold text-gray-500">Test Point</th>
                <th className="px-3 py-2 text-left font-semibold text-gray-500">Position</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Indication</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Error</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {['ind1','ind2','ind3','ind4','ind5'].map((k,i) => {
                const positions = isWeighbridge
                  ? ['Centre','Front','Back','Left','Right']
                  : ['Centre (1)','Front Left (2)','Back Left (3)','Back Right (4)','Front Right (5)']
                return (
                  <tr key={k} className={i === 0 ? 'bg-gray-50/50' : ''}>
                    <td className="px-3 py-2 font-medium text-gray-600">{i + 1}</td>
                    <td className="px-3 py-2 text-gray-500">{positions[i]}</td>
                    <td className="px-3 py-2 text-center">{numInput(ecc[k], v => setEcc(p => ({...p,[k]:v})))}</td>
                    <td className="px-3 py-2 text-center">
                      {i === 0 ? <span className="text-xs text-gray-400 italic">Reference</span> : calcBadge(eccErrors[i])}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {calcResults?.eccentricity && (
          <div className="flex flex-wrap gap-4 mt-2 text-xs text-gray-600">
            <span>Max deviation: <strong className="text-zinc-800 font-mono">{maxDev ?? '—'}</strong></span>
            {calcResults.eccentricity.mpe != null && (
              <span>MPE: <strong className="font-mono">±{calcResults.eccentricity.mpe}</strong></span>
            )}
            {calcResults.eccentricity.pass != null && (
              <span>Result: <strong className={calcResults.eccentricity.pass ? 'text-green-700' : 'text-red-600'}>
                {calcResults.eccentricity.pass ? 'PASS' : 'FAIL'}
              </strong></span>
            )}
            {calcResults.eccentricity.loadAdequate === false && (
              <span className="text-amber-600 font-semibold">⚠ Test load was below recommended ≥ {calcResults.eccentricity.recommendedLoad}</span>
            )}
          </div>
        )}
      </div>

      {/* Repeatability Test */}
      <div>
        <p className="text-xs font-semibold text-gray-600 mb-2">Repeatability Test</p>
        <div className="flex items-center gap-3 mb-1 flex-wrap">
          <label className="text-xs text-gray-500">Test Load</label>
          {canAct ? <input type="number" step="any" value={rep.testLoad} onChange={e => setRep(p => ({...p,testLoad:e.target.value}))} className="w-28 border border-gray-200 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-amber-400" />
          : <span className="text-sm font-medium text-gray-700">{rep.testLoad || '—'}</span>}
          {repRecommended && maxCap && (
            <span className="text-xs text-gray-400">Recommended: <strong>{repRecommended}–{maxCap}</strong> (50%–100% of max)</span>
          )}
          {repLoadOk === false && (
            <span className="text-xs font-semibold text-amber-600 bg-amber-50 border border-amber-200 rounded px-2 py-0.5">
              ⚠ Load below recommended minimum
            </span>
          )}
        </div>
        <div className="mb-3" />
        <div className="overflow-x-auto">
          <table className="w-full text-xs border border-gray-200 rounded-xl overflow-hidden">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="px-3 py-2 text-left font-semibold text-gray-500">Cycle</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Indication</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Error</th>
                {canAct && <th className="w-8" />}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rep.indications.map((ind, i) => (
                <tr key={i}>
                  <td className="px-3 py-2 font-medium text-gray-600">{i + 1}</td>
                  <td className="px-3 py-2 text-center">
                    {canAct ? <input type="number" step="any" value={ind ?? ''} onChange={e => setRep(p => ({ ...p, indications: p.indications.map((v,j) => j===i ? e.target.value : v) }))} className="w-24 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                    : <span className="text-gray-700">{ind ?? '—'}</span>}
                  </td>
                  <td className="px-3 py-2 text-center">{calcBadge(repErrors[i])}</td>
                  {canAct && (
                    <td className="px-1 py-2 text-center">
                      {rep.indications.length > repCount && (
                        <button type="button" onClick={() => setRep(p => ({ ...p, indications: p.indications.filter((_,j) => j !== i) }))}
                          className="text-red-400 hover:text-red-600 text-xs font-bold px-1">×</button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {canAct && (
          <button type="button" onClick={() => setRep(p => ({ ...p, indications: [...p.indications, ''] }))}
            className="mt-2 text-xs text-amber-600 hover:text-amber-800 font-semibold">
            + Add reading
          </button>
        )}
        {repSD != null && (
          <div className="flex flex-wrap gap-4 mt-2 text-xs text-gray-600">
            <span>Standard deviation (s): <strong className="font-mono text-zinc-800">{repSD}</strong></span>
            {calcResults?.repeatability?.mpe != null && (
              <span>Repeatability limit: <strong className="font-mono">±{calcResults.repeatability.mpe}</strong></span>
            )}
            {repPass != null && (
              <span>Result: <strong className={repPass ? 'text-green-700' : 'text-red-600'}>
                {repPass ? 'PASS' : 'FAIL'}
              </strong></span>
            )}
            {calcResults?.repeatability?.insufficientReadings && (
              <span className="text-amber-600 font-semibold">
                ⚠ {calcResults.repeatability.minimumRequiredReadings} readings required
              </span>
            )}
          </div>
        )}
      </div>

      {/* Accuracy Test (Linearity) */}
      <div>
        <p className="text-xs font-semibold text-gray-600 mb-2">Accuracy Test (Linearity)</p>
        <div className="overflow-x-auto">
          <table className="w-full text-xs border border-gray-200 rounded-xl overflow-hidden">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Test Load</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">No. Readings</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Ind. Before Adj.</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">As Found Err.</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Ind. After Adj.</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Definitive Err.</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">±MPE</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">Pass</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">k</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-500">U</th>
                {canAct && <th className="w-8" />}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {linearity.map((row, i) => {
                const lr = linResults[i]
                const passBadge = (pass) => pass == null
                  ? <span className="text-xs text-gray-300">—</span>
                  : pass
                    ? <span className="text-xs font-semibold text-green-600 bg-green-50 px-1.5 py-0.5 rounded">PASS</span>
                    : <span className="text-xs font-semibold text-red-600 bg-red-50 px-1.5 py-0.5 rounded">FAIL</span>
                return (
                  <tr key={i}>
                    <td className="px-2 py-2 text-center">
                      {canAct ? <input type="number" step="any" value={row.testLoad} onChange={e => setLinearity(prev => prev.map((r,j) => j===i ? {...r,testLoad:e.target.value} : r))} className="w-24 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                      : <span className="text-gray-700">{row.testLoad ?? '—'}</span>}
                    </td>
                    <td className="px-2 py-2 text-center">
                      {canAct ? <input type="number" min="1" step="1" value={row.numberOfReadings ?? ''} onChange={e => setLinearity(prev => prev.map((r,j) => j===i ? {...r,numberOfReadings:e.target.value} : r))} className="w-16 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                      : <span className="text-gray-700">{row.numberOfReadings ?? '—'}</span>}
                    </td>
                    <td className="px-2 py-2 text-center">
                      {canAct ? <input type="number" step="any" value={row.asFoundIndication ?? ''} onChange={e => setLinearity(prev => prev.map((r,j) => j===i ? {...r,asFoundIndication:e.target.value} : r))} className="w-24 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                      : <span className="text-gray-700">{row.asFoundIndication ?? '—'}</span>}
                    </td>
                    <td className="px-2 py-2 text-center">{calcBadge(lr?.asFoundError)}</td>
                    <td className="px-2 py-2 text-center">
                      {canAct ? <input type="number" step="any" value={row.definitiveIndication ?? ''} onChange={e => setLinearity(prev => prev.map((r,j) => j===i ? {...r,definitiveIndication:e.target.value} : r))} className="w-24 border border-gray-200 rounded px-2 py-1.5 text-xs text-center focus:outline-none focus:ring-1 focus:ring-amber-400" />
                      : <span className="text-gray-700">{row.definitiveIndication ?? '—'}</span>}
                    </td>
                    <td className="px-2 py-2 text-center">{calcBadge(lr?.definitiveError)}</td>
                    <td className="px-2 py-2 text-center font-mono text-gray-600">{lr?.mpe != null ? `±${lr.mpe}` : '—'}</td>
                    <td className="px-2 py-2 text-center">{passBadge(lr?.definitivePass)}</td>
                    <td className="px-2 py-2 text-center font-mono text-gray-500">{lr?.coverageFactor ?? '—'}</td>
                    <td className="px-2 py-2 text-center font-mono font-semibold text-gray-700">{lr?.uExpanded != null ? `±${lr.uExpanded}` : '—'}</td>
                    {canAct && <td className="px-2 py-2 text-center">
                      {linearity.length > 1 && <button onClick={() => setLinearity(prev => prev.filter((_,j) => j!==i))} className="text-gray-300 hover:text-red-400 text-base">✕</button>}
                    </td>}
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {canAct && (
          <button onClick={() => setLinearity(prev => [...prev, { testLoad: '', asFoundIndication: '', definitiveIndication: '', numberOfReadings: '' }])}
            className="mt-2 text-xs text-amber-600 hover:text-amber-700 font-semibold border border-dashed border-amber-300 rounded-lg px-3 py-1.5 w-full hover:bg-amber-50 transition-colors">
            + Add Row
          </button>
        )}
      </div>

      {/* Calibration results summary */}
      {calcResults && (
        <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 space-y-4">
          <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide">Calibration Results Summary</p>

          {calcResults.tolerance && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                ['Eccentricity', calcResults.tolerance.eccentricityPass],
                ['Repeatability', calcResults.tolerance.repeatabilityPass],
                ['Linearity', calcResults.tolerance.linearityPass],
                ['Overall', calcResults.tolerance.overallPass],
              ].map(([label, pass]) => (
                <div key={label} className="text-center bg-white rounded-lg p-3 border border-gray-200">
                  <p className="text-xs text-gray-500 mb-1">{label}</p>
                  {pass == null
                    ? <span className="text-xs text-gray-400">—</span>
                    : pass
                      ? <span className="text-sm font-bold text-green-600">PASS</span>
                      : <span className="text-sm font-bold text-red-600">FAIL</span>}
                </div>
              ))}
            </div>
          )}

          {(calcResults.uncertainty || calcResults.repeatability?.standardDeviation != null) && (
            <div>
              <p className="text-xs text-gray-500 font-semibold mb-2">Uncertainty Budget (GUM, k=2, 95%)</p>
              <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
                {[
                  ['u_resolution', calcResults.uncertainty?.uResolution],
                  ['u_repeatability', calcResults.uncertainty?.uRepeatability ?? calcResults.repeatability?.standardDeviation],
                  ['u_ref weight', calcResults.uncertainty?.uReferenceWeight],
                  ['u_combined', calcResults.uncertainty?.uCombined],
                  ['U (k=2)', calcResults.uncertainty?.uExpanded],
                ].filter(([, v]) => v != null).map(([label, val]) => (
                  <div key={label} className="bg-white rounded-lg p-2 border border-gray-100">
                    <p className="text-xs text-gray-400">{label}</p>
                    <p className="text-xs font-mono font-semibold text-gray-700">{val}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Sign-off */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div><label className="text-xs text-gray-500 mb-1 block">Calibration Done by</label>{canAct ? <SInput value={doneBy} onChange={setDoneBy} /> : <p className="text-sm text-gray-700">{doneBy || '—'}</p>}</div>
        <div><label className="text-xs text-gray-500 mb-1 block">Checked by</label>{canAct ? <SInput value={checkedBy} onChange={setCheckedBy} /> : <p className="text-sm text-gray-700">{checkedBy || '—'}</p>}</div>
      </div>

      {canAct && (
        <div className="flex gap-3 justify-end pt-2">
          <button disabled={actionLoading} onClick={() => onSave(buildPayload(), false)} className="px-4 py-2 rounded-xl border border-gray-300 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {actionLoading ? 'Saving…' : 'Save Draft'}
          </button>
          <button disabled={actionLoading} onClick={() => onSave(buildPayload(), true)} className="px-5 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60">
            {actionLoading ? 'Submitting…' : 'Submit for TM Review'}
          </button>
        </div>
      )}
    </div>
  )
}

// ── Shared: Environmental Conditions section ──────────────────────────────────
function EnvConditionsSection({ env, onChange, readOnly }) {
  const f = (k, v) => onChange(p => ({ ...p, [k]: v }))
  return (
    <div>
      <p className="text-xs font-semibold text-gray-600 mb-2">Environmental Conditions</p>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[['startTemperature','Start Temp (°C)'],['startHumidity','Start Humidity (%)'],['endTemperature','End Temp (°C)'],['endHumidity','End Humidity (%)']].map(([k,lbl]) => (
          <div key={k}>
            <label className="text-xs text-gray-500 mb-1 block">{lbl}</label>
            {!readOnly ? <input type="number" step="any" value={env[k]} onChange={e => f(k, e.target.value)} className="w-full border border-gray-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-amber-400" />
            : <p className="text-sm text-gray-700">{env[k] || '—'}</p>}
          </div>
        ))}
      </div>
    </div>
  )
}

// ── Pre-Deployment Checklist Tab ───────────────────────────────────────────────

const PPE_ITEMS = [
  'Hard hat / helmet',
  'High-visibility vest',
  'Safety gloves',
  'Safety boots / footwear',
  'Eye protection (safety glasses / goggles)',
  'Face mask / respiratory protection (if required)',
  'First aid kit available on site',
]

const initPreDeployChecklist = () => ({
  ppe: PPE_ITEMS.map(item => ({ item, checked: false, notes: '' })),
  toolsEquipment: { adequate: '', itemsList: '', notes: '' },
  calibrationStandards: { verified: '', standardsRef: '', traceabilityRef: '', certValidity: '', notes: '' },
  massTransport: { applicable: '', vehicleDetails: '', coordinationConfirmed: '', notes: '' },
  labourEngagement: { required: '', count: '', supervisorName: '', notes: '' },
  siteOrientation: { completed: '', conductedBy: '', recordRef: '', notes: '' },
})

function SInput({ value, onChange, placeholder, type = 'text' }) {
  return (
    <input type={type} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
      className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
  )
}

function STextarea({ value, onChange, placeholder, rows = 2 }) {
  return (
    <textarea value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder} rows={rows}
      className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400" />
  )
}

function RadioGroup({ name, value, onChange, options = ['Yes', 'No', 'N/A'] }) {
  return (
    <div className="flex gap-3">
      {options.map(opt => (
        <label key={opt} className="flex items-center gap-1.5 cursor-pointer text-sm text-gray-700">
          <input type="radio" name={name} value={opt} checked={value === opt}
            onChange={() => onChange(opt)} className="accent-amber-500 w-4 h-4" />
          {opt}
        </label>
      ))}
    </div>
  )
}

function SectionHeader({ number, title, sub }) {
  return (
    <div className="flex items-start gap-3 mb-4">
      <div className="w-7 h-7 rounded-full bg-amber-100 text-amber-700 text-xs font-bold flex items-center justify-center shrink-0 mt-0.5">{number}</div>
      <div>
        <p className="text-sm font-bold text-gray-800">{title}</p>
        {sub && <p className="text-xs text-gray-400">{sub}</p>}
      </div>
    </div>
  )
}

function PreDeploymentTab({ assignmentId, data, loading, actionLoading, canAct, onSave }) {
  const [cl, setCl] = useState(() => {
    if (data?.checklistJson) {
      try { return JSON.parse(data.checklistJson) } catch {}
    }
    return initPreDeployChecklist()
  })
  const [notes, setNotes] = useState(data?.notes ?? '')
  const submitted = data?.status === 'Submitted'

  // Re-sync when data loads
  useEffect(() => {
    if (data?.checklistJson) {
      try { setCl(JSON.parse(data.checklistJson)) } catch {}
    }
    setNotes(data?.notes ?? '')
  }, [data])

  const setPpe = (i, field, val) => setCl(prev => ({
    ...prev,
    ppe: prev.ppe.map((row, j) => j === i ? { ...row, [field]: val } : row)
  }))
  const setSection = (section, field, val) => setCl(prev => ({
    ...prev,
    [section]: { ...prev[section], [field]: val }
  }))

  if (loading) return <div className="p-10 text-sm text-gray-400 text-center">Loading pre-deployment checklist…</div>

  return (
    <div className="space-y-5">
      {submitted && (
        <div className="bg-green-50 border border-green-200 rounded-xl px-5 py-3 flex items-center gap-3">
          <svg className="w-5 h-5 text-green-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
          <div>
            <p className="text-sm font-semibold text-green-800">Pre-Deployment Checklist Submitted</p>
            {data.submittedAt && <p className="text-xs text-green-600">By {data.submittedByName ?? 'technician'} on {new Date(data.submittedAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })}</p>}
          </div>
        </div>
      )}

      {/* ── 1. PPE Checklist ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="1" title="Pre-Deployment PPE Checklist" sub="Confirm all personal protective equipment is available and in good condition" />
        <div className="border border-gray-200 rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 w-[55%]">Item</th>
                <th className="text-center px-3 py-2.5 text-xs font-semibold text-gray-500 w-20">Available</th>
                <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500">Notes</th>
              </tr>
            </thead>
            <tbody>
              {cl.ppe.map((row, i) => (
                <tr key={i} className="border-b border-gray-50 last:border-0">
                  <td className="px-4 py-3 text-gray-700 text-xs">{row.item}</td>
                  <td className="px-3 py-3 text-center">
                    {submitted ? (
                      <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${row.checked ? 'bg-green-100 text-green-700' : 'bg-red-50 text-red-500'}`}>
                        {row.checked ? 'Yes' : 'No'}
                      </span>
                    ) : (
                      <input type="checkbox" checked={row.checked} onChange={e => setPpe(i, 'checked', e.target.checked)}
                        className="w-4 h-4 accent-amber-500" disabled={submitted} />
                    )}
                  </td>
                  <td className="px-4 py-2">
                    {submitted
                      ? <span className="text-xs text-gray-400">{row.notes || '—'}</span>
                      : <input value={row.notes} onChange={e => setPpe(i, 'notes', e.target.value)}
                          placeholder="Optional…"
                          className="w-full border border-gray-200 rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-amber-400" />
                    }
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── 2. Tools & Equipment ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="2" title="Tools & Equipment Adequacy Check" sub="Planning stage — confirm all required tools are available and serviceable" />
        <div className="space-y-3">
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">All required equipment available and in good condition?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.toolsEquipment.adequate === 'Yes' ? 'bg-green-100 text-green-700' : cl.toolsEquipment.adequate === 'No' ? 'bg-red-100 text-red-600' : 'bg-gray-100 text-gray-500'}`}>{cl.toolsEquipment.adequate || '—'}</span>
              : <RadioGroup name="te-adequate" value={cl.toolsEquipment.adequate} onChange={v => setSection('toolsEquipment', 'adequate', v)} options={['Yes', 'No']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Equipment / tools list</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.toolsEquipment.itemsList || '—'}</p>
              : <STextarea value={cl.toolsEquipment.itemsList} onChange={v => setSection('toolsEquipment', 'itemsList', v)} placeholder="List key equipment being brought…" rows={3} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Notes / deficiencies</p>
            {submitted
              ? <p className="text-sm text-gray-500">{cl.toolsEquipment.notes || '—'}</p>
              : <STextarea value={cl.toolsEquipment.notes} onChange={v => setSection('toolsEquipment', 'notes', v)} placeholder="Any gaps or items to source…" />
            }
          </div>
        </div>
      </div>

      {/* ── 3. Calibration Standards ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="3" title="Traceable Calibration Standards Check" sub="Planning stage — verify reference standards are identified and within validity" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Traceable standards identified?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.calibrationStandards.verified === 'Yes' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'}`}>{cl.calibrationStandards.verified || '—'}</span>
              : <RadioGroup name="cs-verified" value={cl.calibrationStandards.verified} onChange={v => setSection('calibrationStandards', 'verified', v)} options={['Yes', 'No']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Certificate validity confirmed?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.calibrationStandards.certValidity === 'Yes' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'}`}>{cl.calibrationStandards.certValidity || '—'}</span>
              : <RadioGroup name="cs-cert" value={cl.calibrationStandards.certValidity} onChange={v => setSection('calibrationStandards', 'certValidity', v)} options={['Yes', 'No']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Standards reference numbers</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.calibrationStandards.standardsRef || '—'}</p>
              : <SInput value={cl.calibrationStandards.standardsRef} onChange={v => setSection('calibrationStandards', 'standardsRef', v)} placeholder="e.g. STD-001, STD-002" />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Traceability reference</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.calibrationStandards.traceabilityRef || '—'}</p>
              : <SInput value={cl.calibrationStandards.traceabilityRef} onChange={v => setSection('calibrationStandards', 'traceabilityRef', v)} placeholder="KEBS / BIPM / NMI reference…" />
            }
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Notes</p>
            {submitted
              ? <p className="text-sm text-gray-500">{cl.calibrationStandards.notes || '—'}</p>
              : <STextarea value={cl.calibrationStandards.notes} onChange={v => setSection('calibrationStandards', 'notes', v)} placeholder="Additional notes…" />
            }
          </div>
        </div>
      </div>

      {/* ── 4. Mass Standards Transport ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="4" title="Mass Standards Transport Coordination Log" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Applicable to this job?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.massTransport.applicable === 'Yes' ? 'bg-amber-100 text-amber-700' : 'bg-gray-100 text-gray-500'}`}>{cl.massTransport.applicable || '—'}</span>
              : <RadioGroup name="mt-applicable" value={cl.massTransport.applicable} onChange={v => setSection('massTransport', 'applicable', v)} options={['Yes', 'No']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Coordination confirmed?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.massTransport.coordinationConfirmed === 'Yes' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>{cl.massTransport.coordinationConfirmed || '—'}</span>
              : <RadioGroup name="mt-coord" value={cl.massTransport.coordinationConfirmed} onChange={v => setSection('massTransport', 'coordinationConfirmed', v)} options={['Yes', 'No', 'N/A']} />
            }
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Vehicle / transport details</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.massTransport.vehicleDetails || '—'}</p>
              : <SInput value={cl.massTransport.vehicleDetails} onChange={v => setSection('massTransport', 'vehicleDetails', v)} placeholder="Vehicle type, plate number, driver…" />
            }
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Notes</p>
            {submitted
              ? <p className="text-sm text-gray-500">{cl.massTransport.notes || '—'}</p>
              : <STextarea value={cl.massTransport.notes} onChange={v => setSection('massTransport', 'notes', v)} placeholder="Handling instructions, special conditions…" />
            }
          </div>
        </div>
      </div>

      {/* ── 5. Unskilled Labour ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="5" title="Unskilled Labour Engagement Tracking" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Unskilled labour required?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.labourEngagement.required === 'Yes' ? 'bg-amber-100 text-amber-700' : 'bg-gray-100 text-gray-500'}`}>{cl.labourEngagement.required || '—'}</span>
              : <RadioGroup name="le-required" value={cl.labourEngagement.required} onChange={v => setSection('labourEngagement', 'required', v)} options={['Yes', 'No']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Number of workers</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.labourEngagement.count || '—'}</p>
              : <SInput value={cl.labourEngagement.count} onChange={v => setSection('labourEngagement', 'count', v)} placeholder="0" type="number" />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Site supervisor name</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.labourEngagement.supervisorName || '—'}</p>
              : <SInput value={cl.labourEngagement.supervisorName} onChange={v => setSection('labourEngagement', 'supervisorName', v)} placeholder="Supervisor name…" />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Notes</p>
            {submitted
              ? <p className="text-sm text-gray-500">{cl.labourEngagement.notes || '—'}</p>
              : <STextarea value={cl.labourEngagement.notes} onChange={v => setSection('labourEngagement', 'notes', v)} placeholder="Duties, safety briefing notes…" />
            }
          </div>
        </div>
      </div>

      {/* ── 6. Site Orientation / Induction ── */}
      <div className="bg-white border border-gray-200 rounded-2xl p-5">
        <SectionHeader number="6" title="Site Orientation / Induction Record" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Site induction completed / planned?</p>
            {submitted
              ? <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${cl.siteOrientation.completed === 'Yes' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'}`}>{cl.siteOrientation.completed || '—'}</span>
              : <RadioGroup name="so-completed" value={cl.siteOrientation.completed} onChange={v => setSection('siteOrientation', 'completed', v)} options={['Yes', 'No', 'N/A']} />
            }
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Conducted by</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.siteOrientation.conductedBy || '—'}</p>
              : <SInput value={cl.siteOrientation.conductedBy} onChange={v => setSection('siteOrientation', 'conductedBy', v)} placeholder="Name / role…" />
            }
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Induction record reference</p>
            {submitted
              ? <p className="text-sm text-gray-700">{cl.siteOrientation.recordRef || '—'}</p>
              : <SInput value={cl.siteOrientation.recordRef} onChange={v => setSection('siteOrientation', 'recordRef', v)} placeholder="Document / form reference number…" />
            }
          </div>
        </div>
      </div>

      {/* General notes */}
      {!submitted && (
        <div className="bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-semibold text-gray-600 mb-2">General Notes</p>
          <STextarea value={notes} onChange={setNotes} placeholder="Any additional pre-deployment notes…" rows={3} />
        </div>
      )}
      {submitted && notes && (
        <div className="bg-white border border-gray-200 rounded-2xl p-5">
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">General Notes</p>
          <p className="text-sm text-gray-700">{notes}</p>
        </div>
      )}

      {/* Actions */}
      {canAct && !submitted && (
        <div className="flex gap-3 justify-end pb-4">
          <button disabled={actionLoading}
            onClick={() => onSave(JSON.stringify(cl), notes, false)}
            className="px-5 py-2.5 rounded-xl border border-gray-300 text-gray-700 text-sm font-semibold hover:bg-gray-50 disabled:opacity-60 transition-colors">
            {actionLoading ? 'Saving…' : 'Save Draft'}
          </button>
          <button disabled={actionLoading}
            onClick={() => onSave(JSON.stringify(cl), notes, true)}
            className="px-5 py-2.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60 transition-colors">
            {actionLoading ? 'Submitting…' : 'Submit Checklist'}
          </button>
        </div>
      )}
    </div>
  )
}

// ── NCR status badge helper ──────────────────────────────────────────────────
const NCR_STATUS = {
  Open:               'bg-red-100 text-red-700',
  UnderInvestigation: 'bg-amber-100 text-amber-700',
  Closed:             'bg-green-100 text-green-700',
}

const NCR_TYPES      = ['Deviation', 'NonConformingWork']
const NCR_CATEGORIES = ['Equipment', 'Process', 'Personnel', 'Environmental', 'Measurement']
const LIKELIHOODS    = ['Low', 'Medium', 'High']
const IMPACTS        = ['Low', 'Medium', 'High']

function SSelect({ value, onChange, options, placeholder }) {
  return (
    <select value={value} onChange={e => onChange(e.target.value)}
      className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white">
      {placeholder && <option value="">{placeholder}</option>}
      {options.map(o => <option key={o} value={o}>{o}</option>)}
    </select>
  )
}

// ── DeviationsTab ────────────────────────────────────────────────────────────
function DeviationsTab({ assignmentId, ncrs, loading, actionLoading, canAct, onCreate, onUpdated, onAction }) {
  const EMPTY_FORM = { type: 'Deviation', category: '', description: '', detectionMethod: '', immediateAction: '', rootCause: '', correctiveAction: '', preventiveAction: '' }
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState(EMPTY_FORM)
  const [expanded, setExpanded] = useState(null)
  const [closeTarget, setCloseTarget] = useState(null)
  const [closureNotes, setClosureNotes] = useState('')
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({})

  const ff = (k, v) => setForm(f => ({ ...f, [k]: v }))

  const handleCreate = async () => {
    if (!form.category || !form.description || !form.detectionMethod) return
    await onCreate(form)
    setForm(EMPTY_FORM)
    setShowCreate(false)
  }

  const handleClose = async () => {
    await onAction(closeTarget, 'close', { closureNotes })
    setCloseTarget(null)
    setClosureNotes('')
  }

  const handleUpdate = async () => {
    await onAction(editTarget, 'update', editForm)
    setEditTarget(null)
    setEditForm({})
  }

  if (loading) return <div className="p-10 text-sm text-gray-400 text-center">Loading deviations…</div>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-bold text-zinc-900">Assessment Deviations & NCRs</h2>
          <p className="text-xs text-gray-500 mt-0.5">Non-conformance reports raised against this assignment</p>
        </div>
        {canAct && (
          <button onClick={() => setShowCreate(v => !v)}
            className="px-4 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold transition-colors">
            + Raise NCR
          </button>
        )}
      </div>

      {showCreate && (
        <div className="bg-white border border-amber-200 rounded-2xl p-5 space-y-4">
          <p className="text-sm font-bold text-zinc-800">New Non-Conformance Report</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Type</label>
              <SSelect value={form.type} onChange={v => ff('type', v)} options={NCR_TYPES} />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Category</label>
              <SSelect value={form.category} onChange={v => ff('category', v)} options={NCR_CATEGORIES} placeholder="Select category…" />
            </div>
            <div className="sm:col-span-2">
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Description of Deviation / Non-Conformance</label>
              <STextarea value={form.description} onChange={v => ff('description', v)} placeholder="Describe what was observed…" rows={3} />
            </div>
            <div className="sm:col-span-2">
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Detection Method / Control</label>
              <SInput value={form.detectionMethod} onChange={v => ff('detectionMethod', v)} placeholder="e.g. Internal audit, measurement check, visual inspection…" />
            </div>
            <div className="sm:col-span-2">
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Immediate Action Taken</label>
              <STextarea value={form.immediateAction} onChange={v => ff('immediateAction', v)} placeholder="Any immediate containment or corrective step taken…" rows={2} />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Root Cause</label>
              <STextarea value={form.rootCause} onChange={v => ff('rootCause', v)} rows={2} />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Corrective Action</label>
              <STextarea value={form.correctiveAction} onChange={v => ff('correctiveAction', v)} rows={2} />
            </div>
            <div className="sm:col-span-2">
              <label className="text-xs font-semibold text-gray-600 mb-1 block">Preventive Action</label>
              <STextarea value={form.preventiveAction} onChange={v => ff('preventiveAction', v)} rows={2} />
            </div>
          </div>
          <div className="flex gap-3 justify-end pt-1">
            <button onClick={() => setShowCreate(false)} className="px-4 py-2 rounded-lg border border-gray-300 text-sm text-gray-600 hover:bg-gray-50">Cancel</button>
            <button disabled={actionLoading || !form.category || !form.description || !form.detectionMethod}
              onClick={handleCreate}
              className="px-5 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60">
              {actionLoading ? 'Saving…' : 'Save NCR'}
            </button>
          </div>
        </div>
      )}

      {ncrs.length === 0 && !showCreate && (
        <div className="bg-white border border-gray-200 rounded-2xl p-10 text-center text-sm text-gray-400">
          No non-conformance reports recorded for this assignment.
        </div>
      )}

      {ncrs.map(ncr => (
        <div key={ncr.id} className="bg-white border border-gray-200 rounded-2xl overflow-hidden">
          <button onClick={() => setExpanded(expanded === ncr.id ? null : ncr.id)}
            className="w-full flex items-center gap-3 px-5 py-4 text-left hover:bg-gray-50 transition-colors">
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="font-mono text-xs font-bold text-amber-600">{ncr.ancrNumber}</span>
                <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${NCR_STATUS[ncr.status] ?? 'bg-gray-100 text-gray-600'}`}>{ncr.status}</span>
                <span className="text-xs text-gray-500">{ncr.type} · {ncr.category}</span>
              </div>
              <p className="text-sm text-zinc-700 mt-0.5 truncate">{ncr.description}</p>
            </div>
            <svg className={`h-4 w-4 text-gray-400 flex-shrink-0 transition-transform ${expanded === ncr.id ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7"/></svg>
          </button>

          {expanded === ncr.id && (
            <div className="px-5 pb-5 border-t border-gray-100 space-y-4 pt-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-3 text-sm">
                {[
                  ['Raised by', ncr.raisedByName],
                  ['Detection method', ncr.detectionMethod],
                  ['Immediate action', ncr.immediateAction],
                  ['Root cause', ncr.rootCause],
                  ['Corrective action', ncr.correctiveAction],
                  ['Preventive action', ncr.preventiveAction],
                  ...(ncr.closedByName ? [['Closed by', ncr.closedByName], ['Closed at', ncr.closedAt ? new Date(ncr.closedAt).toLocaleDateString() : '—'], ['Closure notes', ncr.closureNotes]] : [])
                ].map(([label, val]) => val ? (
                  <div key={label}>
                    <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">{label}</p>
                    <p className="text-gray-800 mt-0.5">{val}</p>
                  </div>
                ) : null)}
              </div>

              {canAct && ncr.status !== 'Closed' && editTarget !== ncr.id && (
                <div className="flex gap-2 flex-wrap pt-1">
                  <button onClick={() => { setEditTarget(ncr.id); setEditForm({ rootCause: ncr.rootCause ?? '', correctiveAction: ncr.correctiveAction ?? '', preventiveAction: ncr.preventiveAction ?? '', status: ncr.status }) }}
                    className="px-3 py-1.5 rounded-lg border border-gray-300 text-xs font-semibold text-gray-600 hover:bg-gray-50">
                    Edit
                  </button>
                  <button onClick={() => setCloseTarget(ncr.id)}
                    className="px-3 py-1.5 rounded-lg bg-green-50 border border-green-300 text-xs font-semibold text-green-700 hover:bg-green-100">
                    Close NCR
                  </button>
                </div>
              )}

              {editTarget === ncr.id && (
                <div className="space-y-3 bg-gray-50 rounded-xl p-4 border border-gray-200">
                  <p className="text-xs font-bold text-gray-700">Update NCR</p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div>
                      <label className="text-xs font-semibold text-gray-600 mb-1 block">Status</label>
                      <SSelect value={editForm.status} onChange={v => setEditForm(f => ({ ...f, status: v }))} options={['Open', 'UnderInvestigation']} />
                    </div>
                    <div>
                      <label className="text-xs font-semibold text-gray-600 mb-1 block">Root Cause</label>
                      <SInput value={editForm.rootCause} onChange={v => setEditForm(f => ({ ...f, rootCause: v }))} />
                    </div>
                    <div>
                      <label className="text-xs font-semibold text-gray-600 mb-1 block">Corrective Action</label>
                      <SInput value={editForm.correctiveAction} onChange={v => setEditForm(f => ({ ...f, correctiveAction: v }))} />
                    </div>
                    <div>
                      <label className="text-xs font-semibold text-gray-600 mb-1 block">Preventive Action</label>
                      <SInput value={editForm.preventiveAction} onChange={v => setEditForm(f => ({ ...f, preventiveAction: v }))} />
                    </div>
                  </div>
                  <div className="flex gap-2 justify-end">
                    <button onClick={() => setEditTarget(null)} className="px-3 py-1.5 rounded-lg border border-gray-300 text-xs text-gray-600 hover:bg-gray-50">Cancel</button>
                    <button disabled={actionLoading} onClick={handleUpdate} className="px-4 py-1.5 rounded-lg bg-amber-500 hover:bg-amber-600 text-black text-xs font-bold disabled:opacity-60">
                      {actionLoading ? 'Saving…' : 'Update'}
                    </button>
                  </div>
                </div>
              )}

              {closeTarget === ncr.id && (
                <div className="space-y-2 bg-green-50 rounded-xl p-4 border border-green-200">
                  <p className="text-xs font-bold text-green-800">Close NCR — {ncr.ancrNumber}</p>
                  <STextarea value={closureNotes} onChange={setClosureNotes} placeholder="Closure notes / verification of effectiveness…" rows={2} />
                  <div className="flex gap-2 justify-end">
                    <button onClick={() => setCloseTarget(null)} className="px-3 py-1.5 rounded-lg border border-gray-300 text-xs text-gray-600 hover:bg-gray-50">Cancel</button>
                    <button disabled={actionLoading} onClick={handleClose} className="px-4 py-1.5 rounded-lg bg-green-600 hover:bg-green-700 text-white text-xs font-bold disabled:opacity-60">
                      {actionLoading ? 'Closing…' : 'Confirm Close'}
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}

// ── Risk rating helper ───────────────────────────────────────────────────────
const RISK_SCORE = { Low: 1, Medium: 2, High: 3 }
function riskLevel(likelihood, impact) {
  const score = RISK_SCORE[likelihood] * RISK_SCORE[impact]
  if (score >= 6) return { label: 'High', cls: 'bg-red-100 text-red-700' }
  if (score >= 3) return { label: 'Medium', cls: 'bg-amber-100 text-amber-700' }
  return { label: 'Low', cls: 'bg-green-100 text-green-700' }
}

function StarRating({ value, onChange, max = 5 }) {
  return (
    <div className="flex gap-1">
      {Array.from({ length: max }, (_, i) => i + 1).map(n => (
        <button key={n} type="button" onClick={() => onChange && onChange(n)}
          className={`text-xl transition-colors ${n <= value ? 'text-amber-400' : 'text-gray-300'} ${onChange ? 'hover:text-amber-300 cursor-pointer' : 'cursor-default'}`}>
          ★
        </button>
      ))}
    </div>
  )
}

// ── FeedbackTab ──────────────────────────────────────────────────────────────
function FeedbackTab({ assignmentId, risks, customerFeedback, loading, actionLoading, canAct, onRiskAction, onSaveFeedback }) {
  const EMPTY_RISK = { description: '', likelihood: 'Low', impact: 'Low', mitigation: '', owner: '' }
  const [showRiskForm, setShowRiskForm] = useState(false)
  const [riskForm, setRiskForm] = useState(EMPTY_RISK)
  const [fbForm, setFbForm] = useState({ overallRating: 0, timelinessRating: null, qualityRating: null, professionalismRating: null, wouldRecommend: null, comments: '' })
  const [fbEditing, setFbEditing] = useState(false)
  const rf = (k, v) => setRiskForm(f => ({ ...f, [k]: v }))

  const handleCreateRisk = async () => {
    if (!riskForm.description) return
    const result = await onRiskAction(null, 'create', riskForm)
    if (result) {
      setRiskForm(EMPTY_RISK)
      setShowRiskForm(false)
    }
  }

  const handleDeleteRisk = async (riskId) => {
    await onRiskAction(riskId, 'delete', null)
  }

  const handleSaveFeedback = async () => {
    await onSaveFeedback(fbForm)
    setFbEditing(false)
  }

  if (loading) return <div className="p-10 text-sm text-gray-400 text-center">Loading feedback data…</div>

  return (
    <div className="space-y-6">

      {/* ── Risk Register ── */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-zinc-900">Risk Register</h2>
            <p className="text-xs text-gray-500 mt-0.5">Risks to impartiality and job-specific risks identified for this assignment</p>
          </div>
          {canAct && (
            <button onClick={() => setShowRiskForm(v => !v)}
              className="px-4 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold transition-colors">
              + Add Risk
            </button>
          )}
        </div>

        {showRiskForm && (
          <div className="bg-white border border-amber-200 rounded-2xl p-5 space-y-4">
            <p className="text-sm font-bold text-zinc-800">New Risk Entry</p>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div className="sm:col-span-3">
                <label className="text-xs font-semibold text-gray-600 mb-1 block">Risk Description</label>
                <STextarea value={riskForm.description} onChange={v => rf('description', v)} placeholder="Describe the risk…" rows={2} />
              </div>
              <div>
                <label className="text-xs font-semibold text-gray-600 mb-1 block">Likelihood</label>
                <SSelect value={riskForm.likelihood} onChange={v => rf('likelihood', v)} options={LIKELIHOODS} />
              </div>
              <div>
                <label className="text-xs font-semibold text-gray-600 mb-1 block">Impact</label>
                <SSelect value={riskForm.impact} onChange={v => rf('impact', v)} options={IMPACTS} />
              </div>
              <div>
                <label className="text-xs font-semibold text-gray-600 mb-1 block">Risk Owner</label>
                <SInput value={riskForm.owner} onChange={v => rf('owner', v)} placeholder="Name or role…" />
              </div>
              <div className="sm:col-span-3">
                <label className="text-xs font-semibold text-gray-600 mb-1 block">Mitigation / Control</label>
                <STextarea value={riskForm.mitigation} onChange={v => rf('mitigation', v)} placeholder="Describe the control measure or mitigation plan…" rows={2} />
              </div>
            </div>
            <div className="flex gap-3 justify-end">
              <button onClick={() => setShowRiskForm(false)} className="px-4 py-2 rounded-lg border border-gray-300 text-sm text-gray-600 hover:bg-gray-50">Cancel</button>
              <button disabled={actionLoading || !riskForm.description}
                onClick={handleCreateRisk}
                className="px-5 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60">
                {actionLoading ? 'Saving…' : 'Add Risk'}
              </button>
            </div>
          </div>
        )}

        {risks.length === 0 && !showRiskForm ? (
          <div className="bg-white border border-gray-200 rounded-2xl p-10 text-center text-sm text-gray-400">
            No risks recorded for this assignment.
          </div>
        ) : (
          <div className="bg-white border border-gray-200 rounded-2xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Risk</th>
                  <th className="text-center px-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide w-24">Likelihood</th>
                  <th className="text-center px-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide w-20">Impact</th>
                  <th className="text-center px-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide w-20">Level</th>
                  <th className="text-left px-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Mitigation</th>
                  <th className="text-left px-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide w-28">Owner</th>
                  {canAct && <th className="w-10" />}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {risks.map(r => {
                  const { label, cls } = riskLevel(r.likelihood, r.impact)
                  return (
                    <tr key={r.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 text-gray-800">{r.description}</td>
                      <td className="px-3 py-3 text-center text-xs font-medium text-gray-600">{r.likelihood}</td>
                      <td className="px-3 py-3 text-center text-xs font-medium text-gray-600">{r.impact}</td>
                      <td className="px-3 py-3 text-center"><span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${cls}`}>{label}</span></td>
                      <td className="px-3 py-3 text-gray-600 text-xs">{r.mitigation || '—'}</td>
                      <td className="px-3 py-3 text-gray-600 text-xs">{r.owner || '—'}</td>
                      {canAct && (
                        <td className="px-3 py-3">
                          <button onClick={() => handleDeleteRisk(r.id)} className="text-gray-300 hover:text-red-400 transition-colors text-base leading-none">✕</button>
                        </td>
                      )}
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── Customer Feedback ── */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-zinc-900">Customer Satisfaction</h2>
            <p className="text-xs text-gray-500 mt-0.5">Post-service feedback captured from the client</p>
          </div>
          {canAct && !fbEditing && (
            <button onClick={() => {
              if (customerFeedback) {
                setFbForm({
                  overallRating: customerFeedback.overallRating,
                  timelinessRating: customerFeedback.timelinessRating,
                  qualityRating: customerFeedback.qualityRating,
                  professionalismRating: customerFeedback.professionalismRating,
                  wouldRecommend: customerFeedback.wouldRecommend,
                  comments: customerFeedback.comments ?? '',
                })
              }
              setFbEditing(true)
            }}
              className="px-4 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold transition-colors">
              {customerFeedback ? 'Edit Feedback' : 'Record Feedback'}
            </button>
          )}
        </div>

        {!fbEditing && !customerFeedback && (
          <div className="bg-white border border-gray-200 rounded-2xl p-10 text-center text-sm text-gray-400">
            No customer feedback recorded yet.
          </div>
        )}

        {!fbEditing && customerFeedback && (
          <div className="bg-white border border-gray-200 rounded-2xl p-6 space-y-5">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
              {[
                ['Overall Rating', customerFeedback.overallRating],
                ['Timeliness', customerFeedback.timelinessRating],
                ['Quality of Work', customerFeedback.qualityRating],
                ['Professionalism', customerFeedback.professionalismRating],
              ].map(([label, val]) => (
                <div key={label}>
                  <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">{label}</p>
                  {val != null ? <StarRating value={val} max={5} /> : <p className="text-sm text-gray-400">Not rated</p>}
                </div>
              ))}
            </div>
            <div className="flex items-center gap-2 pt-1">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Would Recommend</p>
              {customerFeedback.wouldRecommend === true && <span className="px-2 py-0.5 rounded-full bg-green-100 text-green-700 text-xs font-semibold">Yes</span>}
              {customerFeedback.wouldRecommend === false && <span className="px-2 py-0.5 rounded-full bg-red-100 text-red-700 text-xs font-semibold">No</span>}
              {customerFeedback.wouldRecommend == null && <span className="text-sm text-gray-400">Not answered</span>}
            </div>
            {customerFeedback.comments && (
              <div>
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">Comments</p>
                <p className="text-sm text-gray-700 bg-gray-50 rounded-xl p-3">{customerFeedback.comments}</p>
              </div>
            )}
            {customerFeedback.capturedByName && (
              <p className="text-xs text-gray-400">Captured by {customerFeedback.capturedByName}</p>
            )}
          </div>
        )}

        {fbEditing && (
          <div className="bg-white border border-amber-200 rounded-2xl p-6 space-y-5">
            <p className="text-sm font-bold text-zinc-800">Customer Satisfaction Form</p>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
              {[
                ['Overall Rating *', 'overallRating'],
                ['Timeliness', 'timelinessRating'],
                ['Quality of Work', 'qualityRating'],
                ['Professionalism', 'professionalismRating'],
              ].map(([label, key]) => (
                <div key={key}>
                  <p className="text-xs font-semibold text-gray-600 mb-2">{label}</p>
                  <StarRating value={fbForm[key] ?? 0} onChange={v => setFbForm(f => ({ ...f, [key]: v }))} max={5} />
                </div>
              ))}
            </div>
            <div>
              <p className="text-xs font-semibold text-gray-600 mb-2">Would you recommend our services?</p>
              <div className="flex gap-3">
                {[true, false].map(v => (
                  <button key={String(v)} type="button"
                    onClick={() => setFbForm(f => ({ ...f, wouldRecommend: v }))}
                    className={`px-4 py-2 rounded-lg text-sm font-semibold border transition-colors ${fbForm.wouldRecommend === v ? (v ? 'bg-green-500 text-white border-green-500' : 'bg-red-500 text-white border-red-500') : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}>
                    {v ? 'Yes' : 'No'}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <p className="text-xs font-semibold text-gray-600 mb-1">Comments / Additional Feedback</p>
              <STextarea value={fbForm.comments} onChange={v => setFbForm(f => ({ ...f, comments: v }))} placeholder="Any other feedback from the client…" rows={3} />
            </div>
            <div className="flex gap-3 justify-end pt-1">
              <button onClick={() => setFbEditing(false)} className="px-4 py-2 rounded-lg border border-gray-300 text-sm text-gray-600 hover:bg-gray-50">Cancel</button>
              <button disabled={actionLoading || !fbForm.overallRating}
                onClick={handleSaveFeedback}
                className="px-5 py-2 rounded-xl bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold disabled:opacity-60">
                {actionLoading ? 'Saving…' : 'Save Feedback'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
