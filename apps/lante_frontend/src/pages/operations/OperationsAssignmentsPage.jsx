import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import { exportAssignmentsPDF, exportAssignmentsExcel } from '../../utils/assignmentExports.js'

const STATUS_BADGE = {
  Pending: 'bg-amber-100 text-amber-700',
  Accepted: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Completed: 'bg-indigo-100 text-indigo-700',
  Cancelled: 'bg-red-100 text-red-600',
  AwaitingProjectLink: 'bg-purple-100 text-purple-700',
  Archived: 'bg-gray-100 text-gray-500',
}

const STATUS_ENUM = {
  Pending: 0, Accepted: 1, Declined: 2, InProgress: 3,
  Completed: 4, Cancelled: 5, AwaitingProjectLink: 6, Archived: 7,
}

const PRIORITY_BADGE = {
  Low: 'bg-gray-100 text-gray-600',
  Normal: 'bg-blue-100 text-blue-700',
  High: 'bg-orange-100 text-orange-700',
  Critical: 'bg-red-100 text-red-700',
}

export default function OperationsAssignmentsPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canWrite = hasPermission('operations.write')
  const canApprove = hasPermission('operations.approve')
  const canCreate = canWrite || hasPermission('operations.read.own')
  const canExport = hasPermission('reports.export')
  const canReadUsers = hasPermission('users.read')
  const [assignments, setAssignments] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [view, setView] = useState('all')
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [creating, setCreating] = useState(false)
  const [form, setForm] = useState({
    title: '', description: '', departmentId: '', departmentType: '',
    priority: 'Normal', natureOfVisit: 'Maintenance',
    locationName: '', locationAddress: '', notes: '',
    technicianIds: [], technicianNames: [],
  })
  const [pendingFiles, setPendingFiles] = useState([])
  const [users, setUsers] = useState([])
  const [departments, setDepartments] = useState([])

  // Link-to-project modal state
  const [linkTarget, setLinkTarget] = useState(null) // assignment being linked
  const [projects, setProjects] = useState([])
  const [milestones, setMilestones] = useState([])
  const [linkForm, setLinkForm] = useState({ projectId: '', milestoneId: '' })
  const [linkLoading, setLinkLoading] = useState(false)

  // Archive modal state
  const [archiveTarget, setArchiveTarget] = useState(null)
  const [archiveReason, setArchiveReason] = useState('')
  const [archiveLoading, setArchiveLoading] = useState(false)

  const pageSize = 100

  // Deployed "Tasks" quick-filter pills (client-side over the loaded page).
  const [pill, setPill] = useState('all')  // all | pending | overdue | critical

  const fetchAssignments = useCallback(async () => {
    setLoading(true)
    try {
      const url = view === 'pending-linkage'
        ? '/api/v1/assignments/standalone/pending'
        : '/api/v1/assignments'
      const params = new URLSearchParams({ page, pageSize, sortDescending: true })
      if (search) params.set('search', search)
      if (statusFilter && view !== 'pending-linkage') params.set('status', STATUS_ENUM[statusFilter])
      const res = await api.get(`${url}?${params}`)
      setAssignments(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setAssignments([])
    } finally {
      setLoading(false)
    }
  }, [page, search, statusFilter, view])

  useEffect(() => { fetchAssignments() }, [fetchAssignments])

  useEffect(() => {
    if (!canReadUsers) return
    api.get('/api/v1/users?pageSize=200').then(res => setUsers(res.data?.data?.items ?? [])).catch(() => {})
  }, [canReadUsers])

  useEffect(() => {
    api.get('/api/v1/departments').then(res => {
      const raw = res.data?.data ?? res.data ?? []
      setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
    }).catch(() => {})
  }, [])

  const handleCreate = async (e) => {
    e.preventDefault()
    setCreating(true)
    try {
      const endpoint = canWrite ? '/api/v1/assignments' : '/api/v1/assignments/standalone'
      const res = await api.post(endpoint, { ...form, sourceType: 'Standalone' })
      const newId = res.data?.data?.id
      if (newId && pendingFiles.length > 0) {
        for (const file of pendingFiles) {
          const fd = new FormData()
          fd.append('file', file)
          fd.append('entityType', 'Assignment')
          fd.append('entityId', newId)
          await api.post('/api/v1/attachments', fd).catch(() => {})
        }
      }
      setShowCreate(false)
      setForm({ title: '', description: '', departmentId: '', departmentType: '', priority: 'Normal', natureOfVisit: 'Maintenance', locationName: '', locationAddress: '', notes: '', technicianIds: [], technicianNames: [] })
      setPendingFiles([])
      fetchAssignments()
    } finally {
      setCreating(false)
    }
  }

  const openLinkModal = async (e, assignment) => {
    e.stopPropagation()
    try {
      const res = await api.get('/api/v1/projects?pageSize=200&status=Planning')
      setProjects(res.data?.data?.items ?? [])
    } catch { setProjects([]) }
    setLinkForm({ projectId: '', milestoneId: '' })
    setMilestones([])
    setLinkTarget(assignment)
  }

  const onProjectPicked = async (projectId) => {
    setLinkForm(f => ({ ...f, projectId, milestoneId: '' }))
    if (!projectId) { setMilestones([]); return }
    try {
      const res = await api.get(`/api/v1/projects/${projectId}/milestones`)
      setMilestones(res.data?.data ?? [])
    } catch { setMilestones([]) }
  }

  const handleLinkToProject = async (e) => {
    e.preventDefault()
    setLinkLoading(true)
    try {
      await api.post(`/api/v1/assignments/${linkTarget.id}/link-to-project`, linkForm)
      setLinkTarget(null)
      fetchAssignments()
    } finally { setLinkLoading(false) }
  }

  const openArchiveModal = (e, assignment) => {
    e.stopPropagation()
    setArchiveReason('')
    setArchiveTarget(assignment)
  }

  const handleArchive = async (e) => {
    e.preventDefault()
    setArchiveLoading(true)
    try {
      await api.post(`/api/v1/assignments/${archiveTarget.id}/archive`, { reason: archiveReason })
      setArchiveTarget(null)
      fetchAssignments()
    } finally { setArchiveLoading(false) }
  }

  const totalPages = Math.ceil(totalCount / pageSize)

  // KPIs + pill filter, mapped from assignment fields (a task IS an assignment).
  const CLOSED = ['Completed', 'Cancelled', 'Archived', 'Declined']
  const isOpen = a => !CLOSED.includes(a.status)
  const isOverdue = a => a.deadline && isOpen(a) && new Date(a.deadline) < new Date(new Date().toDateString())
  const kpiOverdue = assignments.filter(isOverdue).length
  const kpiCritical = assignments.filter(a => a.priority === 'Critical' && isOpen(a)).length
  const kpiCompleted = assignments.filter(a => a.status === 'Completed').length
  const visible = assignments.filter(a => {
    if (pill === 'pending') return isOpen(a)
    if (pill === 'overdue') return isOverdue(a)
    if (pill === 'critical') return a.priority === 'Critical' && isOpen(a)
    return true
  })

  const PILLS = [
    { id: 'all', label: 'All' },
    { id: 'pending', label: 'Pending' },
    { id: 'overdue', label: 'Overdue' },
    { id: 'critical', label: 'Critical' },
  ]

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Tasks</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${totalCount} task${totalCount !== 1 ? 's' : ''}`}
            </p>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {canApprove && (
              <>
                <button
                  onClick={() => { setView('all'); setPage(1) }}
                  className={`px-3.5 py-2.5 text-sm font-semibold rounded-lg border transition-colors ${view === 'all' ? 'bg-navy text-white border-navy' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}
                >
                  All
                </button>
                <button
                  onClick={() => { setView('pending-linkage'); setPage(1) }}
                  className={`px-3.5 py-2.5 text-sm font-semibold rounded-lg border transition-colors ${view === 'pending-linkage' ? 'bg-purple-600 text-white border-purple-600' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}
                >
                  Pending Linkage
                </button>
              </>
            )}
            {canExport && (
              <>
                <button
                  onClick={() => exportAssignmentsPDF(assignments)}
                  className="inline-flex items-center gap-1.5 px-3.5 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 2v6h6M16 13H8M16 17H8M10 9H8"/></svg>
                  PDF
                </button>
                <button
                  onClick={() => exportAssignmentsExcel(assignments)}
                  className="inline-flex items-center gap-1.5 px-3.5 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><rect x="3" y="3" width="18" height="18" rx="2"/><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 3v18M3 9h18"/></svg>
                  Excel
                </button>
              </>
            )}
            {canCreate && (
              <button
                onClick={() => setShowCreate(true)}
                className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                New Task
              </button>
            )}
          </div>
        </div>

        {/* KPIs */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
          {[
            { label: 'Total',     value: totalCount,   icon: '☑️', cls: 'bg-navy/10 text-navy',      val: 'text-gray-900' },
            { label: 'Overdue',   value: kpiOverdue,   icon: '🔴', cls: 'bg-red-100 text-red-700',   val: 'text-red-600' },
            { label: 'Critical',  value: kpiCritical,  icon: '⚡', cls: 'bg-amber-100 text-amber-700', val: 'text-gray-900' },
            { label: 'Completed', value: kpiCompleted, icon: '✅', cls: 'bg-green-100 text-green-700', val: 'text-green-600' },
          ].map(k => (
            <div key={k.label} className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
              <div className="flex items-center justify-between">
                <div className={`w-9 h-9 rounded-xl flex items-center justify-center text-base ${k.cls}`}>{k.icon}</div>
                <p className={`text-2xl font-extrabold ${k.val}`}>{k.value}</p>
              </div>
              <p className="text-[11px] text-gray-500 mt-2 font-semibold uppercase tracking-wider">{k.label}</p>
            </div>
          ))}
        </div>

        {/* Quick filter pills (deployed Tasks look) */}
        <div className="flex flex-wrap gap-2 mb-5">
          {PILLS.map(f => (
            <button
              key={f.id}
              onClick={() => setPill(f.id)}
              className={`px-4 py-1.5 rounded-lg text-sm font-semibold border transition-colors ${pill === f.id ? 'bg-navy text-white border-navy' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'}`}
            >
              {f.label}
            </button>
          ))}
        </div>

        {/* Filters */}
        <div className="bg-white rounded-xl border border-gray-200 p-4 mb-5">
          <div className="flex flex-wrap gap-3">
            <input
              value={search}
              onChange={e => { setSearch(e.target.value); setPage(1) }}
              placeholder="Search tasks…"
              className="flex-1 min-w-[200px] px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
            />
            <select
              value={statusFilter}
              onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
              className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold bg-white"
            >
              <option value="">All Statuses</option>
              {['Pending','Accepted','Declined','InProgress','Completed','Cancelled','AwaitingProjectLink','Archived'].map(s => (
                <option key={s} value={s}>
                  {s === 'AwaitingProjectLink' ? 'Awaiting Project Link' : s === 'InProgress' ? 'In Progress' : s}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <div className="space-y-3">
            {[1,2,3,4,5].map(i => <div key={i} className="h-16 bg-white rounded-xl border border-gray-100 animate-pulse" />)}
          </div>
        ) : visible.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="text-4xl mb-3">📋</div>
            <h3 className="font-semibold text-gray-700">No tasks found.</h3>
            <p className="text-sm text-gray-400 mt-1">{pill === 'all' ? 'Create your first task to get started.' : 'No tasks match this filter.'}</p>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-gray-200 overflow-x-auto">
            <table className="w-full text-sm min-w-[600px]">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-5 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Title</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden md:table-cell">Department</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden sm:table-cell">Priority</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Status</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Nature</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Technicians</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Created</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {visible.map(a => (
                  <tr
                    key={a.id}
                    onClick={() => navigate(`/modules/operations/assignments/${a.id}`)}
                    className="hover:bg-offwhite cursor-pointer transition-colors"
                  >
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span className="font-medium text-gray-900">{a.title}</span>
                        {a.sourceType === 'ProjectTask' && (
                          <span className="text-xs font-semibold text-blue-700 bg-blue-50 border border-blue-200 px-1.5 py-0.5 rounded">Project</span>
                        )}
                        {a.sourceType === 'Ticket' && (
                          <span className="text-xs font-semibold text-orange-700 bg-orange-50 border border-orange-200 px-1.5 py-0.5 rounded">Ticket</span>
                        )}
                      </div>
                      {a.locationName && <div className="text-xs text-gray-400 mt-0.5">{a.locationName}</div>}
                      {a.linkedProjectTaskId && <div className="text-xs text-blue-500 mt-0.5">↗ Linked to project task</div>}
                    </td>
                    <td className="px-4 py-4 text-gray-500 hidden md:table-cell">{a.departmentType}</td>
                    <td className="px-4 py-4 hidden sm:table-cell">
                      <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${PRIORITY_BADGE[a.priority] ?? 'bg-gray-100 text-gray-600'}`}>
                        {a.priority}
                      </span>
                    </td>
                    <td className="px-4 py-4">
                      <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[a.status] ?? 'bg-gray-100 text-gray-600'}`}>
                        {a.status}
                      </span>
                    </td>
                    <td className="px-4 py-4 text-gray-500 hidden lg:table-cell">{a.natureOfVisit}</td>
                    <td className="px-4 py-4 text-gray-700 hidden lg:table-cell">
                      {a.technicians?.length > 0
                        ? a.technicians.map(t => t.userName).join(', ')
                        : <span className="text-gray-300">Unassigned</span>}
                    </td>
                    <td className="px-4 py-4 text-gray-400 hidden lg:table-cell">
                      {new Date(a.createdAt).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-4 text-right">
                      {canApprove && a.sourceType === 'Standalone' ? (
                        <div className="flex items-center justify-end gap-1.5" onClick={e => e.stopPropagation()}>
                          <button
                            onClick={e => openLinkModal(e, a)}
                            className="px-2.5 py-1 text-xs font-semibold rounded-lg bg-purple-600 text-white hover:bg-purple-700 transition-colors whitespace-nowrap"
                          >
                            Link to Project
                          </button>
                          <button
                            onClick={e => openArchiveModal(e, a)}
                            className="px-2.5 py-1 text-xs font-semibold rounded-lg bg-gray-100 text-gray-600 hover:bg-gray-200 transition-colors"
                          >
                            Archive
                          </button>
                        </div>
                      ) : (
                        <svg className="w-4 h-4 text-gray-400 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                        </svg>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-5">
            <p className="text-sm text-gray-500">Page {page} of {totalPages}</p>
            <div className="flex gap-2">
              <button
                disabled={page === 1}
                onClick={() => setPage(p => p - 1)}
                className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors"
              >
                Previous
              </button>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
                className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </main>

      {/* Create Modal */}
      {showCreate && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-6 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-lg font-bold text-navy">New Task</h2>
              <button onClick={() => { setShowCreate(false); setPendingFiles([]) }} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            <form onSubmit={handleCreate} className="space-y-4">
              <MField label="Title *">
                <input type="text" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} required className="input" />
              </MField>
              <MField label="Description">
                <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} rows={3} className="input resize-none" />
              </MField>
              <div className="grid grid-cols-2 gap-3">
                <MField label="Department">
                  <select
                    value={form.departmentId}
                    onChange={e => {
                      const dept = departments.find(d => d.id === e.target.value)
                      setForm(f => ({ ...f, departmentId: e.target.value, departmentType: dept?.name ?? 'General' }))
                    }}
                    required
                    className="input"
                  >
                    <option value="">— Select department —</option>
                    {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
                  </select>
                </MField>
                <MField label="Priority">
                  <select value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value }))} className="input">
                    {['Low','Normal','High','Critical'].map(o => <option key={o}>{o}</option>)}
                  </select>
                </MField>
              </div>
              <MField label="Nature of Visit">
                <select value={form.natureOfVisit} onChange={e => setForm(f => ({ ...f, natureOfVisit: e.target.value }))} className="input">
                  {['Installation','Maintenance','Repair','Inspection','Commissioning','Survey','Training','Meeting','Other'].map(o => <option key={o}>{o}</option>)}
                </select>
              </MField>
              <MField label="Location Name">
                <input type="text" value={form.locationName} onChange={e => setForm(f => ({ ...f, locationName: e.target.value }))} className="input" />
              </MField>
              <MField label="Location Address">
                <input type="text" value={form.locationAddress} onChange={e => setForm(f => ({ ...f, locationAddress: e.target.value }))} className="input" />
              </MField>
              <MField label="Notes">
                <textarea value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} rows={3} className="input resize-none" />
              </MField>
              <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
              {canReadUsers && (
                <MField label="Assign Technicians">
                  <select
                    multiple
                    value={form.technicianIds}
                    onChange={e => {
                      const sel = Array.from(e.target.selectedOptions)
                      setForm(f => ({
                        ...f,
                        technicianIds: sel.map(o => o.value),
                        technicianNames: sel.map(o => o.dataset.name ?? o.text),
                      }))
                    }}
                    className="input min-h-[80px]"
                  >
                    {users.map(u => (
                      <option key={u.id} value={u.id} data-name={`${u.firstName} ${u.lastName}`.trim()}>
                        {u.firstName} {u.lastName}
                      </option>
                    ))}
                  </select>
                  <p className="text-xs text-gray-400 mt-1">Hold Ctrl/Cmd to select multiple</p>
                </MField>
              )}
              <div className="flex items-center justify-end gap-3 pt-2">
                <button type="button" onClick={() => { setShowCreate(false); setPendingFiles([]) }} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={creating} className="px-5 py-2 text-sm font-semibold rounded-lg bg-navy hover:bg-navy-dark text-white disabled:opacity-50 transition-colors">
                  {creating ? 'Creating…' : 'Create Assignment'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
      {/* Link to Project modal */}
      {linkTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <div className="flex items-center justify-between mb-5">
              <div>
                <h2 className="text-base font-bold text-gray-900">Link to Project</h2>
                <p className="text-xs text-gray-400 mt-0.5 truncate max-w-xs">{linkTarget.title}</p>
              </div>
              <button onClick={() => setLinkTarget(null)} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>
            <form onSubmit={handleLinkToProject} className="flex flex-col gap-4">
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
              <p className="text-xs text-gray-400">A task will be auto-created from this assignment and linked to the selected project.</p>
              <div className="flex gap-2.5 justify-end pt-1">
                <button type="button" onClick={() => setLinkTarget(null)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
                <button type="submit" disabled={linkLoading} className="px-4 py-2 rounded-lg bg-gray-900 hover:bg-gray-800 text-white text-sm font-semibold transition-colors disabled:opacity-70">
                  {linkLoading ? 'Linking…' : 'Link to Project'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Archive modal */}
      {archiveTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <div className="flex items-center justify-between mb-5">
              <div>
                <h2 className="text-base font-bold text-gray-900">Archive Assignment</h2>
                <p className="text-xs text-gray-400 mt-0.5 truncate max-w-xs">{archiveTarget.title}</p>
              </div>
              <button onClick={() => setArchiveTarget(null)} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>
            <form onSubmit={handleArchive} className="flex flex-col gap-4">
              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Reason *</label>
                <textarea required rows={3} value={archiveReason} onChange={e => setArchiveReason(e.target.value)}
                  placeholder="Why is this assignment being archived without linking to a project?"
                  className="input resize-y" />
              </div>
              <div className="flex gap-2.5 justify-end">
                <button type="button" onClick={() => setArchiveTarget(null)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
                <button type="submit" disabled={archiveLoading} className="px-4 py-2 rounded-lg bg-gray-400 hover:bg-gray-500 text-white text-sm font-semibold transition-colors disabled:opacity-70">
                  {archiveLoading ? 'Archiving…' : 'Archive'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
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
