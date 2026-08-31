import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import { exportProjectDetailPDF, exportProjectDetailExcel } from '../../utils/projectExports.js'
import ProjectComplianceCard from '../../components/operations/detail/ProjectComplianceCard.jsx'
import VariationOrdersTab from '../../components/operations/variations/VariationOrdersTab.jsx'
import HandoverTab from '../../components/operations/handover/HandoverTab.jsx'
import ScheduleTab from '../../components/operations/schedule/ScheduleTab.jsx'
import TaskDependencies from '../../components/operations/schedule/TaskDependencies.jsx'
import CommercialsTab from '../../components/operations/budget/CommercialsTab.jsx'
import GovernanceTab from '../../components/operations/governance/GovernanceTab.jsx'
import CommentThread from '../../components/operations/comments/CommentThread.jsx'
import EarnedValueTab from '../../components/operations/analytics/EarnedValueTab.jsx'
import KanbanBoard from '../../components/operations/board/KanbanBoard.jsx'
import ProjectCalendar from '../../components/operations/board/ProjectCalendar.jsx'
import ContractCard from '../../components/operations/detail/ContractCard.jsx'
import MilestoneSignOffModal from '../../components/operations/detail/MilestoneSignOffModal.jsx'
import MilestoneUpdateModal from '../../components/operations/detail/MilestoneUpdateModal.jsx'
import { signOffMilestone as apiSignOffMilestone, addMilestoneUpdate as apiAddMilestoneUpdate } from '../../services/operations.js'
import * as opsApi from '../../services/operations.js'

const API_BASE = import.meta.env.VITE_API_URL || 'https://kmk.support.qalibrated.co.ke'

async function uploadAttachments(files, entityType, entityId) {
  for (const file of files) {
    const fd = new FormData()
    fd.append('file', file)
    fd.append('entityType', entityType)
    fd.append('entityId', entityId)
    await api.post('/api/v1/attachments', fd).catch(() => {})
  }
}

const STATUS_BADGE = {
  Draft: 'bg-gray-100 text-gray-600',
  Planning: 'bg-amber-100 text-amber-700',
  PendingMdApproval: 'bg-orange-100 text-orange-700',
  PendingFinanceApproval: 'bg-orange-100 text-orange-700',
  Active: 'bg-green-100 text-green-700',
  OnHold: 'bg-red-100 text-red-600',
  Completed: 'bg-indigo-100 text-indigo-700',
  Closed: 'bg-gray-200 text-gray-600',
  Cancelled: 'bg-red-200 text-red-700',
}

const MILESTONE_BADGE = {
  Pending: 'bg-gray-100 text-gray-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Completed: 'bg-green-100 text-green-700',
  Approved: 'bg-indigo-100 text-indigo-700',
  Rejected: 'bg-red-100 text-red-600',
}

const TASK_BADGE = {
  NotStarted: 'bg-gray-100 text-gray-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Done: 'bg-green-100 text-green-700',
  Blocked: 'bg-red-100 text-red-600',
}

