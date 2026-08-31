import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import ProjectStatusBadge from '../../components/projects/ProjectStatusBadge.jsx'
import api from '../../api/axios.js'
import { exportToPdf, fmtKES } from '../../utils/export.js'
import * as XLSX from 'xlsx'
// O11 — operations project enhancements moved onto the live projects page
import ProjectComplianceCard from '../../components/operations/detail/ProjectComplianceCard.jsx'
import VariationOrdersTab from '../../components/operations/variations/VariationOrdersTab.jsx'
import OpsHandoverTab from '../../components/operations/handover/HandoverTab.jsx'
import MilestoneSignOffModal from '../../components/operations/detail/MilestoneSignOffModal.jsx'
import MilestoneUpdateModal from '../../components/operations/detail/MilestoneUpdateModal.jsx'
import { signOffMilestone as apiSignOffMilestone, addMilestoneUpdate as apiAddMilestoneUpdate } from '../../services/operations.js'

const TASK_STATUS_LABELS = ['Not Started','In Progress','Done','Blocked']

// Backend serializes status enums as strings — name-keyed maps for milestone/task display.
const MS_NAMES  = ['NotStarted', 'InProgress', 'Completed', 'Delayed']
const MS_LABEL  = { NotStarted: 'Not Started', InProgress: 'In Progress', Completed: 'Completed', Delayed: 'Delayed' }
const MS_COLOR  = { NotStarted: 'bg-gray-100 text-gray-600', InProgress: 'bg-blue-100 text-blue-700', Completed: 'bg-green-100 text-green-700', Delayed: 'bg-red-100 text-red-700' }
const TASK_NAMES = ['NotStarted', 'InProgress', 'Done', 'Blocked']
const TASK_LABEL = { NotStarted: 'Not Started', InProgress: 'In Progress', Done: 'Done', Blocked: 'Blocked' }
const TASK_COLOR = { NotStarted: 'bg-gray-100 text-gray-600', InProgress: 'bg-blue-100 text-blue-700', Done: 'bg-green-100 text-green-700', Blocked: 'bg-red-100 text-red-700' }
const TASK_STATUS_COLORS = ['bg-gray-100 text-gray-600','bg-blue-100 text-blue-700','bg-green-100 text-green-700','bg-red-100 text-red-700']
const MILESTONE_STATUS_LABELS = ['Not Started','In Progress','Completed','Delayed']
const BUDGET_CATS = ['Labour','Materials','Equipment','Fleet','Subcontractor','Other']
const APPROVAL_TYPES = ['Finance','MD','Board']
const APPROVAL_STATUS_LABELS = ['Pending','Approved','Rejected']

function fmt(n) {
  return new Intl.NumberFormat('en-KE', { style:'currency', currency:'KES', maximumFractionDigits:0 }).format(n ?? 0)
}
function fmtDate(d) {
  return d ? new Date(d).toLocaleDateString(undefined, { dateStyle:'medium' }) : '—'
}

