import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import ProjectStatusBadge from '../../components/projects/ProjectStatusBadge.jsx'
import { DataTable, Btn } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel, PROJECT_COLUMNS } from '../../utils/export.js'

// Must match OperationsService.Core.Enums.ProjectStatus exactly — the backend matches by
// name via Enum.TryParse<ProjectStatus>(filters.Status, ...) (case-sensitive), not by ordinal.
const STATUSES  = ['Draft','Planning','PendingMdApproval','PendingFinanceApproval','Active','OnHold','Completed','Closed','Cancelled']
const STATUS_LABELS = { PendingMdApproval: 'Pending MD Approval', PendingFinanceApproval: 'Pending Finance Approval' }
const TYPES     = ['Service','Construction','Calibration']

export default function ProjectsPage() {
  const navigate = useNavigate()

  const [projects, setProjects]     = useState([])
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')
  const [page, setPage]             = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal]           = useState(0)

  const [search, setSearch]       = useState('')
  const [statusF, setStatusF]     = useState('')
  const [typeF, setTypeF]         = useState('')
  const [exporting, setExporting] = useState(false)
  const [exportMenu, setExportMenu] = useState(false)
  // Card vs list layout — remembered across visits.
  const [layout, setLayout] = useState(() => localStorage.getItem('projects_layout') || 'cards')
  useEffect(() => { localStorage.setItem('projects_layout', layout) }, [layout])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = { page, pageSize: 15, sortDescending: true }
      if (search)  params.search    = search
      if (statusF) params.status    = statusF
      if (typeF)   params.type      = typeF

      const res  = await api.get('/api/v1/projects', { params })
      const data = res.data?.data
      setProjects(data?.items ?? [])
      setTotalPages(data?.totalPages ?? 1)
      setTotal(data?.totalCount ?? 0)
    } catch {
      setError('Failed to load projects.')
    } finally {
      setLoading(false)
    }
  }, [page, search, statusF, typeF])

  useEffect(() => { load() }, [load])

  const hasFilters = search || statusF || typeF

  function clearFilters() {
    setSearch(''); setStatusF(''); setTypeF(''); setPage(1)
  }

  async function handleExport(format) {
    setExporting(true)
    setExportMenu(false)
    try {
      const params = { page: 1, pageSize: 1000, sortDescending: true }
      if (search)  params.search    = search
      if (statusF) params.status    = statusF
      if (typeF)   params.type      = typeF

      const res  = await api.get('/api/v1/projects', { params })
      const rows = res.data?.data?.items ?? []
      const subtitle = `Total: ${rows.length} project${rows.length !== 1 ? 's' : ''}${hasFilters ? ' (filtered)' : ''} · Generated ${new Date().toLocaleString()}`

      if (format === 'pdf') {
        exportToPdf({ title: 'Projects Report', subtitle, columns: PROJECT_COLUMNS, rows, filename: 'lante-projects' })
      } else {
        exportToExcel({ title: 'Projects', subtitle, columns: PROJECT_COLUMNS, rows, filename: 'lante-projects', sheetName: 'Projects' })
      }
    } catch {
      // silently ignore export errors
    } finally {
      setExporting(false)
    }
  }

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8" onClick={() => setExportMenu(false)}>
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Projects</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${total} project${total !== 1 ? 's' : ''}`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {/* Export dropdown */}
            <div className="relative" onClick={e => e.stopPropagation()}>
              <button
                onClick={() => setExportMenu(v => !v)}
                disabled={exporting || loading}
                className="inline-flex items-center gap-2 px-4 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-lg transition-colors disabled:opacity-50"
              >
                {exporting ? (
                  <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
                  </svg>
                ) : (
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                  </svg>
                )}
                Export
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>
              {exportMenu && (
                <div className="absolute right-0 mt-1 w-40 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
                  <button onClick={() => handleExport('pdf')} className="flex items-center gap-2 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50">
                    <span className="text-red-500">PDF</span> Export as PDF
                  </button>
                  <button onClick={() => handleExport('excel')} className="flex items-center gap-2 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 border-t border-gray-100">
                    <span className="text-green-600">XLS</span> Export as Excel
                  </button>
                </div>
              )}
            </div>
            <button
              onClick={() => navigate('/modules/projects/new')}
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              New Project
            </button>
          </div>
        </div>

        {/* Filters */}
        <div className="bg-white rounded-xl border border-gray-200 p-4 mb-5">
          <form onSubmit={e => { e.preventDefault(); setPage(1); load() }} className="flex flex-wrap gap-3 items-end">
            <div className="flex-1 min-w-[180px]">
              <label className="block text-xs font-medium text-gray-500 mb-1">Search</label>
              <input
                type="text"
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="Project name or client…"
                className="input"
              />
            </div>
            <div className="w-40">
              <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
              <select value={statusF} onChange={e => { setStatusF(e.target.value); setPage(1) }} className="input">
                <option value="">All</option>
                {STATUSES.map(s => <option key={s} value={s}>{STATUS_LABELS[s] ?? s}</option>)}
              </select>
            </div>
            <div className="w-36">
              <label className="block text-xs font-medium text-gray-500 mb-1">Type</label>
              <select value={typeF} onChange={e => { setTypeF(e.target.value); setPage(1) }} className="input">
                <option value="">All</option>
                {TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <button type="submit" className="px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors">
              Search
            </button>
            {hasFilters && (
              <button type="button" onClick={clearFilters} className="px-4 py-2.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                Clear
              </button>
            )}
          </form>
        </div>

        {/* View toggle: cards vs list */}
        <div className="flex items-center justify-end mb-4">
          <div className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5">
            <button
              type="button"
              onClick={() => setLayout('cards')}
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${layout === 'cards' ? 'bg-navy text-white' : 'text-gray-500 hover:text-navy'}`}
              title="Card view"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="M4 5h6v6H4V5zM14 5h6v6h-6V5zM4 15h6v4H4v-4zM14 15h6v4h-6v-4z"/></svg>
              Cards
            </button>
            <button
              type="button"
              onClick={() => setLayout('list')}
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${layout === 'list' ? 'bg-navy text-white' : 'text-gray-500 hover:text-navy'}`}
              title="List view"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16"/></svg>
              List
            </button>
          </div>
        </div>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{error}</div>}

        {loading ? (
          <div className="space-y-3">
            {[1,2,3,4].map(i => <div key={i} className="h-20 bg-white rounded-xl border border-gray-100 animate-pulse" />)}
          </div>
        ) : projects.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="text-4xl mb-3">📁</div>
            <h3 className="font-semibold text-gray-700">No projects found</h3>
            <p className="text-sm text-gray-400 mt-1">{hasFilters ? 'Adjust your filters.' : 'Create your first project to get started.'}</p>
          </div>
        ) : layout === 'cards' ? (
          <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(330px, 1fr))' }}>
            {projects.map(p => {
              const value     = p.contractValue ?? p.plannedBudget ?? 0
              const expenses  = p.actualCost ?? 0
              const invoiced  = p.invoicedAmount ?? p.invoiced ?? 0
              const collected = p.collectedAmount ?? p.collected ?? 0
              const budget    = p.plannedBudget || p.contractValue || 0
              const ratio     = budget ? expenses / budget : 0
              const critical  = ratio >= 1
              const barColor  = ratio >= 1 ? 'bg-red-500' : ratio >= 0.8 ? 'bg-amber-500' : 'bg-green-500'
              const pctColor  = ratio >= 1 ? 'text-red-500' : ratio >= 0.8 ? 'text-amber-600' : 'text-green-600'
              return (
                <button
                  key={p.id}
                  onClick={() => navigate(`/modules/projects/${p.id}`)}
                  className="text-left bg-white rounded-xl border border-gray-200 hover:border-gold hover:shadow-md transition p-5"
                >
                  <div className="flex items-start justify-between gap-3 mb-4">
                    <div className="min-w-0">
                      <p className="font-bold text-navy leading-snug">{p.name}</p>
                      <p className="text-xs text-gray-500 mt-0.5">{p.clientName ?? '—'}</p>
                    </div>
                    <ProjectStatusBadge status={p.status} />
                  </div>
                  <div className="grid grid-cols-2 gap-2 mb-4">
                    {[['Value', value], ['Expenses', expenses], ['Invoiced', invoiced], ['Collected', collected]].map(([l, v]) => (
                      <div key={l} className="bg-offwhite rounded-lg px-3 py-2.5">
                        <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">{l}</p>
                        <p className="text-sm font-bold text-navy">{fmt(v)}</p>
                      </div>
                    ))}
                  </div>
                  <div className="h-1.5 bg-lgrey rounded-full overflow-hidden mb-1.5">
                    <div className={`h-full ${barColor}`} style={{ width: `${Math.min(ratio * 100, 100)}%` }} />
                  </div>
                  <div className="flex items-center justify-between text-xs">
                    <span className="text-gray-500">Budget Used</span>
                    <span className={`font-semibold ${pctColor}`}>{(ratio * 100).toFixed(1)}%</span>
                  </div>
                  {critical && (
                    <div className="mt-3 bg-red-50 text-red-600 rounded-lg px-3 py-2 text-xs font-semibold">🔴 Budget Critical — MD Approval Required</div>
                  )}
                </button>
              )
            })}
          </div>
        ) : (
          /* List view — CRM-style data table (navy header) */
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
            <DataTable
              headers={['Project', 'Client', 'Status', 'Value', 'Expenses', 'Budget Used', 'Actions']}
              onRowClick={(_, i) => projects[i] && navigate(`/modules/projects/${projects[i].id}`)}
              rows={projects.map(p => {
                const value    = p.contractValue ?? p.plannedBudget ?? 0
                const expenses = p.actualCost ?? 0
                const budget   = p.plannedBudget || p.contractValue || 0
                const ratio    = budget ? expenses / budget : 0
                const pctColor = ratio >= 1 ? T.red : ratio >= 0.8 ? '#d97706' : '#16a34a'
                return [
                  <span style={{ fontWeight: 600, color: T.navy }}>{p.name}</span>,
                  p.clientName ?? '—',
                  <ProjectStatusBadge status={p.status} />,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmt(value)}</span>,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmt(expenses)}</span>,
                  <span style={{ color: pctColor, fontWeight: 700, whiteSpace: 'nowrap' }}>{(ratio * 100).toFixed(1)}%</span>,
                  <Btn size="sm" variant="outline" onClick={(e) => { e.stopPropagation(); navigate(`/modules/projects/${p.id}`) }}>View</Btn>,
                ]
              })}
            />
          </div>
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-5">
            <p className="text-sm text-gray-500">Page {page} of {totalPages}</p>
            <div className="flex gap-2">
              <button disabled={page === 1} onClick={() => setPage(p => p - 1)}
                className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors">Previous</button>
              <button disabled={page === totalPages} onClick={() => setPage(p => p + 1)}
                className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors">Next</button>
            </div>
          </div>
        )}
      </main>
    </>
  )
}

function fmt(n) {
  return new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
}
