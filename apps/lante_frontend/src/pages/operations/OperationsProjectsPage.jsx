import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import { exportProjectsListPDF, exportProjectsListExcel } from '../../utils/projectExports.js'

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

const RISK_TEXT = { Low: 'text-green-600', Medium: 'text-amber-600', High: 'text-red-600' }

export default function OperationsProjectsPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canWrite = hasPermission('projects.write')
  const canExport = hasPermission('reports.export')
  const [projects, setProjects] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [creating, setCreating] = useState(false)
  const [departments, setDepartments] = useState([])
  const [form, setForm] = useState({
    name: '', clientName: '', scopeSummary: '', type: 'Service',
    riskLevel: 'Low', contractValue: '', plannedBudget: '',
    startDate: '', expectedEndDate: '', departmentId: '',
  })
  const [pendingFiles, setPendingFiles] = useState([])
  const [exporting, setExporting] = useState(false)

  const pageSize = 15

  const fetchProjects = async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams({ page, pageSize, sortDescending: true })
      if (search) params.set('search', search)
      if (statusFilter) params.set('status', statusFilter)
      const res = await api.get(`/api/v1/projects?${params}`)
      setProjects(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setProjects([])
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { fetchProjects() }, [page, statusFilter])
  useEffect(() => { setPage(1); fetchProjects() }, [search])

  useEffect(() => {
    api.get('/api/v1/departments?pageSize=100').then(res => {
      setDepartments(res.data?.data?.items ?? res.data?.data ?? [])
    }).catch(() => {})
  }, [])

  const handleCreate = async (e) => {
    e.preventDefault()
    setCreating(true)
    try {
      const res = await api.post('/api/v1/projects', {
        ...form,
        contractValue: parseFloat(form.contractValue) || 0,
        plannedBudget: parseFloat(form.plannedBudget) || 0,
      })
      const newId = res.data?.data?.id
      if (newId && pendingFiles.length > 0) {
        for (const file of pendingFiles) {
          const fd = new FormData()
          fd.append('file', file)
          fd.append('entityType', 'Project')
          fd.append('entityId', newId)
          await api.post('/api/v1/attachments', fd).catch(() => {})
        }
      }
      setShowCreate(false)
      setForm({ name: '', clientName: '', scopeSummary: '', type: 'Service', riskLevel: 'Low', contractValue: '', plannedBudget: '', startDate: '', expectedEndDate: '', departmentId: '' })
      setPendingFiles([])
      fetchProjects()
    } finally {
      setCreating(false)
    }
  }

  const totalPages = Math.ceil(totalCount / pageSize)

  const handleExportPDF = async () => {
    setExporting(true)
    try { await exportProjectsListPDF(projects) } finally { setExporting(false) }
  }

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">Projects</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${totalCount} project${totalCount !== 1 ? 's' : ''}`}
            </p>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {canExport && (
              <>
                <button
                  onClick={handleExportPDF}
                  disabled={exporting || projects.length === 0}
                  className="inline-flex items-center gap-1.5 px-3.5 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors disabled:opacity-50"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" /></svg>
                  {exporting ? 'Generating…' : 'PDF'}
                </button>
                <button
                  onClick={() => exportProjectsListExcel(projects)}
                  disabled={projects.length === 0}
                  className="inline-flex items-center gap-1.5 px-3.5 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-600 text-sm font-medium rounded-lg transition-colors disabled:opacity-50"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><rect x="3" y="3" width="18" height="18" rx="2"/><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 3v18M3 9h18"/></svg>
                  Excel
                </button>
              </>
            )}
            {canWrite && (
              <button
                onClick={() => setShowCreate(true)}
                className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                New Project
              </button>
            )}
          </div>
        </div>

        {/* Filters */}
        <div className="bg-white rounded-xl border border-gray-200 p-4 mb-5">
          <div className="flex flex-wrap gap-3">
            <input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Search projects…"
              className="flex-1 min-w-[200px] px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
            <select
              value={statusFilter}
              onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
              className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white"
            >
              <option value="">All Statuses</option>
              {Object.keys(STATUS_BADGE).map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <div className="space-y-3">
            {[1,2,3,4,5].map(i => <div key={i} className="h-16 bg-white rounded-xl border border-gray-100 animate-pulse" />)}
          </div>
        ) : projects.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="text-4xl mb-3">📁</div>
            <h3 className="font-semibold text-gray-700">No projects found</h3>
            <p className="text-sm text-gray-400 mt-1">Create your first project to get started.</p>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-gray-200 overflow-x-auto">
            <table className="w-full text-sm min-w-[600px]">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-5 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Project</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden md:table-cell">Client</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden sm:table-cell">Type</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Status</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Risk</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Budget</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Milestones</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {projects.map(p => (
                  <tr
                    key={p.id}
                    onClick={() => navigate(`/modules/operations/projects/${p.id}`)}
                    className="hover:bg-amber-50 cursor-pointer transition-colors"
                  >
                    <td className="px-5 py-4">
                      <div className="font-medium text-gray-900">{p.name}</div>
                      {p.tenderReference && <div className="text-xs text-gray-400 mt-0.5">{p.tenderReference}</div>}
                    </td>
                    <td className="px-4 py-4 text-gray-500 hidden md:table-cell">{p.clientName ?? '—'}</td>
                    <td className="px-4 py-4 text-gray-500 hidden sm:table-cell">{p.type}</td>
                    <td className="px-4 py-4">
                      <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[p.status] ?? 'bg-gray-100 text-gray-600'}`}>
                        {p.status}
                      </span>
                    </td>
                    <td className="px-4 py-4 hidden lg:table-cell">
                      <span className={`text-xs font-semibold ${RISK_TEXT[p.riskLevel] ?? 'text-gray-500'}`}>{p.riskLevel}</span>
                    </td>
                    <td className="px-4 py-4 text-gray-700 hidden lg:table-cell">KES {(p.plannedBudget ?? 0).toLocaleString()}</td>
                    <td className="px-4 py-4 text-gray-700 hidden lg:table-cell">{p.milestoneCount ?? 0}</td>
                    <td className="px-4 py-4 text-right">
                      <svg className="w-4 h-4 text-gray-400 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                      </svg>
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
              <h2 className="text-lg font-bold text-zinc-950">New Project</h2>
              <button onClick={() => { setShowCreate(false); setPendingFiles([]) }} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            <form onSubmit={handleCreate} className="space-y-4">
              <MField label="Project Name *">
                <input type="text" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} required className="input" placeholder="e.g. Nairobi Office Fit-Out" />
              </MField>
              <MField label="Client Name">
                <input type="text" value={form.clientName} onChange={e => setForm(f => ({ ...f, clientName: e.target.value }))} className="input" />
              </MField>
              <MField label="Department *">
                <select value={form.departmentId} onChange={e => setForm(f => ({ ...f, departmentId: e.target.value }))} required className="input">
                  <option value="">Select department…</option>
                  {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </MField>
              <MField label="Scope Summary">
                <textarea value={form.scopeSummary} onChange={e => setForm(f => ({ ...f, scopeSummary: e.target.value }))} rows={3} className="input resize-none" />
              </MField>
              <div className="grid grid-cols-2 gap-3">
                <MField label="Type">
                  <select value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value }))} className="input">
                    {['Service','Construction','Calibration','ICT','CRM','Sales','General'].map(t => <option key={t}>{t}</option>)}
                  </select>
                </MField>
                <MField label="Risk Level">
                  <select value={form.riskLevel} onChange={e => setForm(f => ({ ...f, riskLevel: e.target.value }))} className="input">
                    {['Low','Medium','High'].map(r => <option key={r}>{r}</option>)}
                  </select>
                </MField>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <MField label="Contract Value">
                  <input type="number" value={form.contractValue} onChange={e => setForm(f => ({ ...f, contractValue: e.target.value }))} className="input" />
                </MField>
                <MField label="Planned Budget">
                  <input type="number" value={form.plannedBudget} onChange={e => setForm(f => ({ ...f, plannedBudget: e.target.value }))} className="input" />
                </MField>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <MField label="Start Date *">
                  <input type="date" value={form.startDate} onChange={e => setForm(f => ({ ...f, startDate: e.target.value }))} required className="input" />
                </MField>
                <MField label="Expected End Date *">
                  <input type="date" value={form.expectedEndDate} onChange={e => setForm(f => ({ ...f, expectedEndDate: e.target.value }))} required className="input" />
                </MField>
              </div>
              <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
              <div className="flex items-center justify-end gap-3 pt-2">
                <button type="button" onClick={() => { setShowCreate(false); setPendingFiles([]) }} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={creating} className="px-5 py-2 text-sm font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white disabled:opacity-50 transition-colors">
                  {creating ? 'Creating…' : 'Create Project'}
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