export default function ProjectDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [project, setProject] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [tab, setTab]         = useState('overview') // overview | plan | execution | history
  const [history, setHistory] = useState([])
  const [costs, setCosts]     = useState([])
  const [reports, setReports] = useState([])
  const [successMsg, setSuccessMsg] = useState('')

  // Modals
  const [modal, setModal]       = useState(null)
  const [modalData, setModalData] = useState({})
  const [signOffMs, setSignOffMs] = useState(null)   // O11 — milestone sign-off modal
  const [updateMs, setUpdateMs]   = useState(null)   // O11 — milestone daily-update modal
  const [users, setUsers]         = useState([])     // for the task "assigned to" dropdown
  const [departments, setDepartments] = useState([]) // resolves project.departmentId to a name
  const [modalErr, setModalErr] = useState('')
  const [saving, setSaving]     = useState(false)
  const [exportMenu, setExportMenu] = useState(false)

  const loadProject = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get(`/api/v1/projects/${id}`)
      setProject(res.data?.data)
    } catch {
      setError('Failed to load project.')
    } finally {
      setLoading(false)
    }
  }, [id])

  const refreshProject = useCallback(async () => {
    try {
      const res = await api.get(`/api/v1/projects/${id}`)
      setProject(res.data?.data)
    } catch {}
  }, [id])

  useEffect(() => { loadProject() }, [loadProject])
  useEffect(() => { api.get('/api/v1/users?pageSize=200').then(r => setUsers(r.data?.data?.items ?? [])).catch(() => {}) }, [])
  useEffect(() => { api.get('/api/v1/departments').then(r => setDepartments(r.data?.data ?? [])).catch(() => {}) }, [])
  const userName = (uid) => { const u = users.find(x => x.id === uid); return u ? [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email : uid }
  const departmentName = (deptId) => departments.find(d => d.id === deptId)?.name ?? deptId

  async function loadTab(t) {
    setTab(t)
    if (t === 'history' && history.length === 0) {
      try {
        const res = await api.get(`/api/v1/projects/${id}/history`)
        setHistory(res.data?.data ?? [])
      } catch {}
    }
    if (t === 'expenses') {
      // Load independently so a missing optional endpoint (daily-reports) never blocks costs.
      try {
        const budgetRes = await api.get(`/api/v1/projects/${id}/budget`)
        setCosts(budgetRes.data?.data?.costEntries ?? [])
      } catch {}
      try {
        const repsRes = await api.get(`/api/v1/projects/${id}/daily-reports`)
        setReports(repsRes.data?.data ?? [])
      } catch { setReports([]) }
    }
  }

  async function doAction(action, payload = {}) {
    setSaving(true)
    setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/${action}`, payload)  // submit/activate/hold/resume/close are POST
      setModal(null)
      setModalData({})
      flash(`Action completed.`)
      refreshProject()
      setHistory([])
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Action failed.')
    } finally {
      setSaving(false)
    }
  }

  async function handleApprovalProcess() {
    const { approvalType, status, comments } = modalData
    // Route to the correct backend endpoint by approval type (MD → approve/md, Finance → approve/finance).
    const seg = String(approvalType).toLowerCase() === 'finance' ? 'finance' : 'md'
    setSaving(true)
    setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/approve/${seg}`, {
        approved: status !== 'Rejected',
        comments: comments || null,
      })
      setModal(null)
      flash('Approval processed.')
      refreshProject()
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to process approval.')
    } finally {
      setSaving(false)
    }
  }

  async function handleAddApproval() {
    setSaving(true)
    setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/approvals`, { approvalType: Number(modalData.type ?? 0) })
      setModal(null)
      flash('Approval request created.')
      refreshProject()
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to create approval.')
    } finally {
      setSaving(false)
    }
  }

  // O11 — milestone sign-off + daily update (operations service), then refresh.
  async function doSignOffMilestone(milestoneId, dto) { await apiSignOffMilestone(id, milestoneId, dto); refreshProject() }
  async function doUpdateMilestone(milestoneId, dto)  { await apiAddMilestoneUpdate(id, milestoneId, dto); refreshProject() }

  async function handleAddMilestone() {
    const { name, description, endDate, budgetAllocation } = modalData
    if (!name?.trim() || !endDate) { setModalErr('Name and due date are required.'); return }
    setSaving(true)
    setModalErr('')
    try {
      const res = await api.post(`/api/v1/projects/${id}/milestones`, {
        title: name.trim(),
        description: description?.trim() || null,
        order: project.milestones?.length ?? 0,
        dueDate: new Date(endDate).toISOString(),
        plannedAmount: budgetAllocation ? Number(budgetAllocation) : null,
      })
      const newMilestone = res.data?.data ?? { title: name.trim(), tasks: [], status: 'NotStarted' }
      setProject(p => ({ ...p, milestones: [...(p.milestones ?? []), newMilestone] }))
      setModal(null)
      flash('Milestone added.')
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to add milestone.')
    } finally {
      setSaving(false)
    }
  }

  async function handleAddTask(milestoneId) {
    const { name, description, assignedToUserId, dueDate } = modalData
    if (!name?.trim()) { setModalErr('Task name is required.'); return }
    setSaving(true)
    setModalErr('')
    try {
      const res = await api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks`, {
        title: name.trim(), description: description?.trim() || null,
        assignedToUserId: assignedToUserId?.trim() || null,
        dueDate: dueDate ? new Date(dueDate).toISOString() : null,
      })
      const newTask = res.data?.data ?? { title: name.trim(), status: 'NotStarted', assignedToUserId: assignedToUserId?.trim() || null, dueDate: dueDate || null }
      setProject(p => ({
        ...p,
        milestones: p.milestones.map(m =>
          m.id === milestoneId ? { ...m, tasks: [...(m.tasks ?? []), newTask] } : m
        ),
      }))
      setModal(null)
      flash('Task added.')
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to add task.')
    } finally {
      setSaving(false)
    }
  }

  async function handleAddBudgetLine() {
    const { category, description, plannedAmount } = modalData
    if (!description?.trim() || !plannedAmount) { setModalErr('Description and amount are required.'); return }
    setSaving(true)
    setModalErr('')
    try {
      const res = await api.post(`/api/v1/projects/${id}/budget/lines`, {
        category: Number(category ?? 0), description: description.trim(), plannedAmount: Number(plannedAmount),
      })
      const newLine = res.data?.data ?? { category: Number(category ?? 0), description: description.trim(), plannedAmount: Number(plannedAmount), actualAmount: 0 }
      setProject(p => ({ ...p, budgetLines: [...(p.budgetLines ?? []), newLine] }))
      setModal(null)
      flash('Budget line added.')
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to add budget line.')
    } finally {
      setSaving(false)
    }
  }

  async function handleLogCost() {
    const { category, description, amount, entryDate, reference } = modalData
    if (!description?.trim() || !amount || !entryDate) { setModalErr('Description, amount and date are required.'); return }
    setSaving(true)
    setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/budget/costs`, {
        category: Number(category ?? 0), description: description.trim(),
        amount: Number(amount), expenditureDate: new Date(entryDate).toISOString(),
        notes: reference?.trim() || null,
      })
      setModal(null)
      flash('Cost logged.')
      refreshProject()
      loadTab('expenses')
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to log cost.')
    } finally {
      setSaving(false)
    }
  }

  async function handleSubmitReport() {
    const { workDone, issues, plannedForTomorrow, reportDate } = modalData
    if (!workDone?.trim() || !reportDate) { setModalErr('Report date and work done are required.'); return }
    setSaving(true)
    setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/daily-reports`, {
        reportDate: new Date(reportDate).toISOString(),
        workDone: workDone.trim(), issues: issues?.trim() || null,
        plannedForTomorrow: plannedForTomorrow?.trim() || null,
      })
      setModal(null)
      flash('Report submitted.')
      setReports([])
      loadTab('expenses')
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to submit report.')
    } finally {
      setSaving(false)
    }
  }

  function flash(msg) {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(''), 3000)
  }

  function handleExport(format) {
    if (!project) return
    setExportMenu(false)

    const STATUSES_L = ['Draft','Planning','Pending Approval','Active','On Hold','Completed','Closed','Cancelled']
    const subtitle = `${project.clientName ? 'Client: ' + project.clientName + ' · ' : ''}Generated ${new Date().toLocaleString()}`

    // Overview sheet / section
    const overviewCols = [
      { header: 'Field',  accessor: r => r.field,  width: 22 },
      { header: 'Value',  accessor: r => r.value,  width: 40 },
    ]
    const overviewRows = [
      { field: 'Project Name',   value: project.name },
      { field: 'Status',         value: project.status ?? '' },
      { field: 'Type',           value: project.type ?? '' },
      { field: 'Risk Level',     value: project.riskLevel ?? '' },
      { field: 'Client',         value: project.clientName ?? '—' },
      { field: 'Client Ref',     value: project.clientReference ?? '—' },
      { field: 'Tender Ref',     value: project.tenderReference ?? '—' },
      { field: 'Start Date',     value: fmtDate(project.startDate) },
      { field: 'Expected End',   value: fmtDate(project.expectedEndDate) },
      { field: 'Contract Value', value: fmtKES(project.contractValue) },
      { field: 'Planned Budget', value: fmtKES(project.plannedBudget) },
      { field: 'Actual Cost',    value: fmtKES(project.actualCost) },
      { field: 'Variance',       value: fmtKES(Math.abs(project.plannedBudget - project.actualCost)) },
      { field: 'Scope',          value: project.scopeSummary ?? '—' },
    ]

    // Budget lines
    const budgetCols = [
      { header: 'Category',    accessor: r => BUDGET_CATS[r.category], width: 18 },
      { header: 'Description', accessor: r => r.description,           width: 30 },
      { header: 'Planned',     accessor: r => fmtKES(r.plannedAmount), width: 20 },
      { header: 'Actual',      accessor: r => fmtKES(r.actualAmount),  width: 20 },
      { header: 'Variance',    accessor: r => fmtKES(r.plannedAmount - r.actualAmount), width: 20 },
    ]

    // Milestones
    const milestoneCols = [
      { header: 'Milestone',   accessor: r => r.name,                              width: 28 },
      { header: 'Status',      accessor: r => MILESTONE_STATUS_LABELS[r.status],   width: 16 },
      { header: 'Start',       accessor: r => fmtDate(r.startDate),                width: 16 },
      { header: 'End',         accessor: r => fmtDate(r.endDate),                  width: 16 },
      { header: 'Budget',      accessor: r => fmtKES(r.budgetAllocation),          width: 20 },
      { header: 'Deliverables',accessor: r => r.deliverables ?? '—',               width: 28 },
    ]

    // Cost entries (loaded in execution tab)
    const costCols = [
      { header: 'Date',        accessor: r => fmtDate(r.entryDate),   width: 16 },
      { header: 'Category',    accessor: r => BUDGET_CATS[r.category],width: 16 },
      { header: 'Description', accessor: r => r.description,          width: 30 },
      { header: 'Amount',      accessor: r => fmtKES(r.amount),       width: 18 },
      { header: 'Reference',   accessor: r => r.reference ?? '—',     width: 18 },
    ]

    const filename = `lante-project-${project.name.replace(/\s+/g, '-').toLowerCase()}`

    if (format === 'pdf') {
      exportToPdf({ title: `Project Report — ${project.name}`, subtitle, columns: overviewCols, rows: overviewRows, filename })
    } else {
      const buildSheet = (cols, rows) => {
        const header = cols.map(c => c.header)
        const data = rows.map(row => cols.map(c => c.accessor(row) ?? ''))
        const ws = {}
        ;[header, ...data].forEach((row, ri) => {
          row.forEach((val, ci) => {
            const cellRef = String.fromCharCode(65 + ci) + (ri + 1)
            ws[cellRef] = { v: val, t: 's' }
          })
        })
        ws['!ref'] = `A1:${String.fromCharCode(65 + cols.length - 1)}${data.length + 1}`
        ws['!cols'] = cols.map(c => ({ wch: c.width ?? 20 }))
        return ws
      }

      const wb = XLSX.utils.book_new()
      XLSX.utils.book_append_sheet(wb, buildSheet(overviewCols, overviewRows), 'Overview')
      if (project.budgetLines?.length) XLSX.utils.book_append_sheet(wb, buildSheet(budgetCols, project.budgetLines), 'Budget')
      if (project.milestones?.length)  XLSX.utils.book_append_sheet(wb, buildSheet(milestoneCols, project.milestones), 'Milestones')
      if (costs?.length)               XLSX.utils.book_append_sheet(wb, buildSheet(costCols, costs), 'Cost Entries')
      XLSX.writeFile(wb, `${filename}.xlsx`)
    }
  }

  async function handleTaskStatus(projectId, milestoneId, taskId, newStatus) {
    setProject(p => ({
      ...p,
      milestones: p.milestones.map(m =>
        m.id === milestoneId
          ? { ...m, tasks: m.tasks.map(t => t.id === taskId ? { ...t, status: newStatus } : t) }
          : m
      ),
    }))
    try {
      await api.put(`/api/v1/projects/${projectId}/milestones/${milestoneId}/tasks/${taskId}`, { status: newStatus })
    } catch {
      flash('Failed to update task status.')
      refreshProject()
    }
  }

  async function handleMilestoneStatus(milestoneId, newStatus) {
    setProject(p => ({
      ...p,
      milestones: p.milestones.map(m => m.id === milestoneId ? { ...m, status: newStatus } : m),
    }))
    try {
      await api.put(`/api/v1/projects/${id}/milestones/${milestoneId}`, { status: newStatus })
    } catch {
      flash('Failed to update milestone status.')
      refreshProject()
    }
  }

  async function handleDispatchTask() {
    const { milestoneId, taskId, assignedToUserId } = modalData
    if (!assignedToUserId) { setModalErr('Select a technician.'); return }
    const u = users.find(x => x.id === assignedToUserId)
    const uname = u ? ([u.firstName, u.lastName].filter(Boolean).join(' ') || u.email) : null
    setSaving(true); setModalErr('')
    try {
      await api.post(`/api/v1/projects/${id}/milestones/${milestoneId}/tasks/${taskId}/dispatch`, {
        assignedToUserId, assignedToUserName: uname, notes: null,
      })
      setModal(null)
      flash(modalData.isReassign ? 'Task reassigned.' : 'Task dispatched to Tasks/Assignments.')
      refreshProject()
    } catch (err) {
      setModalErr(err.response?.data?.message ?? 'Failed to dispatch task.')
    } finally {
      setSaving(false)
    }
  }

  function openModal(type, data = {}) {
    setModal(type)
    setModalData(data)
    setModalErr('')
  }

  if (loading) return <LoadingScreen />
  if (error || !project) return <ErrorScreen msg={error} onBack={() => navigate('/modules/projects')} />

  // Backend serializes ProjectStatus as a string (JsonStringEnumConverter) — compare by name.
  const FINALIZED   = ['Completed', 'Closed', 'Cancelled']
  const statusLabel = project.status || 'Draft'
  const canEdit     = !FINALIZED.includes(project.status)               // add milestones/tasks/budget lines
  const canSubmit   = project.status === 'Draft'
  const canActivate = project.status === 'Planning'                     // Planning → Active (POST /activate)
  const canHold     = project.status === 'Active'
  const canResume   = project.status === 'OnHold'
  const canComplete = false                                             // no backend "complete" endpoint yet
  const canClose    = project.status === 'Active' || project.status === 'OnHold'
  const canCancel   = false                                             // no backend "cancel" endpoint yet

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8">
        {/* Back */}
        <div className="flex items-center justify-between mb-5">
          <button onClick={() => navigate('/modules/projects')}
            className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-600 transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            All Projects
          </button>
          {/* Export dropdown */}
          <div className="relative" onClick={e => e.stopPropagation()}>
            <button
              onClick={() => setExportMenu(v => !v)}
              className="inline-flex items-center gap-2 px-3.5 py-2 border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 text-sm font-medium rounded-lg transition-colors"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              Export Report
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
              </svg>
            </button>
            {exportMenu && (
              <div className="absolute right-0 mt-1 w-44 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
                <button onClick={() => handleExport('pdf')} className="flex items-center gap-2.5 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 rounded-t-lg">
                  <span className="text-red-500 font-bold text-xs">PDF</span> Export as PDF
                </button>
                <button onClick={() => handleExport('excel')} className="flex items-center gap-2.5 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 border-t border-gray-100 rounded-b-lg">
                  <span className="text-green-600 font-bold text-xs">XLS</span> Export as Excel
                </button>
              </div>
            )}
          </div>
        </div>

        {successMsg && (
          <div className="bg-green-50 border border-green-200 text-green-700 rounded-xl px-5 py-3 text-sm mb-5">{successMsg}</div>
        )}

        {/* Header card */}
        <div className="bg-white rounded-xl border border-gray-200 p-6 mb-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-3 mb-1">
                <h1 className="text-xl font-extrabold text-navy">{project.name}</h1>
                <ProjectStatusBadge status={project.status} />
                <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${project.riskLevel === 'High' ? 'bg-red-50 text-red-600' : project.riskLevel === 'Medium' ? 'bg-amber-50 text-amber-600' : 'bg-green-50 text-green-600'}`}>
                  {project.riskLevel} Risk
                </span>
              </div>
              {project.clientName && <p className="text-sm text-gray-500">Client: <span className="font-medium text-gray-700">{project.clientName}</span></p>}
              {project.scopeSummary && <p className="text-sm text-gray-500 mt-1 max-w-2xl">{project.scopeSummary}</p>}
            </div>

            {/* Action buttons */}
            <div className="flex flex-wrap gap-2">
              {canSubmit   && <ActionBtn label="Submit for Approval" color="amber" onClick={() => openModal('confirm', { action:'submit', label:'Submit this project for approval?' })} />}
              {canActivate && <ActionBtn label="Activate" color="green" onClick={() => openModal('confirm', { action:'activate', label:'Activate this project? Ensure Finance & MD approvals are done.' })} />}
              {canHold     && <ActionBtn label="Put on Hold" color="orange" onClick={() => openModal('confirm', { action:'hold', label:'Put this project on hold?' })} />}
              {canResume   && <ActionBtn label="Resume" color="blue" onClick={() => openModal('confirm', { action:'resume', label:'Resume this project?' })} />}
              {canComplete && <ActionBtn label="Mark Complete" color="purple" onClick={() => openModal('confirm', { action:'complete', label:'Mark project as completed? All tasks must be done.' })} />}
              {canClose    && <ActionBtn label="Close Project" color="gray" onClick={() => openModal('confirm', { action:'close', label:'Close and lock this project?' })} />}
              {canCancel   && <ActionBtn label="Cancel" color="red" onClick={() => openModal('confirm', { action:'cancel', label:'Cancel this project? This cannot be undone.' })} />}
            </div>
          </div>

          {/* KPI row — Value/Budget/Expenses/Invoiced/Collected, matching the reference project card layout */}
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-4 mt-5 pt-5 border-t border-gray-100">
            <KPI label="Value" value={fmt(project.contractValue)} />
            <KPI label="Budget" value={fmt(project.plannedBudget)} />
            <KPI label="Expenses" value={fmt(project.actualCost)} />
            <KPI label="Invoiced" value={fmt(project.invoicedAmount ?? project.invoiced ?? 0)} />
            <KPI label="Collected" value={fmt(project.collectedAmount ?? project.collected ?? 0)} />
          </div>

          {/* Budget-used bar + gross profit (mirrors the reference project detail layout) */}
          {(() => {
            const ratio = project.plannedBudget ? project.actualCost / project.plannedBudget : 0
            const gp = (project.contractValue || 0) - (project.actualCost || 0)
            const margin = project.contractValue ? gp / project.contractValue : 0
            const barColor = ratio >= 1 ? 'bg-red-500' : ratio >= 0.8 ? 'bg-amber-500' : 'bg-green-500'
            const pctColor = ratio >= 1 ? 'text-red-500' : ratio >= 0.8 ? 'text-amber-600' : 'text-green-600'
            return (
              <div className="mt-5">
                <div className="h-2 bg-lgrey rounded-full overflow-hidden mb-1.5">
                  <div className={`h-full ${barColor}`} style={{ width: `${Math.min(ratio * 100, 100)}%` }} />
                </div>
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <span className="text-gray-500">Budget Used: <span className={`font-semibold ${pctColor}`}>{(ratio * 100).toFixed(1)}%</span></span>
                  <span className="text-gray-500">Gross Profit: <span className={`font-semibold ${margin >= .15 ? 'text-green-600' : margin >= .1 ? 'text-amber-600' : 'text-red-500'}`}>{fmt(gp)} ({(margin * 100).toFixed(1)}%)</span></span>
                </div>
                {ratio >= .95 && (
                  <div className="mt-3 bg-red-50 text-red-600 rounded-lg px-3 py-2 text-xs font-semibold">🔴 Budget Critical — MD Approval Required</div>
                )}
              </div>
            )
          })()}
        </div>

        {/* Tabs — mirrors the deployed QSL Projects detail. Overview/Milestones/Expenses/History are
            wired to the real operations backend; Timesheets/Subcontractors/Handover are frontend-only
            for now (no backend yet — see memory note). */}
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="flex border-b border-gray-100 overflow-x-auto">
            {[
              { id: 'overview', label: 'Overview' },
              { id: 'milestones', label: 'Milestones' },
              { id: 'expenses', label: 'Expenses' },
              { id: 'variations', label: 'Variations' },
              { id: 'timesheets', label: 'Timesheets' },
              { id: 'subcontractors', label: 'Subcontractors' },
              { id: 'handover', label: 'Handover' },
              { id: 'history', label: 'History' },
            ].map(t => (
              <button
                key={t.id}
                onClick={() => loadTab(t.id)}
                className={`px-5 py-3 text-sm font-semibold whitespace-nowrap transition-colors ${tab === t.id ? 'text-navy border-b-2 border-gold' : 'text-gray-500 hover:text-gray-700'}`}
              >
                {t.label}
              </button>
            ))}
          </div>

          <div className="p-6">
            {tab === 'overview'       && <OverviewTab project={project} statusLabel={statusLabel} openModal={openModal} userName={userName} departmentName={departmentName} />}
            {tab === 'milestones'     && <PlanTab project={project} openModal={openModal} onTaskStatus={handleTaskStatus} onMilestoneStatus={handleMilestoneStatus} onSignOff={setSignOffMs} onUpdate={setUpdateMs} userName={userName} projectId={id} />}
            {tab === 'expenses'       && <ExecutionTab costs={costs} reports={reports} openModal={openModal} project={project} onExportCosts={() => handleExport('excel')} />}
            {tab === 'variations'     && <VariationOrdersTab projectId={id} />}
            {tab === 'timesheets'     && <TimesheetsTab />}
            {tab === 'subcontractors' && <SubcontractorsTab projectId={id} />}
            {tab === 'handover'       && <OpsHandoverTab projectId={id} />}
            {tab === 'history'        && <HistoryTab history={history} />}
          </div>
        </div>
      </main>

      {/* Modals */}
      {signOffMs && <MilestoneSignOffModal milestone={signOffMs} onClose={() => setSignOffMs(null)} onSave={doSignOffMilestone} />}
      {updateMs && <MilestoneUpdateModal milestone={updateMs} onClose={() => setUpdateMs(null)} onSave={doUpdateMilestone} />}
      {modal === 'confirm' && (
        <ModalShell title="Confirm Action" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <p className="text-sm text-gray-600 mb-5">{modalData.label}</p>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={() => doAction(modalData.action)} loading={saving} confirmLabel="Confirm" />
        </ModalShell>
      )}

      {modal === 'addApproval' && (
        <ModalShell title="Request Approval" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <Field label="Approval Type">
            <select value={modalData.type ?? 0} onChange={e => setModalData(d => ({ ...d, type: e.target.value }))} className="input">
              {APPROVAL_TYPES.map((t,i) => <option key={i} value={i}>{t}</option>)}
            </select>
          </Field>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleAddApproval} loading={saving} confirmLabel="Request" />
        </ModalShell>
      )}

      {modal === 'processApproval' && (
        <ModalShell title="Process Approval" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Decision">
              <select value={modalData.status ?? 'Approved'} onChange={e => setModalData(d => ({ ...d, status: e.target.value }))} className="input">
                <option value="Approved">Approve</option>
                <option value="Rejected">Reject</option>
              </select>
            </Field>
            <Field label="Comments">
              <textarea value={modalData.comments ?? ''} onChange={e => setModalData(d => ({ ...d, comments: e.target.value }))} rows={3} className="input resize-none" placeholder="Optional comments…" />
            </Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleApprovalProcess} loading={saving} confirmLabel="Submit" />
        </ModalShell>
      )}

      {modal === 'addMilestone' && (
        <ModalShell title="Add Milestone" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Name *"><input type="text" value={modalData.name ?? ''} onChange={e => setModalData(d => ({ ...d, name: e.target.value }))} className="input" placeholder="e.g. Phase 1 — Site Survey" /></Field>
            <Field label="Description"><textarea value={modalData.description ?? ''} onChange={e => setModalData(d => ({ ...d, description: e.target.value }))} rows={2} className="input resize-none" /></Field>
            <Field label="Due Date *"><input type="date" value={modalData.endDate ?? ''} onChange={e => setModalData(d => ({ ...d, endDate: e.target.value }))} className="input" /></Field>
            <Field label="Budget Allocation (KES)"><input type="number" min="0" value={modalData.budgetAllocation ?? ''} onChange={e => setModalData(d => ({ ...d, budgetAllocation: e.target.value }))} className="input" /></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleAddMilestone} loading={saving} confirmLabel="Add Milestone" />
        </ModalShell>
      )}

      {modal === 'addTask' && (
        <ModalShell title="Add Task" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Task Name *"><input type="text" value={modalData.name ?? ''} onChange={e => setModalData(d => ({ ...d, name: e.target.value }))} className="input" /></Field>
            <Field label="Description"><textarea value={modalData.description ?? ''} onChange={e => setModalData(d => ({ ...d, description: e.target.value }))} rows={2} className="input resize-none" /></Field>
            <Field label="Assigned To"><select value={modalData.assignedToUserId ?? ''} onChange={e => setModalData(d => ({ ...d, assignedToUserId: e.target.value }))} className="input">
              <option value="">Unassigned</option>
              {users.map(u => <option key={u.id} value={u.id}>{[u.firstName, u.lastName].filter(Boolean).join(' ') || u.email}</option>)}
            </select></Field>
            <Field label="Due Date"><input type="date" value={modalData.dueDate ?? ''} onChange={e => setModalData(d => ({ ...d, dueDate: e.target.value }))} className="input" /></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={() => handleAddTask(modalData.milestoneId)} loading={saving} confirmLabel="Add Task" />
        </ModalShell>
      )}

      {modal === 'dispatchTask' && (
        <ModalShell title={modalData.isReassign ? 'Reassign Task' : 'Dispatch Task'} onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <p className="text-sm text-gray-500">
              {modalData.isReassign
                ? 'Move this task to a different technician. Its linked assignment updates automatically.'
                : 'Assign this task to a technician. It becomes an assignment on the Tasks page.'}
            </p>
            <Field label="Technician *"><select value={modalData.assignedToUserId ?? ''} onChange={e => setModalData(d => ({ ...d, assignedToUserId: e.target.value }))} className="input">
              <option value="">Select technician…</option>
              {users.map(u => <option key={u.id} value={u.id}>{[u.firstName, u.lastName].filter(Boolean).join(' ') || u.email}</option>)}
            </select></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleDispatchTask} loading={saving} confirmLabel={modalData.isReassign ? 'Reassign' : 'Dispatch'} />
        </ModalShell>
      )}

      {modal === 'addBudgetLine' && (
        <ModalShell title="Add Budget Line" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Category">
              <select value={modalData.category ?? 0} onChange={e => setModalData(d => ({ ...d, category: e.target.value }))} className="input">
                {BUDGET_CATS.map((c,i) => <option key={i} value={i}>{c}</option>)}
              </select>
            </Field>
            <Field label="Description *"><input type="text" value={modalData.description ?? ''} onChange={e => setModalData(d => ({ ...d, description: e.target.value }))} className="input" /></Field>
            <Field label="Planned Amount (KES) *"><input type="number" min="0" value={modalData.plannedAmount ?? ''} onChange={e => setModalData(d => ({ ...d, plannedAmount: e.target.value }))} className="input" /></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleAddBudgetLine} loading={saving} confirmLabel="Add Line" />
        </ModalShell>
      )}

      {modal === 'logCost' && (
        <ModalShell title="Log Cost Entry" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Category">
              <select value={modalData.category ?? 0} onChange={e => setModalData(d => ({ ...d, category: e.target.value }))} className="input">
                {BUDGET_CATS.map((c,i) => <option key={i} value={i}>{c}</option>)}
              </select>
            </Field>
            <Field label="Description *"><input type="text" value={modalData.description ?? ''} onChange={e => setModalData(d => ({ ...d, description: e.target.value }))} className="input" /></Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Amount (KES) *"><input type="number" min="0" value={modalData.amount ?? ''} onChange={e => setModalData(d => ({ ...d, amount: e.target.value }))} className="input" /></Field>
              <Field label="Date *"><input type="date" value={modalData.entryDate ?? ''} onChange={e => setModalData(d => ({ ...d, entryDate: e.target.value }))} className="input" /></Field>
            </div>
            <Field label="Reference (invoice/receipt)"><input type="text" value={modalData.reference ?? ''} onChange={e => setModalData(d => ({ ...d, reference: e.target.value }))} className="input" /></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleLogCost} loading={saving} confirmLabel="Log Cost" />
        </ModalShell>
      )}

      {modal === 'submitReport' && (
        <ModalShell title="Submit Daily Report" onClose={() => setModal(null)}>
          {modalErr && <ErrBox msg={modalErr} />}
          <div className="space-y-4">
            <Field label="Report Date *"><input type="date" value={modalData.reportDate ?? ''} onChange={e => setModalData(d => ({ ...d, reportDate: e.target.value }))} className="input" /></Field>
            <Field label="Work Done *"><textarea value={modalData.workDone ?? ''} onChange={e => setModalData(d => ({ ...d, workDone: e.target.value }))} rows={4} className="input resize-none" placeholder="Describe work completed today…" /></Field>
            <Field label="Issues / Blockers"><textarea value={modalData.issues ?? ''} onChange={e => setModalData(d => ({ ...d, issues: e.target.value }))} rows={2} className="input resize-none" /></Field>
            <Field label="Planned for Tomorrow"><textarea value={modalData.plannedForTomorrow ?? ''} onChange={e => setModalData(d => ({ ...d, plannedForTomorrow: e.target.value }))} rows={2} className="input resize-none" /></Field>
          </div>
          <ModalFooter onCancel={() => setModal(null)} onConfirm={handleSubmitReport} loading={saving} confirmLabel="Submit Report" />
        </ModalShell>
      )}
    </>
  )
}

/* ── Tab components ─────────────────────────────── */

function OverviewTab({ project, statusLabel, openModal, userName, departmentName }) {
  const APPROVAL_STATUS_COLORS = ['bg-amber-100 text-amber-700','bg-green-100 text-green-700','bg-red-100 text-red-700']
  const gp = (project.contractValue || 0) - (project.actualCost || 0)
  const invoiced = project.invoicedAmount ?? project.invoiced ?? 0
  const collected = project.collectedAmount ?? project.collected ?? 0
  const pnl = [
    ['Contract Value', fmt(project.contractValue), ''],
    ['Total Budget', fmt(project.plannedBudget), ''],
    ['Expenses', fmt(project.actualCost), 'text-red-600'],
    ['Gross Profit', fmt(gp), 'text-green-600'],
    ['Invoiced', fmt(invoiced), ''],
    ['Collected', fmt(collected), 'text-green-600'],
  ]

  return (
    <div className="space-y-6">
      {/* Deployed-style top row: Daily Updates + P&L Summary */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="border border-gray-200 rounded-xl p-5">
          <div className="flex items-start justify-between gap-3 mb-4">
            <div>
              <p className="font-bold text-navy">Daily Updates</p>
              <p className="text-xs text-gray-500 mt-0.5">PROJ-014/015: Post by COB daily</p>
            </div>
            <button onClick={() => openModal('logCost', { entryDate: new Date().toISOString().split('T')[0] })}
              className="flex-shrink-0 px-3.5 py-2 bg-navy hover:bg-navy-dark text-white text-xs font-semibold rounded-lg transition-colors">+ Post Expense</button>
          </div>
          <p className="text-sm text-gray-400 py-6 text-center">No updates posted yet.</p>
        </div>
        <div className="border border-gray-200 rounded-xl p-5">
          <p className="font-bold text-navy mb-3">P&amp;L Summary</p>
          {pnl.map(([l, v, c], i) => (
            <div key={l} className={`flex justify-between py-2.5 text-sm ${i < pnl.length - 1 ? 'border-b border-gray-100' : ''}`}>
              <span className="text-gray-600">{l}</span>
              <span className={`font-bold ${c || 'text-navy'}`}>{v}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Real backend detail — project meta, approvals, resources, progress */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <div className="lg:col-span-2 space-y-5">
        {/* Project details */}
        <section>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Project Details</p>
          <div className="grid grid-cols-2 gap-x-8 gap-y-3">
            <MetaRow label="Type"            value={project.type} />
            <MetaRow label="Department"      value={project.departmentId ? departmentName(project.departmentId) : null} />
            <MetaRow label="Start Date"      value={fmtDate(project.startDate)} />
            <MetaRow label="Expected End"    value={fmtDate(project.expectedEndDate)} />
            {project.actualEndDate && <MetaRow label="Actual End" value={fmtDate(project.actualEndDate)} />}
            {project.tenderReference && <MetaRow label="Tender Ref" value={project.tenderReference} />}
            {project.clientReference && <MetaRow label="Client Ref" value={project.clientReference} />}
            {project.notes && <div className="col-span-2"><MetaRow label="Notes" value={project.notes} /></div>}
          </div>
        </section>

        {/* Approvals */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Approvals</p>
          </div>
          {!project.approvals?.length ? (
            <p className="text-sm text-gray-400">No approvals yet.</p>
          ) : (
            <div className="space-y-2">
              {project.approvals?.map(a => (
                <div key={a.id} className="flex items-center justify-between bg-gray-50 rounded-lg px-4 py-3">
                  <div>
                    <p className="text-sm font-semibold text-gray-800">{a.approvalType} Approval</p>
                    {a.comments && <p className="text-xs text-gray-500 mt-0.5">{a.comments}</p>}
                    {a.reviewedAt && <p className="text-xs text-gray-400">{fmtDate(a.reviewedAt)}</p>}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${a.status === 'Approved' ? 'bg-green-100 text-green-700' : a.status === 'Rejected' ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-700'}`}>
                      {a.status}
                    </span>
                    {a.status === 'Pending' && (
                      <button
                        onClick={() => openModal('processApproval', { approvalId: a.id, approvalType: a.approvalType })}
                        className="text-xs font-medium text-navy hover:text-navy bg-zinc-50 px-2 py-1 rounded-lg transition-colors"
                      >
                        Process
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* Resources */}
        <section>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Team / Resources</p>
          {!project.resources?.length ? (
            <p className="text-sm text-gray-400">No resources assigned.</p>
          ) : (
            <div className="divide-y divide-gray-100">
              {project.resources?.map(r => (
                <div key={r.id} className="flex items-center justify-between py-2">
                  <div>
                    <p className="text-sm font-medium text-gray-800">{userName(r.userId)}</p>
                    <p className="text-xs text-gray-500">{r.role}</p>
                  </div>
                  <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${r.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                    {r.isActive ? 'Active' : 'Inactive'}
                  </span>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>

      {/* Right sidebar — Budget Burn/HSE live here too (not full-width below) so this column's
          height tracks the left column instead of leaving a large empty gap under it. */}
      <div className="space-y-4">
        <div className="bg-gray-50 rounded-xl p-4">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Progress</p>
          <div className="space-y-3">
            <Stat label="Milestones" value={project.milestoneCount ?? 0} />
            <Stat label="Tasks" value={project.taskCount ?? 0} />
            <Stat label="Status" value={statusLabel === 'PendingApproval' ? 'Pending Approval' : statusLabel} />
          </div>
        </div>
        <ProjectComplianceCard project={project} stacked />
      </div>
      </div>
    </div>
  )
}

function PlanTab({ project, openModal, onTaskStatus, onMilestoneStatus, onSignOff, onUpdate, userName, projectId }) {
  const editable = !['Completed', 'Closed', 'Cancelled'].includes(project.status)  // string status
  return (
    <div className="space-y-6">
      {/* Budget lines */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Budget</p>
          {editable && (
            <button onClick={() => openModal('addBudgetLine')} className="text-xs font-medium text-navy hover:text-navy-dark">+ Add Line</button>
          )}
        </div>
        {project.budgetLines?.length === 0 ? (
          <p className="text-sm text-gray-400">No budget lines. Add at least one before submitting for approval.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Category</th>
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Description</th>
                  <th className="text-right px-4 py-2 text-xs font-semibold text-gray-500">Planned</th>
                  <th className="text-right px-4 py-2 text-xs font-semibold text-gray-500">Actual</th>
                  <th className="text-right px-4 py-2 text-xs font-semibold text-gray-500">Variance</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {project.budgetLines?.map(l => {
                  const v = l.plannedAmount - l.actualAmount
                  return (
                    <tr key={l.id}>
                      <td className="px-4 py-2.5 text-gray-600">{BUDGET_CATS[l.category]}</td>
                      <td className="px-4 py-2.5 text-gray-800">{l.description}</td>
                      <td className="px-4 py-2.5 text-right font-medium">{fmt(l.plannedAmount)}</td>
                      <td className="px-4 py-2.5 text-right text-gray-600">{fmt(l.actualAmount)}</td>
                      <td className={`px-4 py-2.5 text-right font-medium ${v < 0 ? 'text-red-500' : 'text-green-600'}`}>{v < 0 ? '-' : ''}{fmt(Math.abs(v))}</td>
                    </tr>
                  )
                })}
              </tbody>
              <tfoot>
                <tr className="bg-gray-50 font-semibold border-t border-gray-200">
                  <td colSpan={2} className="px-4 py-2.5 text-gray-700">Total</td>
                  <td className="px-4 py-2.5 text-right">{fmt(project.plannedBudget)}</td>
                  <td className="px-4 py-2.5 text-right">{fmt(project.actualCost)}</td>
                  <td className={`px-4 py-2.5 text-right ${project.plannedBudget - project.actualCost < 0 ? 'text-red-500' : 'text-green-600'}`}>
                    {fmt(Math.abs(project.plannedBudget - project.actualCost))}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </section>

      {/* Milestones + Tasks */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Milestones & Tasks</p>
          {editable && (
            <button onClick={() => openModal('addMilestone')} className="text-xs font-medium text-navy hover:text-navy-dark">+ Add Milestone</button>
          )}
        </div>

        {project.milestones?.length === 0 ? (
          <p className="text-sm text-gray-400">No milestones yet. Add at least one before submitting for approval.</p>
        ) : (
          <div className="space-y-4">
            {project.milestones?.map(m => (
              <MilestoneCard
                key={m.id} milestone={m} openModal={openModal}
                canEdit={editable}
                onTaskStatus={(milestoneId, taskId, s) => onTaskStatus(projectId, milestoneId, taskId, s)}
                onMilestoneStatus={(milestoneId, s) => onMilestoneStatus(milestoneId, s)}
                onSignOff={onSignOff} onUpdate={onUpdate} userName={userName}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

function MilestoneCard({ milestone, openModal, canEdit, onTaskStatus, onMilestoneStatus, onSignOff, onUpdate, userName }) {
  const [open, setOpen] = useState(true)
  const [statusOpen, setStatusOpen] = useState(false)
  const statusLabel = MS_LABEL[milestone.status] ?? milestone.status ?? 'Not Started'
  const statusColor = MS_COLOR[milestone.status] ?? 'bg-gray-100 text-gray-600'

  return (
    <div className="border border-gray-200 rounded-xl overflow-hidden">
      <div className="flex items-center justify-between px-5 py-3 bg-gray-50">
        <div className="flex items-center gap-3 cursor-pointer flex-1" onClick={() => setOpen(o => !o)}>
          <svg className={`w-4 h-4 text-gray-400 transition-transform ${open ? 'rotate-90' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
          </svg>
          <div>
            <p className="font-semibold text-gray-900 text-sm">{milestone.title}</p>
            <p className="text-xs text-gray-400">Due {fmtDate(milestone.dueDate)} · {milestone.plannedAmount ? fmt(milestone.plannedAmount) : '—'}</p>
          </div>
        </div>
        {/* Milestone status badge — clickable when canEdit */}
        <div className="relative" onClick={e => e.stopPropagation()}>
          <button
            onClick={() => canEdit && setStatusOpen(v => !v)}
            className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${statusColor} ${canEdit ? 'cursor-pointer hover:opacity-80' : 'cursor-default'}`}
          >
            {statusLabel} {canEdit && '▾'}
          </button>
          {statusOpen && canEdit && (
            <div className="absolute right-0 mt-1 w-40 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
              {MS_NAMES.map((name) => (
                <button
                  key={name}
                  onClick={() => { onMilestoneStatus(milestone.id, name); setStatusOpen(false) }}
                  className={`w-full text-left px-4 py-2 text-xs hover:bg-gray-50 ${milestone.status === name ? 'font-semibold text-navy' : 'text-gray-700'}`}
                >
                  {MS_LABEL[name]}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {open && (
        <div className="px-5 py-4">
          {/* O11 — milestone meta + sign-off / daily-update actions */}
          <div className="flex flex-wrap items-center gap-2 mb-3 text-xs">
            <span className="px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">{milestone.progressPct ?? 0}% complete</span>
            {milestone.isBillable && <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 font-medium">Billable</span>}
            {milestone.ldApplies && milestone.ldAmount > 0 && <span className="px-2 py-0.5 rounded-full bg-red-100 text-red-700 font-medium">LD {fmt(milestone.ldAmount)}</span>}
            {milestone.signOffAt
              ? <span className="px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium">Signed off · {milestone.signOffBy}</span>
              : milestone.lastUpdatedAt && <span className="text-gray-400">Updated {fmtDate(milestone.lastUpdatedAt)}</span>}
            {canEdit && !milestone.signOffAt && onSignOff && (
              <div className="ml-auto flex gap-3">
                <button onClick={() => onUpdate(milestone)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">Log Update</button>
                <button onClick={() => onSignOff(milestone)} className="text-xs font-semibold text-green-600 hover:text-green-800">Sign Off</button>
              </div>
            )}
          </div>
          <div className="divide-y divide-gray-50">
          {milestone.tasks?.length === 0 && canEdit ? (
            <p className="text-sm text-gray-400 py-2">No tasks yet.</p>
          ) : null}
          {milestone.tasks?.map(task => (
            <TaskRow
              key={task.id}
              task={task}
              canEdit={canEdit}
              userName={userName}
              openModal={openModal}
              milestoneId={milestone.id}
              onStatusChange={s => onTaskStatus(milestone.id, task.id, s)}
            />
          ))}
          {canEdit && (
            <div className="pt-3">
              <button
                onClick={() => openModal('addTask', { milestoneId: milestone.id })}
                className="text-xs font-medium text-navy hover:text-navy-dark transition-colors"
              >
                + Add Task
              </button>
            </div>
          )}
          </div>
        </div>
      )}
    </div>
  )
}

function TaskRow({ task, canEdit, userName, onStatusChange, openModal, milestoneId }) {
  const [open, setOpen] = useState(false)
  const dispatched = !!task.linkedAssignmentId
  return (
    <div className="flex items-center justify-between py-2.5">
      <div>
        <p className="text-sm font-medium text-gray-800">{task.title || 'Untitled task'}</p>
        {task.description && <p className="text-xs text-gray-500 mt-0.5">{task.description}</p>}
        {task.assignedToUserId && <p className="text-xs text-gray-400">Assigned: {userName ? userName(task.assignedToUserId) : task.assignedToUserId}</p>}
        {dispatched && <p className="text-xs text-green-600 mt-0.5">● Dispatched to Tasks</p>}
        {task.dueDate && <p className="text-xs text-gray-400">Due: {fmtDate(task.dueDate)}</p>}
      </div>
      <div className="flex items-center gap-2 ml-3 flex-shrink-0">
        {canEdit && openModal && (
          <button
            onClick={() => openModal('dispatchTask', { milestoneId, taskId: task.id, assignedToUserId: task.assignedToUserId ?? '', isReassign: dispatched })}
            className="px-2.5 py-0.5 rounded-lg text-xs font-medium text-navy bg-zinc-50 hover:opacity-80 transition-colors"
          >
            {dispatched ? 'Reassign' : 'Dispatch'}
          </button>
        )}
        <div className="relative" onClick={e => e.stopPropagation()}>
        <button
          onClick={() => canEdit && setOpen(v => !v)}
          className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${TASK_COLOR[task.status] ?? 'bg-gray-100 text-gray-600'} ${canEdit ? 'cursor-pointer hover:opacity-80' : 'cursor-default'}`}
        >
          {TASK_LABEL[task.status] ?? task.status} {canEdit && '▾'}
        </button>
        {open && canEdit && (
          <div className="absolute right-0 mt-1 w-36 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
            {TASK_NAMES.map((name) => (
              <button
                key={name}
                onClick={() => { onStatusChange(name); setOpen(false) }}
                className={`w-full text-left px-4 py-2 text-xs hover:bg-gray-50 ${task.status === name ? 'font-semibold text-navy' : 'text-gray-700'}`}
              >
                {TASK_LABEL[name]}
              </button>
            ))}
          </div>
        )}
        </div>
      </div>
    </div>
  )
}

function ExecutionTab({ costs, reports, openModal, project }) {
  const isActive = project.status === 'Active'

  return (
    <div className="space-y-6">
      {/* Daily Reports */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Daily Reports</p>
          {isActive && (
            <button onClick={() => openModal('submitReport', { reportDate: new Date().toISOString().split('T')[0] })}
              className="text-xs font-medium text-navy hover:text-navy-dark">+ Submit Report</button>
          )}
        </div>
        {reports.length === 0 ? (
          <p className="text-sm text-gray-400">No daily reports yet.</p>
        ) : (
          <div className="space-y-3">
            {reports.map(r => (
              <div key={r.id} className="bg-gray-50 rounded-xl p-4">
                <div className="flex items-center justify-between mb-2">
                  <p className="text-sm font-semibold text-gray-800">{fmtDate(r.reportDate)}</p>
                  <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${r.isReviewed ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'}`}>
                    {r.isReviewed ? 'Reviewed' : 'Pending Review'}
                  </span>
                </div>
                <p className="text-sm text-gray-700"><span className="font-medium">Work done:</span> {r.workDone}</p>
                {r.issues && <p className="text-sm text-red-600 mt-1"><span className="font-medium">Issues:</span> {r.issues}</p>}
                {r.plannedForTomorrow && <p className="text-sm text-gray-500 mt-1"><span className="font-medium">Tomorrow:</span> {r.plannedForTomorrow}</p>}
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Cost Entries */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Cost Entries</p>
          {isActive && (
            <button onClick={() => openModal('logCost', { entryDate: new Date().toISOString().split('T')[0] })}
              className="text-xs font-medium text-navy hover:text-navy-dark">+ Log Cost</button>
          )}
        </div>
        {costs.length === 0 ? (
          <p className="text-sm text-gray-400">No costs logged yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Date</th>
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Category</th>
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Description</th>
                  <th className="text-right px-4 py-2 text-xs font-semibold text-gray-500">Amount</th>
                  <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Ref</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {costs.map(c => (
                  <tr key={c.id}>
                    <td className="px-4 py-2.5 text-gray-500">{fmtDate(c.entryDate)}</td>
                    <td className="px-4 py-2.5 text-gray-600">{BUDGET_CATS[c.category]}</td>
                    <td className="px-4 py-2.5 text-gray-800">{c.description}</td>
                    <td className="px-4 py-2.5 text-right font-medium text-gray-900">{fmt(c.amount)}</td>
                    <td className="px-4 py-2.5 text-gray-400 text-xs">{c.reference ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function HistoryTab({ history }) {
  if (history.length === 0) return <p className="text-sm text-gray-400 py-4 text-center">No history yet.</p>
  return (
    <div className="space-y-0">
      {history.map((h, i) => (
        <div key={h.id} className="flex gap-3 pb-4">
          <div className="flex flex-col items-center">
            <div className="w-2.5 h-2.5 rounded-full bg-gold mt-1 shrink-0" />
            {i < history.length - 1 && <div className="w-px flex-1 bg-gray-200 mt-1" />}
          </div>
          <div className="pb-1">
            <p className="text-sm font-medium text-gray-800">{h.action}</p>
            {(h.fromValue || h.toValue) && (
              <p className="text-xs text-gray-500 mt-0.5">
                {h.fromValue && <span className="line-through mr-1">{h.fromValue}</span>}
                {h.toValue && <span className="text-green-700 font-medium">{h.toValue}</span>}
              </p>
            )}
            {h.notes && <p className="text-xs text-gray-400 italic mt-0.5">{h.notes}</p>}
            <p className="text-xs text-gray-400 mt-0.5">{fmtDate(h.occurredAt)}</p>
          </div>
        </div>
      ))}
    </div>
  )
}

/* ── Frontend-only tabs (mirror the deployed QSL layout; no backend yet — see memory) ── */

function TimesheetsTab() {
  return (
    <div className="space-y-4">
      <div className="flex items-start gap-2 bg-blue-50 border border-blue-100 text-blue-800 rounded-lg px-4 py-3 text-sm">
        <span>ℹ️</span>
        <span>Hours logged by staff against this project (My Workspace → Timesheet). Approving locks the entry into project labour cost at the employee's derived hourly rate.</span>
      </div>
      <div className="overflow-x-auto border border-gray-100 rounded-xl">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-navy text-white">
              {['Date','Employee','Hours','Description','Labour Cost','Status'].map(h => (
                <th key={h} className="text-left px-4 py-2.5 text-xs font-semibold uppercase tracking-wide">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr><td colSpan={6} className="px-4 py-12 text-center text-gray-400">No hours logged against this project.</td></tr>
          </tbody>
        </table>
      </div>
    </div>
  )
}

function SubcontractorsTab({ projectId }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      try {
        // Subcontracts module has no server-side project filter on these lists (see
        // SubcontractAwardsController) — pull a large page and filter client-side, same
        // pattern the Subcontracts module page itself uses for its own lookups.
        const [awardsRes, retentionsRes] = await Promise.all([
          api.get('/api/v1/subcontract-awards', { params: { pageSize: 200 } }),
          api.get('/api/v1/payment-retentions', { params: { pageSize: 200 } }),
        ])
        const awards = (awardsRes.data?.data?.items ?? awardsRes.data?.data ?? []).filter(a => a.projectId === projectId)
        const retentions = retentionsRes.data?.data?.items ?? retentionsRes.data?.data ?? []
        if (cancelled) return
        setRows(awards.map(a => {
          const awardRetentions = retentions.filter(r => r.awardId === a.id)
          const paid = awardRetentions.filter(r => r.status === 'Paid').reduce((s, r) => s + (r.certifiedAmount || 0), 0)
          const retentionHeld = awardRetentions.reduce((s, r) => s + (r.retentionHeld || 0), 0)
          return { ...a, paid, retentionHeld, balance: (a.value || 0) - paid }
        }))
      } catch {
        if (!cancelled) setRows([])
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => { cancelled = true }
  }, [projectId])

  return (
    <div className="overflow-x-auto border border-gray-100 rounded-xl">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-navy text-white">
            {['Subcontractor','Contract','Paid','Retention','Balance','RAMS','Status'].map(h => (
              <th key={h} className="text-left px-4 py-2.5 text-xs font-semibold uppercase tracking-wide">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            <tr><td colSpan={7} className="px-4 py-12 text-center text-gray-400">Loading…</td></tr>
          ) : rows.length === 0 ? (
            <tr><td colSpan={7} className="px-4 py-12 text-center text-gray-400">No subcontractors awarded on this project.</td></tr>
          ) : rows.map(r => (
            <tr key={r.id} className="border-t border-gray-100">
              <td className="px-4 py-2.5 font-semibold text-gray-800">{r.subcontractorName}</td>
              <td className="px-4 py-2.5">{fmt(r.value)}</td>
              <td className="px-4 py-2.5">{fmt(r.paid)}</td>
              <td className="px-4 py-2.5">{fmt(r.retentionHeld)}</td>
              <td className="px-4 py-2.5">{fmt(r.balance)}</td>
              <td className="px-4 py-2.5">
                <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${r.ramsApproved ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'}`}>
                  {r.ramsApproved ? 'Approved' : 'Not Confirmed'}
                </span>
              </td>
              <td className="px-4 py-2.5">
                <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${r.status === 'Mobilized' || r.status === 'Completed' ? 'bg-green-100 text-green-700' : r.status === 'Terminated' ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-700'}`}>
                  {r.status}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function HandoverTab() {
  const sigs = [
    ['Signature 1 of 4', 'Outgoing Process Owner'],
    ['Signature 2 of 4', 'Client / Incoming Representative'],
    ['Signature 3 of 4', 'Department Head'],
    ['Signature 4 of 4', 'Managing Director'],
  ]
  return (
    <div className="space-y-4">
      <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 text-amber-800 rounded-lg px-4 py-3 text-sm">
        <span>⚠️</span>
        <span><strong>POH-002:</strong> 4 digital signatures mandatory before project closure: Outgoing PM, Client/Incoming PM, Dept Head, MD.</span>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {sigs.map(([n, role]) => (
          <div key={n} className="border border-dashed border-gray-300 rounded-xl px-5 py-4">
            <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide mb-1.5">{n}</p>
            <p className="text-sm font-bold text-navy mb-2">{role}</p>
            <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-700">Awaiting Signature</span>
          </div>
        ))}
      </div>
      <div className="bg-red-50 text-red-600 rounded-lg px-4 py-3 text-sm font-semibold">🔴 Project CANNOT be closed until all 4 signatures are applied.</div>
    </div>
  )
}

/* ── Shared helpers ───────────────────────────── */

function ActionBtn({ label, color, onClick }) {
  const colors = { amber:'bg-amber-50 text-amber-700 hover:bg-amber-100', green:'bg-green-50 text-green-700 hover:bg-green-100', orange:'bg-orange-50 text-orange-700 hover:bg-orange-100', blue:'bg-blue-50 text-blue-700 hover:bg-blue-100', purple:'bg-purple-50 text-purple-700 hover:bg-purple-100', gray:'bg-gray-100 text-gray-700 hover:bg-gray-200', red:'bg-red-50 text-red-700 hover:bg-red-100' }
  return <button onClick={onClick} className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors ${colors[color] ?? colors.gray}`}>{label}</button>
}

function KPI({ label, value, sub, subColor }) {
  return (
    <div>
      <p className="text-xs text-gray-400 mb-0.5">{label}</p>
      <p className="text-lg font-bold text-gray-900">{value}</p>
      {sub && <p className={`text-xs font-medium ${subColor}`}>{sub}</p>}
    </div>
  )
}

function MetaRow({ label, value }) {
  return (
    <div>
      <p className="text-xs text-gray-400">{label}</p>
      <p className="text-sm font-medium text-gray-800">{value ?? '—'}</p>
    </div>
  )
}

function Stat({ label, value }) {
  return (
    <div className="flex justify-between">
      <span className="text-xs text-gray-500">{label}</span>
      <span className="text-xs font-semibold text-gray-800">{value}</span>
    </div>
  )
}

function ModalShell({ title, onClose, children }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-navy">{title}</h2>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 transition-colors">
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

function ModalFooter({ onCancel, onConfirm, loading, confirmLabel }) {
  return (
    <div className="flex items-center justify-end gap-3 mt-6">
      <button onClick={onCancel} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
      <button onClick={onConfirm} disabled={loading} className="px-5 py-2 bg-navy hover:bg-navy-dark disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors">
        {loading ? 'Saving…' : confirmLabel}
      </button>
    </div>
  )
}

function Field({ label, children }) {
  return (
    <div>
      <label className="block text-sm font-semibold text-gray-700 mb-1.5">{label}</label>
      {children}
    </div>
  )
}

function ErrBox({ msg }) {
  return <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{msg}</div>
}

function LoadingScreen() {
  return (
    <>
      <div className="flex-1 flex items-center justify-center">
        <p className="text-gray-400 text-sm animate-pulse">Loading project…</p>
      </div>
    </>
  )
}

function ErrorScreen({ msg, onBack }) {
  return (
    <>
      <div className="flex-1 flex flex-col items-center justify-center gap-3">
        <p className="text-gray-500">{msg || 'Project not found.'}</p>
        <button onClick={onBack} className="text-navy text-sm font-medium">Back to projects</button>
      </div>
    </>
  )
}