export default function ProjectDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { hasPermission, user } = useAuth()
  const canWrite = hasPermission('projects.write')
  const permApproveMd = hasPermission('projects.approve')
  const permApproveFinance = hasPermission('finance.approve')

  const [project, setProject] = useState(null)
  const [milestones, setMilestones] = useState([])
  const [budget, setBudget] = useState(null)
  const [resources, setResources] = useState([])
  const [attachments, setAttachments] = useState([])
  const [lightboxIdx, setLightboxIdx] = useState(null)
  const [tab, setTab] = useState('overview')
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState(false)
  const [expandedMilestone, setExpandedMilestone] = useState(null)
  const [milestoneTasks, setMilestoneTasks] = useState({})

  const [showAddMilestone, setShowAddMilestone] = useState(false)
  const [signOffMs, setSignOffMs] = useState(null)   // O11.1 — milestone sign-off modal
  const [updateMs, setUpdateMs]   = useState(null)   // O11.1 — milestone daily-update modal
  const [showAddTask, setShowAddTask] = useState(null)
  const [taskDeps, setTaskDeps] = useState([])
  const [taskErr, setTaskErr] = useState('')
  const [taskSaving, setTaskSaving] = useState(false)
  const [showAddCostEntry, setShowAddCostEntry] = useState(false)
  const [showReview, setShowReview] = useState(null)
  const [reviewForm, setReviewForm] = useState({ approved: true, comments: '' })
  const [milestoneForm, setMilestoneForm] = useState({ title: '', description: '', plannedCompletionDate: '', plannedAmount: '' })
  const [taskForm, setTaskForm] = useState({ title: '', description: '', assignedToUserId: '', parentTaskId: '', startDate: '', estimatedHours: '' })
  const [costEntryForm, setCostEntryForm] = useState({ milestoneId: '', budgetLineId: '', category: 'Labour', description: '', amount: '', notes: '' })
  const [pendingFiles, setPendingFiles] = useState([])
  const [users, setUsers] = useState([])
  const [showDispatchModal, setShowDispatchModal] = useState(null)
  const [dispatchTechId, setDispatchTechId] = useState('')
  const [dispatchNotes, setDispatchNotes] = useState('')
  const [dispatchLoading, setDispatchLoading] = useState(false)
  const [exporting, setExporting] = useState(false)

  const fetchProject = useCallback(async () => {
    try {
      const [projRes, msRes, budRes, resRes, attRes] = await Promise.all([
        api.get(`/api/v1/projects/${id}`),
        api.get(`/api/v1/projects/${id}/milestones`),
        api.get(`/api/v1/projects/${id}/budget`).catch(() => ({ data: { data: null } })),
        api.get(`/api/v1/projects/${id}/resources`).catch(() => ({ data: { data: [] } })),
        api.get(`/api/v1/attachments?entityType=Project&entityId=${id}`).catch(() => ({ data: { data: [] } })),
      ])
      setProject(projRes.data?.data)
      setMilestones(msRes.data?.data ?? [])
      setBudget(budRes.data?.data)
      setResources(resRes.data?.data ?? [])
      setAttachments(attRes.data?.data ?? [])
    } catch {
      setProject(null)
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => { fetchProject() }, [fetchProject])

  useEffect(() => {
    api.get('/api/v1/users?pageSize=200').then(res => {
      const data = res.data?.data
      setUsers(Array.isArray(data) ? data : (data?.items ?? []))
    }).catch(() => {})
  }, [])

  const getUserName = (u) => [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email || u.id
  const usersMap = Object.fromEntries(users.map(u => [u.id, getUserName(u)]))
  const mentionUsers = users.map(u => ({ id: u.id, name: getUserName(u) }))

  const loadTaskDeps = useCallback(async () => {
    try { setTaskDeps(await opsApi.getTaskDependencies(id) ?? []) }
    catch { /* ordering is supplementary — a failure must not blank the milestone list */ }
  }, [id])

  const loadTasks = async (milestoneId) => {
    if (milestoneTasks[milestoneId]) return
    try {
      const res = await api.get(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`)
      setMilestoneTasks(prev => ({ ...prev, [milestoneId]: res.data?.data ?? [] }))
    } catch {
      setMilestoneTasks(prev => ({ ...prev, [milestoneId]: [] }))
    }
  }

  const toggleMilestone = (milestoneId) => {
    if (expandedMilestone === milestoneId) {
      setExpandedMilestone(null)
    } else {
      setExpandedMilestone(milestoneId)
      loadTasks(milestoneId)
      loadTaskDeps()
    }
  }

  const doAction = async (action, body) => {
    setActionLoading(true)
    try {
      if (action === 'submit') await api.post(`/api/v1/projects/${id}/submit`)
      else if (action === 'activate') await api.post(`/api/v1/projects/${id}/activate`)   // O11.1 — MD-activation gate
      else if (action === 'hold') await api.post(`/api/v1/projects/${id}/hold`, JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } })
      else if (action === 'resume') await api.post(`/api/v1/projects/${id}/resume`)
      else if (action === 'close') await api.post(`/api/v1/projects/${id}/close`)
      else if (action === 'approve-md') await api.post(`/api/v1/projects/${id}/approve/md`, body)
      else if (action === 'approve-finance') await api.post(`/api/v1/projects/${id}/approve/finance`, body)
      await fetchProject()
    } finally {
      setActionLoading(false)
    }
  }

  // O11.1 — milestone sign-off + daily update (service layer), then refresh.
  const doSignOffMilestone = async (milestoneId, dto) => { await apiSignOffMilestone(id, milestoneId, dto); await fetchProject() }
  const doUpdateMilestone  = async (milestoneId, dto) => { await apiAddMilestoneUpdate(id, milestoneId, dto); await fetchProject() }

  const handleAddMilestone = async (e) => {
    e.preventDefault()
    try {
      const payload = {
        title: milestoneForm.title,
        description: milestoneForm.description || null,
        order: milestones.length,
        dueDate: milestoneForm.plannedCompletionDate
          ? new Date(milestoneForm.plannedCompletionDate).toISOString()
          : new Date().toISOString(),
        plannedAmount: milestoneForm.plannedAmount !== '' ? parseFloat(milestoneForm.plannedAmount) : null,
      }
      const res = await api.post(`/api/v1/projects/${id}/milestones`, payload)
      await uploadAttachments(pendingFiles, 'Milestone', res.data?.data?.id ?? id)
      setShowAddMilestone(false)
      setMilestoneForm({ title: '', description: '', plannedCompletionDate: '', plannedAmount: '' })
      setPendingFiles([])
      fetchProject()
    } catch { }
  }

  const handleAddTask = async (e) => {
    e.preventDefault()
    const milestoneId = showAddTask
    setTaskSaving(true)
    setTaskErr('')
    try {
      const res = await api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`, {
        title: taskForm.title,
        description: taskForm.description || null,
        assignedToUserId: taskForm.assignedToUserId || null,
        parentTaskId: taskForm.parentTaskId || null,
        startDate: taskForm.startDate || null,
        estimatedHours: taskForm.estimatedHours === '' ? null : Number(taskForm.estimatedHours),
      })
      const newTask = res.data?.data ?? { title: taskForm.title, status: 'NotStarted' }
      setMilestoneTasks(prev => ({ ...prev, [milestoneId]: [...(prev[milestoneId] ?? []), newTask] }))
      setShowAddTask(null)
      setTaskForm({ title: '', description: '', assignedToUserId: '', parentTaskId: '', startDate: '', estimatedHours: '' })
      setPendingFiles([])
    } catch (err) {
      setTaskErr(err.response?.data?.title ?? err.response?.data?.message ?? 'Failed to add task.')
    } finally {
      setTaskSaving(false)
    }
  }

  const handleAddCostEntry = async (e) => {
    e.preventDefault()
    try {
      await api.post(`/api/v1/projects/${id}/budget/costs`, {
        projectId: id,
        milestoneId: costEntryForm.milestoneId || null,
        budgetLineId: costEntryForm.budgetLineId || null,
        category: costEntryForm.category,
        description: costEntryForm.description,
        amount: parseFloat(costEntryForm.amount) || 0,
        notes: costEntryForm.notes || null,
      })
      setShowAddCostEntry(false)
      setCostEntryForm({ milestoneId: '', budgetLineId: '', category: 'Labour', description: '', amount: '', notes: '' })
      fetchProject()
    } catch { }
  }

  const handleReview = async (e) => {
    e.preventDefault()
    await doAction(`approve-${showReview.type}`, reviewForm)
    setShowReview(null)
    setReviewForm({ approved: true, comments: '' })
  }

  const handleDispatch = async () => {
    if (!showDispatchModal) return
    const { taskId, milestoneId } = showDispatchModal
    setDispatchLoading(true)
    try {
      const techName = dispatchTechId ? getUserName(users.find(u => u.id === dispatchTechId) ?? {}) : null
      await api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks/${taskId}/dispatch`, {
        assignedToUserId: dispatchTechId || null,
        assignedToUserName: techName,
        notes: dispatchNotes || null,
      })
      const res = await api.get(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`)
      setMilestoneTasks(prev => ({ ...prev, [milestoneId]: res.data?.data ?? [] }))
      setShowDispatchModal(null)
      setDispatchNotes('')
      setDispatchTechId('')
    } catch (err) {
      alert(err?.response?.data?.message ?? 'Dispatch failed. Please try again.')
    } finally {
      setDispatchLoading(false)
    }
  }

  const handleExportPDF = async () => {
    setExporting(true)
    try { await exportProjectDetailPDF(project, milestones, milestoneTasks, budget, resources) }
    finally { setExporting(false) }
  }

  if (loading) return (
      <>
        <div className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
          <div className="space-y-4">{[1,2,3].map(i => <div key={i} className="h-24 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
        </div>
      </>
  )

  if (!project) return (
      <>
        <div className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm">Project not found.</div>
        </div>
      </>
  )

  const canSubmit = project.status === 'Draft'
  const canActivate = project.status === 'Planning'   // O11.1 — approved → Active
  const canHold = project.status === 'Active'
  const canResume = project.status === 'OnHold'
  const canClose = project.status === 'Active' || project.status === 'OnHold'
  const canApproveMd = project.status === 'PendingMdApproval'
  const canApproveFinance = project.status === 'PendingFinanceApproval'

  const TABS = [
    ['overview', 'Overview'],
    ['milestones', 'Milestones'],
    ['schedule', 'Schedule'],
    ['board', 'Board'],
    ['calendar', 'Calendar'],
    ['budget', 'Budget'],
    ['variations', 'Variations'],
    ['earnedvalue', 'Earned Value'],
    ['governance', 'Governance'],
    ['discussion', 'Discussion'],
    ['handover', 'Handover'],
    ['resources', 'Resources'],
    ['attachments', attachments.length > 0 ? `Attachments (${attachments.length})` : 'Attachments'],
  ]

  return (
      <>
        <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
          {/* Back */}
          <button
              onClick={() => navigate('/modules/operations/projects')}
              className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-800 mb-5 transition-colors"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Projects
          </button>

          {/* Header */}
          <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-2xl font-extrabold text-zinc-950">{project.name}</h1>
                <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[project.status] ?? 'bg-gray-100 text-gray-600'}`}>
                {project.status}
              </span>
              </div>
              <p className="text-sm text-gray-500 mt-1">
                {project.clientName ?? 'No client'} · {project.type} · {project.riskLevel} Risk
              </p>
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              <button onClick={handleExportPDF} disabled={exporting}
                      className="inline-flex items-center gap-1.5 px-3.5 py-2 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors disabled:opacity-50">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 2v6h6M16 13H8M16 17H8M10 9H8"/></svg>
                {exporting ? 'Generating…' : 'PDF'}
              </button>
              <button onClick={() => exportProjectDetailExcel(project, milestones, milestoneTasks, budget, resources)}
                      className="inline-flex items-center gap-1.5 px-3.5 py-2 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><rect x="3" y="3" width="18" height="18" rx="2"/><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 3v18M3 9h18"/></svg>
                Excel
              </button>
              {canSubmit && canWrite && (
                  <button onClick={() => doAction('submit')} disabled={actionLoading}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white disabled:opacity-50 transition-colors">
                    Submit for Approval
                  </button>
              )}
              {canApproveMd && permApproveMd && (
                  <button onClick={() => setShowReview({ type: 'md' })}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-green-500 hover:bg-green-600 text-white transition-colors">
                    Approve (MD)
                  </button>
              )}
              {canApproveFinance && permApproveFinance && (
                  <button onClick={() => setShowReview({ type: 'finance' })}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-green-500 hover:bg-green-600 text-white transition-colors">
                    Approve (Finance)
                  </button>
              )}
              {canActivate && permApproveMd && (
                  <button onClick={() => doAction('activate')} disabled={actionLoading}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-green-600 hover:bg-green-700 text-white disabled:opacity-50 transition-colors">
                    Activate Project
                  </button>
              )}
              {canHold && canWrite && (
                  <button onClick={() => doAction('hold', 'Placed on hold')} disabled={actionLoading}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-red-500 hover:bg-red-600 text-white disabled:opacity-50 transition-colors">
                    Put on Hold
                  </button>
              )}
              {canResume && canWrite && (
                  <button onClick={() => doAction('resume')} disabled={actionLoading}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-green-500 hover:bg-green-600 text-white disabled:opacity-50 transition-colors">
                    Resume
                  </button>
              )}
              {canClose && (canWrite || permApproveMd) && (
                  <button onClick={() => doAction('close')} disabled={actionLoading}
                          className="px-4 py-2 text-sm font-semibold rounded-lg bg-gray-700 hover:bg-gray-800 text-white disabled:opacity-50 transition-colors">
                    Close Project
                  </button>
              )}
            </div>
          </div>

          {/* Tabs */}
          <div className="flex gap-0 border-b border-gray-200 mb-6 overflow-x-auto">
            {TABS.map(([tid, tlabel]) => (
                <button
                    key={tid}
                    onClick={() => setTab(tid)}
                    className={`px-5 py-2.5 text-sm font-semibold border-b-2 -mb-px whitespace-nowrap transition-colors ${
                        tab === tid
                            ? 'border-amber-500 text-amber-600'
                            : 'border-transparent text-gray-500 hover:text-gray-700'
                    }`}
                >
                  {tlabel}
                </button>
            ))}
          </div>

          {/* Overview Tab */}
          {tab === 'overview' && (
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <InfoCard title="Project Info">
                  <InfoRow label="Reference" value={project.tenderReference ?? '—'} />
                  <InfoRow label="Scope" value={project.scopeSummary ?? '—'} />
                  {project.clientId
                    ? <InfoRow label="CRM Client" value={
                        <button onClick={() => navigate(`/modules/crm/customers/${project.clientId}`)}
                                className="text-navy hover:underline font-medium">
                          {project.clientName ?? project.clientId.slice(0, 8)} ↗
                        </button>
                      } />
                    : project.clientName && <InfoRow label="Client" value={project.clientName} />}
                  <InfoRow label="Process Owner" value={project.processOwnerId ?? '—'} />
                  <InfoRow label="Start Date" value={project.startDate ? new Date(project.startDate).toLocaleDateString() : '—'} />
                  <InfoRow label="Expected End" value={project.expectedEndDate ? new Date(project.expectedEndDate).toLocaleDateString() : '—'} />
                  {project.activatedAt && <InfoRow label="Activated" value={new Date(project.activatedAt).toLocaleDateString()} />}
                  {project.actualEndDate && <InfoRow label="Actual End" value={new Date(project.actualEndDate).toLocaleDateString()} />}
                </InfoCard>
                <InfoCard title="Financials">
                  <InfoRow label="Contract Value" value={`KES ${(project.contractValue ?? 0).toLocaleString()}`} />
                  <InfoRow label="Planned Budget" value={`KES ${(project.plannedBudget ?? 0).toLocaleString()}`} />
                  <InfoRow label="Approved Baseline" value={
                    project.baselineBudget != null
                      ? `KES ${project.baselineBudget.toLocaleString()}`
                      : <span className="text-amber-700">not set — approve a budget to baseline</span>} />
                  <InfoRow label="Actual Spend" value={`KES ${(project.actualSpend ?? 0).toLocaleString()}`} />
                  <InfoRow label="Milestones" value={project.milestoneCount ?? milestones.length} />
                  <InfoRow label="Tasks" value={project.taskCount ?? '—'} />
                </InfoCard>
                {/* O11.1 — budget burn + alert log + HSE summary */}
                <div className="lg:col-span-2">
                  <ProjectComplianceCard project={project} />
                </div>
                {project.description && (
                    <div className="lg:col-span-2">
                      <InfoCard title="Description">
                        <p className="text-sm text-gray-700 leading-relaxed">{project.description}</p>
                      </InfoCard>
                    </div>
                )}
                <div className="lg:col-span-2">
                  <ContractCard
                      projectId={id}
                      contractAttachmentId={project.contractAttachmentId}
                      canWrite={canWrite}
                      onUploaded={fetchProject}
                  />
                </div>
                {attachments.length > 0 && (
                    <div className="lg:col-span-2">
                      <div className="bg-white rounded-xl border border-gray-200 p-5">
                        <p className="text-sm font-bold text-gray-700 uppercase tracking-wider mb-3">Project Photos</p>
                        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                          {attachments.map((a, i) => (
                              <button
                                  key={a.id}
                                  onClick={() => setLightboxIdx(i)}
                                  className="block rounded-lg overflow-hidden border border-gray-100 hover:shadow-md transition-shadow focus:outline-none"
                              >
                                <img
                                    src={`${API_BASE}${a.url}`}
                                    alt={a.fileName ?? 'Attachment'}
                                    className="w-full h-32 object-cover"
                                    onError={e => { e.target.parentElement.style.display = 'none' }}
                                />
                              </button>
                          ))}
                        </div>
                      </div>
                    </div>
                )}
              </div>
          )}

          {/* Milestones Tab */}
          {tab === 'milestones' && (
              <div>
                {canWrite && (
                    <div className="flex justify-end mb-4">
                      <button onClick={() => setShowAddMilestone(true)}
                              className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors">
                        + Add Milestone
                      </button>
                    </div>
                )}
                {milestones.length === 0 ? (
                    <EmptyState icon="🏁" title="No milestones yet" sub="Add the first milestone to track project progress." />
                ) : (
                    <div className="space-y-3">
                      {milestones.map(m => (
                          <div key={m.id} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                            <button
                                onClick={() => toggleMilestone(m.id)}
                                className="w-full flex items-center justify-between px-5 py-4 text-left hover:bg-amber-50 transition-colors"
                            >
                              <div className="flex items-center gap-3 min-w-0">
                                <svg className={`w-4 h-4 text-gray-400 shrink-0 transition-transform ${expandedMilestone === m.id ? 'rotate-90' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                </svg>
                                <div className="min-w-0">
                                  <p className="text-sm font-semibold text-gray-900 truncate">{m.title}</p>
                                  {m.plannedCompletionDate && (
                                      <p className="text-xs text-gray-400 mt-0.5">
                                        Due {new Date(m.plannedCompletionDate).toLocaleDateString()}
                                        {m.actualCompletionDate && ` · Completed ${new Date(m.actualCompletionDate).toLocaleDateString()}`}
                                      </p>
                                  )}
                                </div>
                              </div>
                              <span className={`shrink-0 px-2 py-0.5 rounded-full text-xs font-semibold ml-3 ${MILESTONE_BADGE[m.status] ?? 'bg-gray-100 text-gray-600'}`}>
                        {m.status}
                      </span>
                            </button>

                            {expandedMilestone === m.id && (
                                <div className="border-t border-gray-100 px-5 py-4">
                                  {m.description && <p className="text-sm text-gray-600 mb-3">{m.description}</p>}
                                  {/* O11.1 — milestone meta + sign-off / update actions */}
                                  <div className="flex flex-wrap items-center gap-2 mb-4 text-xs">
                                    <span className="px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">{m.progressPct ?? 0}% complete</span>
                                    {m.isBillable && <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 font-medium">Billable</span>}
                                    {m.ldApplies && m.ldAmount > 0 && <span className="px-2 py-0.5 rounded-full bg-red-100 text-red-700 font-medium">LD {Number(m.ldAmount).toLocaleString()}</span>}
                                    {m.signOffAt
                                      ? <span className="px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium">Signed off · {m.signOffBy}</span>
                                      : m.lastUpdatedAt && <span className="text-gray-400">Updated {new Date(m.lastUpdatedAt).toLocaleDateString()}</span>}
                                    {canWrite && !m.signOffAt && (
                                        <div className="ml-auto flex gap-3">
                                          <button onClick={() => setUpdateMs(m)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">Log Update</button>
                                          <button onClick={() => setSignOffMs(m)} className="text-xs font-semibold text-green-600 hover:text-green-800">Sign Off</button>
                                        </div>
                                    )}
                                  </div>
                                  <div className="flex items-center justify-between mb-3">
                                    <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Tasks</p>
                                    {canWrite && (
                                        <button onClick={() => setShowAddTask(m.id)}
                                                className="text-xs font-semibold text-amber-600 hover:text-amber-800 transition-colors">
                                          + Add Task
                                        </button>
                                    )}
                                  </div>
                                  {!milestoneTasks[m.id] ? (
                                      <p className="text-sm text-gray-400 animate-pulse">Loading tasks…</p>
                                  ) : milestoneTasks[m.id].length === 0 ? (
                                      <p className="text-sm text-gray-400">No tasks yet.</p>
                                  ) : (
                                      <div className="space-y-2">
                                        {/* Parents first, each followed by its own subtasks — the list mirrors
                                            the one-level nesting the API enforces. */}
                                        {[...milestoneTasks[m.id].filter(t => !t.parentTaskId),
                                          ...milestoneTasks[m.id].filter(t => t.parentTaskId && !milestoneTasks[m.id].some(p => p.id === t.parentTaskId))]
                                          .flatMap(parent => [parent, ...milestoneTasks[m.id].filter(c => c.parentTaskId === parent.id)])
                                          .map(t => (
                                            <div
                                                key={t.id}
                                                onClick={() => t.linkedAssignmentId && navigate(`/modules/operations/assignments/${t.linkedAssignmentId}`)}
                                                className={`flex items-center justify-between p-3 bg-gray-50 rounded-lg gap-3 ${t.parentTaskId ? 'ml-6 border-l-2 border-gray-200' : ''} ${t.linkedAssignmentId ? 'cursor-pointer hover:bg-amber-50 transition-colors' : ''}`}
                                            >
                                              <div className="min-w-0 flex-1">
                                                <p className={`text-sm font-medium truncate ${t.linkedAssignmentId ? 'text-amber-600' : 'text-gray-900'}`}>
                                                  {t.parentTaskId && <span className="text-gray-300 mr-1">↳</span>}{t.title}
                                                </p>
                                                <div className="flex flex-wrap items-center gap-2 mt-0.5">
                                                  {t.assignedToUserId && (
                                                      <span className="text-xs text-gray-400">→ {usersMap[t.assignedToUserId] ?? t.assignedToUserId.slice(0,8) + '…'}</span>
                                                  )}
                                                  {t.estimatedHours != null && <span className="text-xs text-gray-400">{t.estimatedHours}h est.</span>}
                                                  {t.startDate && <span className="text-xs text-gray-400">from {new Date(t.startDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })}</span>}
                                                </div>
                                              </div>
                                              <div className="flex items-center gap-2 shrink-0">
                                                {canWrite && !t.linkedAssignmentId && t.status !== 'Done' && (
                                                    <button
                                                        onClick={e => { e.stopPropagation(); setShowDispatchModal({ taskId: t.id, milestoneId: m.id, taskTitle: t.title, assignedToUserId: t.assignedToUserId }); setDispatchTechId(t.assignedToUserId ?? '') }}
                                                        className="text-xs font-semibold text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded hover:bg-amber-100 transition-colors"
                                                    >
                                                      Dispatch
                                                    </button>
                                                )}
                                                <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${TASK_BADGE[t.status] ?? 'bg-gray-100 text-gray-600'}`}>
                                    {t.status}
                                  </span>
                                              </div>
                                            </div>
                                        ))}
                                        <TaskDependencies
                                            projectId={id}
                                            tasks={milestoneTasks[m.id]}
                                            deps={taskDeps}
                                            canWrite={canWrite}
                                            onChanged={loadTaskDeps}
                                        />

                                        {/* PR3b — the discussion sits beside the work it is about,
                                            rather than making people go and find a separate tab. */}
                                        <div className="mt-4 pt-3 border-t border-gray-100">
                                          <CommentThread
                                              projectId={id}
                                              targetType="Milestone"
                                              targetId={m.id}
                                              users={mentionUsers}
                                              currentUserId={user?.id}
                                              compact
                                          />
                                        </div>
                                      </div>
                                  )}
                                </div>
                            )}
                          </div>
                      ))}
                    </div>
                )}
              </div>
          )}

          {/* Budget Tab */}
          {tab === 'budget' && (
              <div className="space-y-6">
                {/* Money lives on one page. The commercials panel carries quote-vs-spend, the
                    versioned budget with its approval chain, and the rate card; the milestone
                    breakdown and expense log follow below. */}
                <CommercialsTab
                    projectId={id}
                    canWrite={canWrite}
                    canApprove={permApproveMd || permApproveFinance}
                    onToast={(m) => window.alert(m)}
                />

                {/* Utilization bar */}
                {budget?.plannedBudget > 0 && (
                    <div>
                      <div className="flex justify-between text-xs text-gray-500 mb-1">
                        <span>Budget utilization</span>
                        <span>{budget.utilizationPercent ?? 0}%</span>
                      </div>
                      <div className="h-2.5 bg-gray-100 rounded-full overflow-hidden">
                        <div
                            className={`h-full rounded-full transition-all ${(budget.utilizationPercent ?? 0) > 100 ? 'bg-red-500' : (budget.utilizationPercent ?? 0) > 80 ? 'bg-amber-400' : 'bg-green-500'}`}
                            style={{ width: `${Math.min(budget.utilizationPercent ?? 0, 100)}%` }}
                        />
                      </div>
                    </div>
                )}

                {/* Milestone breakdown */}
                {budget?.milestoneBreakdown?.length > 0 && (
                    <div>
                      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Milestone Breakdown</h3>
                      <div className="bg-white rounded-xl border border-gray-200 overflow-x-auto">
                        <table className="w-full text-sm min-w-[500px]">
                          <thead>
                          <tr className="bg-gray-50 border-b border-gray-100">
                            {['Milestone', 'Budget', 'Actual', 'Variance'].map(h => (
                                <th key={h} className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">{h}</th>
                            ))}
                          </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-50">
                          {budget.milestoneBreakdown.map(m => (
                              <tr key={m.milestoneId}>
                                <td className="px-4 py-3 font-medium text-gray-900">{m.milestoneTitle}</td>
                                <td className="px-4 py-3 text-gray-600">{m.plannedAmount > 0 ? `KES ${Number(m.plannedAmount).toLocaleString()}` : '—'}</td>
                                <td className="px-4 py-3 text-gray-700">KES {Number(m.actualAmount).toLocaleString()}</td>
                                <td className={`px-4 py-3 font-semibold text-sm ${m.variance >= 0 ? 'text-green-600' : 'text-red-500'}`}>
                                  {m.plannedAmount > 0 ? `${m.variance >= 0 ? '+' : ''}KES ${Number(m.variance).toLocaleString()}` : '—'}
                                </td>
                              </tr>
                          ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                )}

                {/* Cost Entries (Expense Log) */}
                <div>
                  <div className="flex items-center justify-between mb-3">
                    <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Expense Log</h3>
                    {canWrite && (
                        <button onClick={() => setShowAddCostEntry(true)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">
                          + Record Expense
                        </button>
                    )}
                  </div>
                  {(!budget?.costEntries || budget.costEntries.length === 0) ? (
                      <EmptyState icon="🧾" title="No expenses recorded" sub="Record actual costs as work progresses." />
                  ) : (
                      <div className="bg-white rounded-xl border border-gray-200 overflow-x-auto">
                        <table className="w-full text-sm min-w-[500px]">
                          <thead>
                          <tr className="bg-gray-50 border-b border-gray-100">
                            {['Date', 'Category', 'Description', 'Milestone', 'Amount'].map(h => (
                                <th key={h} className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">{h}</th>
                            ))}
                          </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-50">
                          {budget.costEntries.map(e => (
                              <tr key={e.id}>
                                <td className="px-4 py-3 text-gray-500 whitespace-nowrap">{new Date(e.entryDate).toLocaleDateString()}</td>
                                <td className="px-4 py-3 text-gray-600">{e.category}</td>
                                <td className="px-4 py-3 text-gray-700">{e.description}</td>
                                <td className="px-4 py-3 text-gray-500">{e.milestoneTitle || '—'}</td>
                                <td className="px-4 py-3 font-semibold text-gray-900">KES {Number(e.amount).toLocaleString()}</td>
                              </tr>
                          ))}
                          </tbody>
                        </table>
                      </div>
                  )}
                </div>
              </div>
          )}

          {/* O11.3 — Variations Tab */}
          {tab === 'schedule' && (
              <ScheduleTab
                  projectId={id}
                  canWrite={canWrite}
                  canApprove={permApproveMd}
                  onToast={(m) => window.alert(m)}
              />
          )}

          {tab === 'variations' && <VariationOrdersTab projectId={id} />}

          {/* PR3 — RAID + change control. Approving a change request re-baselines, so it needs the
              same authority as approving the budget. */}
          {/* PR4c — the same milestones and tasks, seen as a board and a calendar. */}
          {tab === 'board' && <KanbanBoard projectId={id} canWrite={canWrite} usersMap={usersMap} />}
          {tab === 'calendar' && <ProjectCalendar projectId={id} />}

          {/* PR4a — earned value, derived from PR1 baselines + PR2 actuals. */}
          {tab === 'earnedvalue' && <EarnedValueTab projectId={id} />}

          {/* PR3b — project-level discussion. Milestone and task threads live inline on the
              Milestones tab, next to the thing being discussed. */}
          {tab === 'discussion' && (
              <CommentThread
                  projectId={id}
                  targetType="Project"
                  targetId={id}
                  users={mentionUsers}
                  currentUserId={user?.id}
              />
          )}

          {tab === 'governance' && (
              <GovernanceTab
                  projectId={id}
                  canWrite={canWrite}
                  canApprove={permApproveMd || permApproveFinance}
                  onToast={(m) => window.alert(m)}
              />
          )}

          {/* O11.4 — Handover Tab */}
          {tab === 'handover' && <HandoverTab projectId={id} />}

          {/* Attachments Tab */}
          {tab === 'attachments' && (
              <div>
                {attachments.length === 0 ? (
                    <EmptyState icon="📎" title="No attachments" sub="Images uploaded when creating this project or its milestones/tasks appear here." />
                ) : (
                    <div className="columns-2 sm:columns-3 lg:columns-4 gap-3 space-y-3">
                      {attachments.map((a, i) => (
                          <button
                              key={a.id}
                              onClick={() => setLightboxIdx(i)}
                              className="break-inside-avoid w-full rounded-xl overflow-hidden border border-gray-200 bg-white hover:shadow-lg transition-shadow focus:outline-none block"
                          >
                            <img
                                src={`${API_BASE}${a.url}`}
                                alt={a.fileName ?? 'Attachment'}
                                className="w-full object-cover"
                                onError={e => { e.target.closest('button').style.display = 'none' }}
                            />
                            <div className="px-3 py-2 text-left">
                              <p className="text-xs font-semibold text-gray-700 truncate">{a.fileName}</p>
                              <p className="text-xs text-gray-400">{a.entityType} · {new Date(a.uploadedAt).toLocaleDateString()}</p>
                            </div>
                          </button>
                      ))}
                    </div>
                )}
              </div>
          )}

          {/* Resources Tab */}
          {tab === 'resources' && (
              <div>
                {resources.length === 0 ? (
                    <EmptyState icon="👥" title="No resources assigned" sub="Resources assigned to this project will appear here." />
                ) : (
                    <div className="bg-white rounded-xl border border-gray-200 overflow-x-auto">
                      <table className="w-full text-sm min-w-[400px]">
                        <thead>
                        <tr className="bg-gray-50 border-b border-gray-100">
                          {['Name','Role','Department','Added'].map(h => (
                              <th key={h} className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">{h}</th>
                          ))}
                        </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-50">
                        {resources.map(r => (
                            <tr key={r.id}>
                              <td className="px-4 py-3 font-medium text-gray-900">{r.userName}</td>
                              <td className="px-4 py-3 text-gray-500">{r.role ?? '—'}</td>
                              <td className="px-4 py-3 text-gray-500">{r.department ?? '—'}</td>
                              <td className="px-4 py-3 text-gray-400 text-xs">{r.addedAt ? new Date(r.addedAt).toLocaleDateString() : '—'}</td>
                            </tr>
                        ))}
                        </tbody>
                      </table>
                    </div>
                )}
              </div>
          )}
        </main>

        {/* O11.1 — milestone sign-off / daily-update modals */}
        {signOffMs && <MilestoneSignOffModal milestone={signOffMs} onClose={() => setSignOffMs(null)} onSave={doSignOffMilestone} />}
        {updateMs && <MilestoneUpdateModal milestone={updateMs} onClose={() => setUpdateMs(null)} onSave={doUpdateMilestone} />}

        {/* Add Milestone Modal */}
        {showAddMilestone && (
            <Modal title="Add Milestone" onClose={() => { setShowAddMilestone(false); setPendingFiles([]) }}>
              <form onSubmit={handleAddMilestone} className="space-y-4">
                <MField label="Title *"><input type="text" value={milestoneForm.title} onChange={e => setMilestoneForm(f => ({ ...f, title: e.target.value }))} required className="input" /></MField>
                <MField label="Description"><textarea value={milestoneForm.description} onChange={e => setMilestoneForm(f => ({ ...f, description: e.target.value }))} rows={3} className="input resize-none" /></MField>
                <MField label="Planned Completion Date"><input type="date" value={milestoneForm.plannedCompletionDate} onChange={e => setMilestoneForm(f => ({ ...f, plannedCompletionDate: e.target.value }))} className="input" /></MField>
                <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
                <ModalActions onCancel={() => { setShowAddMilestone(false); setPendingFiles([]) }} submitLabel="Add Milestone" />
              </form>
            </Modal>
        )}

        {/* Add Task Modal */}
        {showAddTask && (
            <Modal title="Add Task" onClose={() => { setShowAddTask(null); setTaskErr(''); setPendingFiles([]) }}>
              <form onSubmit={handleAddTask} className="space-y-4">
                {taskErr && <p className="text-sm text-red-600">{taskErr}</p>}
                <MField label="Task Title *"><input type="text" value={taskForm.title} onChange={e => setTaskForm(f => ({ ...f, title: e.target.value }))} required className="input" /></MField>
                <MField label="Description"><textarea value={taskForm.description} onChange={e => setTaskForm(f => ({ ...f, description: e.target.value }))} rows={3} className="input resize-none" /></MField>
                <MField label="Assign To">
                  <select value={taskForm.assignedToUserId} onChange={e => setTaskForm(f => ({ ...f, assignedToUserId: e.target.value }))} className="input">
                    <option value="">— Unassigned —</option>
                    {users.map(u => <option key={u.id} value={u.id}>{getUserName(u)}</option>)}
                  </select>
                </MField>
                <MField label="Subtask of" note="Tasks nest one level only, so a subtask can't be chosen as a parent.">
                  <select value={taskForm.parentTaskId} onChange={e => setTaskForm(f => ({ ...f, parentTaskId: e.target.value }))} className="input">
                    <option value="">— Top-level task —</option>
                    {(milestoneTasks[showAddTask] ?? []).filter(t => !t.parentTaskId).map(t => (
                        <option key={t.id} value={t.id}>{t.title}</option>
                    ))}
                  </select>
                </MField>
                <div className="grid grid-cols-2 gap-4">
                  <MField label="Start date"><input type="date" value={taskForm.startDate} onChange={e => setTaskForm(f => ({ ...f, startDate: e.target.value }))} className="input" /></MField>
                  <MField label="Estimated hours"><input type="number" min="0" step="0.5" value={taskForm.estimatedHours} onChange={e => setTaskForm(f => ({ ...f, estimatedHours: e.target.value }))} className="input" /></MField>
                </div>
                <ModalActions onCancel={() => { setShowAddTask(null); setTaskErr(''); setPendingFiles([]) }} submitLabel="Add Task" loading={taskSaving} />
              </form>
            </Modal>
        )}

        {/* Record Expense Modal */}
        {showAddCostEntry && (
            <Modal title="Record Expense" onClose={() => setShowAddCostEntry(false)}>
              <form onSubmit={handleAddCostEntry} className="space-y-4">
                <MField label="Category *">
                  <select value={costEntryForm.category} onChange={e => setCostEntryForm(f => ({ ...f, category: e.target.value }))} className="input">
                    {['Labour','Materials','Equipment','Fleet','Subcontractor','Other'].map(c => <option key={c}>{c}</option>)}
                  </select>
                </MField>
                <MField label="Milestone (optional)">
                  <select value={costEntryForm.milestoneId} onChange={e => setCostEntryForm(f => ({ ...f, milestoneId: e.target.value }))} className="input">
                    <option value="">— Not linked to a milestone —</option>
                    {milestones.map(m => <option key={m.id} value={m.id}>{m.title}</option>)}
                  </select>
                </MField>
                <MField label="Budget Line (optional)">
                  <select value={costEntryForm.budgetLineId} onChange={e => setCostEntryForm(f => ({ ...f, budgetLineId: e.target.value }))} className="input">
                    <option value="">— Not linked to a budget line —</option>
                    {(budget?.lines ?? []).map(l => <option key={l.id} value={l.id}>{l.category} — {l.description || 'No description'}</option>)}
                  </select>
                </MField>
                <MField label="Description *"><input type="text" required value={costEntryForm.description} onChange={e => setCostEntryForm(f => ({ ...f, description: e.target.value }))} className="input" placeholder="e.g. Site labour for week 3" /></MField>
                <MField label="Amount (KES) *"><input type="number" min="0" step="0.01" required value={costEntryForm.amount} onChange={e => setCostEntryForm(f => ({ ...f, amount: e.target.value }))} className="input" /></MField>
                <MField label="Notes"><textarea value={costEntryForm.notes} onChange={e => setCostEntryForm(f => ({ ...f, notes: e.target.value }))} rows={2} className="input resize-none" /></MField>
                <ModalActions onCancel={() => setShowAddCostEntry(false)} submitLabel="Record Expense" />
              </form>
            </Modal>
        )}

        {/* Dispatch Task Modal */}
        {showDispatchModal && (
            <Modal title="Dispatch as Assignment" onClose={() => { setShowDispatchModal(null); setDispatchNotes(''); setDispatchTechId('') }}>
              <div className="space-y-4">
                <p className="text-sm text-gray-600">
                  Create an operations assignment from <strong>{showDispatchModal.taskTitle}</strong>.
                </p>
                <MField label="Assign To">
                  {users.length > 0 ? (
                      <select value={dispatchTechId} onChange={e => setDispatchTechId(e.target.value)} className="input">
                        <option value="">— Select technician —</option>
                        {users.map(u => <option key={u.id} value={u.id}>{getUserName(u)}</option>)}
                      </select>
                  ) : (
                      <div className="input text-gray-400">No users available</div>
                  )}
                </MField>
                <MField label="Notes (optional)">
                  <textarea value={dispatchNotes} onChange={e => setDispatchNotes(e.target.value)} rows={3} className="input resize-none" placeholder="Any notes for the technician…" />
                </MField>
                <div className="flex items-center justify-end gap-3">
                  <button type="button" onClick={() => { setShowDispatchModal(null); setDispatchNotes(''); setDispatchTechId('') }}
                          className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                    Cancel
                  </button>
                  <button type="button" onClick={handleDispatch} disabled={dispatchLoading}
                          className="px-5 py-2 text-sm font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white disabled:opacity-50 transition-colors">
                    {dispatchLoading ? 'Dispatching…' : 'Dispatch'}
                  </button>
                </div>
              </div>
            </Modal>
        )}

        {/* Review/Approve Modal */}
        {showReview && (
            <Modal title={showReview.type === 'md' ? 'MD Approval' : 'Finance Approval'} onClose={() => setShowReview(null)}>
              <form onSubmit={handleReview} className="space-y-4">
                <MField label="Decision">
                  <div className="flex gap-4">
                    {[true, false].map(v => (
                        <label key={String(v)} className="flex items-center gap-2 cursor-pointer text-sm font-medium text-gray-700">
                          <input type="radio" name="decision" checked={reviewForm.approved === v} onChange={() => setReviewForm(f => ({ ...f, approved: v }))} className="accent-amber-500" />
                          {v ? 'Approve' : 'Reject'}
                        </label>
                    ))}
                  </div>
                </MField>
                <MField label="Comments">
                  <textarea value={reviewForm.comments} onChange={e => setReviewForm(f => ({ ...f, comments: e.target.value }))} rows={3} className="input resize-none" />
                </MField>
                <div className="flex items-center justify-end gap-3">
                  <button type="button" onClick={() => setShowReview(null)} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
                  <button type="submit" className={`px-5 py-2 text-sm font-semibold rounded-lg text-white transition-colors ${reviewForm.approved ? 'bg-green-500 hover:bg-green-600' : 'bg-red-500 hover:bg-red-600'}`}>
                    {reviewForm.approved ? 'Approve' : 'Reject'}
                  </button>
                </div>
              </form>
            </Modal>
        )}

        {/* Lightbox */}
        {lightboxIdx !== null && (
            <Lightbox
                attachments={attachments}
                index={lightboxIdx}
                onClose={() => setLightboxIdx(null)}
                onPrev={() => setLightboxIdx(i => (i - 1 + attachments.length) % attachments.length)}
                onNext={() => setLightboxIdx(i => (i + 1) % attachments.length)}
            />
        )}
      </>
  )
}

function Lightbox({ attachments, index, onClose, onPrev, onNext }) {
  const a = attachments[index]
  useEffect(() => {
    const handler = (e) => {
      if (e.key === 'Escape') onClose()
      if (e.key === 'ArrowLeft') onPrev()
      if (e.key === 'ArrowRight') onNext()
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose, onPrev, onNext])

  return (
      <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/90"
          onClick={onClose}
      >
        {/* Prev */}
        <button
            onClick={e => { e.stopPropagation(); onPrev() }}
            className="absolute left-4 top-1/2 -translate-y-1/2 w-10 h-10 flex items-center justify-center rounded-full bg-white/10 hover:bg-white/25 text-white transition-colors"
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" /></svg>
        </button>

        {/* Image */}
        <div className="flex flex-col items-center max-w-4xl max-h-screen px-16" onClick={e => e.stopPropagation()}>
          <img
              src={`${API_BASE}${a.url}`}
              alt={a.fileName ?? 'Attachment'}
              className="max-h-[80vh] max-w-full rounded-xl object-contain shadow-2xl"
          />
          <div className="flex items-center gap-4 mt-4">
            <p className="text-white/80 text-sm">{a.fileName}</p>
            <span className="text-white/40 text-xs">{index + 1} / {attachments.length}</span>
            <a
                href={`${API_BASE}${a.url}`}
                download={a.fileName}
                target="_blank"
                rel="noopener noreferrer"
                className="ml-2 px-3 py-1.5 rounded-lg bg-white/10 hover:bg-white/20 text-white text-xs font-medium transition-colors"
                onClick={e => e.stopPropagation()}
            >
              Download
            </a>
          </div>
        </div>

        {/* Next */}
        <button
            onClick={e => { e.stopPropagation(); onNext() }}
            className="absolute right-4 top-1/2 -translate-y-1/2 w-10 h-10 flex items-center justify-center rounded-full bg-white/10 hover:bg-white/25 text-white transition-colors"
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" /></svg>
        </button>

        {/* Close */}
        <button
            onClick={onClose}
            className="absolute top-4 right-4 w-9 h-9 flex items-center justify-center rounded-full bg-white/10 hover:bg-white/25 text-white transition-colors"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
      </div>
  )
}

function InfoCard({ title, children }) {
  return (
      <div className="bg-white rounded-xl border border-gray-200 p-5">
        <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wider mb-4">{title}</h3>
        <div className="space-y-3">{children}</div>
      </div>
  )
}

function InfoRow({ label, value }) {
  return (
      <div className="flex items-start justify-between gap-4">
        <span className="text-sm text-gray-500 shrink-0">{label}</span>
        <span className="text-sm font-medium text-gray-900 text-right">{value}</span>
      </div>
  )
}

function EmptyState({ icon, title, sub }) {
  return (
      <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-16 text-center">
        <div className="text-4xl mb-3">{icon}</div>
        <h3 className="font-semibold text-gray-700 text-sm">{title}</h3>
        {sub && <p className="text-xs text-gray-400 mt-1">{sub}</p>}
      </div>
  )
}

function Modal({ title, onClose, children }) {
  return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
        <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 max-h-[90vh] overflow-y-auto">
          <div className="flex items-center justify-between mb-5">
            <h2 className="text-lg font-bold text-zinc-950">{title}</h2>
            <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          {children}
        </div>
      </div>
  )
}

function ModalActions({ onCancel, submitLabel, loading }) {
  return (
      <div className="flex items-center justify-end gap-3">
        <button type="button" onClick={onCancel} disabled={loading} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50">Cancel</button>
        <button type="submit" disabled={loading} className="px-5 py-2 text-sm font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white transition-colors disabled:opacity-60">
          {loading ? 'Saving…' : submitLabel}
        </button>
      </div>
  )
}

function MField({ label, children }) {
  return (
      <div>
        <label className="block text-sm font-semibold text-gray-700 mb-1.5">{label}</label>
        {children}
      </div>
  )
}