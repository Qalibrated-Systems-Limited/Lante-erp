import { useState, useEffect, useCallback } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import TicketStatusBadge from '../../components/ticketing/TicketStatusBadge.jsx'
import TicketPriorityBadge from '../../components/ticketing/TicketPriorityBadge.jsx'
import { CustomerFeedback } from '../../components/ticketing/list/CustomerFeedback.jsx'
import { DataTable, Badge, Btn } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'
import { listTickets, getCategories, getUsers, getCsat, getCsOverview } from '../../services/ticketing.js'
import { exportToPdf, exportToExcel, TICKET_COLUMNS } from '../../utils/export.js'
import { useAuth } from '../../context/AuthContext.jsx'

const STATUSES = ['New','Assigned','InProgress','Pending','Escalated','Resolved','Closed','Reopened']
const PRIORITIES = ['Low','Medium','High','Critical']
// Map ticket status / priority to a Badge variant for the list (table) view.
const STATUS_VARIANT = {
  New: 'blue', Assigned: 'blue', InProgress: 'amber', Pending: 'amber',
  Escalated: 'red', Resolved: 'green', Closed: 'default', Reopened: 'amber',
}
const PRIORITY_VARIANT = { Critical: 'red', High: 'amber', Medium: 'default', Low: 'default' }

export default function TicketingPage() {
  const navigate = useNavigate()
  const { user, ticketScope, hasPermission } = useAuth()

  const [tickets, setTickets] = useState([])
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [priorityFilter, setPriorityFilter] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal] = useState(0)
  const [exporting, setExporting] = useState(false)
  const [exportMenu, setExportMenu] = useState(false)
  const [userMap, setUserMap] = useState({})

  // Customer Feedback section
  const [searchParams] = useSearchParams()
  const [view, setView] = useState(searchParams.get('tab') === 'feedback' ? 'feedback' : 'tickets')   // 'tickets' | 'feedback'
  // Card vs list layout for the tickets view — remembered across visits.
  const [layout, setLayout] = useState(() => localStorage.getItem('helpdesk_ticket_layout') || 'cards')
  useEffect(() => { localStorage.setItem('helpdesk_ticket_layout', layout) }, [layout])
  // Honour ?tab= deep-links from the Helpdesk nav (e.g. Customer Feedback).
  useEffect(() => {
    const t = searchParams.get('tab')
    if (t === 'feedback' || t === 'tickets') setView(t)
  }, [searchParams])
  const [feedback, setFeedback] = useState(null)
  const [feedbackLoading, setFeedbackLoading] = useState(false)

  const loadFeedback = useCallback(async () => {
    setFeedbackLoading(true)
    try {
      // #14 + D6-3: CSAT aggregate + the CS dashboard overview (open-by-category, SLA, resolution…).
      const [d, ov] = await Promise.all([
        getCsat().then(x => x ?? {}),
        getCsOverview().catch(() => null),
      ])
      const dist = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, ...(d.distribution ?? {}) }
      const rated = (d.recentComments ?? []).map(c => ({
        ticketId: c.ticketId,
        title: c.title,
        rating: c.rating,
        comment: c.comment,
        submittedAt: c.submittedAt,
      }))
      setFeedback({
        rated,
        total: d.totalResponses ?? 0,
        dist,
        avg: d.averageRating ?? 0,
        goodPct: Math.round(d.csatPercent ?? 0),
        responseRate: Math.round(d.responseRatePct ?? 0),
        surveysSent: d.surveysSent ?? 0,
        overview: ov,
      })
    } catch {
      setFeedback({ rated: [], total: 0, dist: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 }, avg: 0, goodPct: 0 })
    } finally {
      setFeedbackLoading(false)
    }
  }, [])

  useEffect(() => { if (view === 'feedback' && !feedback) loadFeedback() }, [view, feedback, loadFeedback])

  useEffect(() => {
    getCategories()
      .then(list => setCategories(list ?? []))
      .catch(() => {})
    getUsers().then(raw => {
      const list = Array.isArray(raw) ? raw : raw?.items ?? []
      const map = {}
      list.forEach(u => { map[u.id] = `${u.firstName} ${u.lastName}`.trim() })
      setUserMap(map)
    }).catch(() => {})
  }, [])

  const fetchTickets = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = { page, pageSize: 15, sortDescending: true }
      if (search)         params.search = search
      if (statusFilter)   params.status = STATUSES.indexOf(statusFilter)
      if (priorityFilter) params.priority = PRIORITIES.indexOf(priorityFilter)
      if (categoryFilter) params.categoryId = categoryFilter

      const data = await listTickets(params)
      setTickets(data?.items ?? [])
      setTotalPages(data?.totalPages ?? 1)
      setTotal(data?.totalCount ?? 0)
    } catch {
      setError('Failed to load tickets.')
    } finally {
      setLoading(false)
    }
  }, [page, search, statusFilter, priorityFilter, categoryFilter])

  useEffect(() => {
    fetchTickets()
  }, [fetchTickets])

  function handleSearchSubmit(e) {
    e.preventDefault()
    setPage(1)
    fetchTickets()
  }

  function clearFilters() {
    setSearch('')
    setStatusFilter('')
    setPriorityFilter('')
    setCategoryFilter('')
    setPage(1)
  }

  const hasFilters = search || statusFilter || priorityFilter || categoryFilter

  async function handleExport(format) {
    setExporting(true)
    setExportMenu(false)
    try {
      const params = { page: 1, pageSize: 1000, sortDescending: true }
      if (search)         params.search     = search
      if (statusFilter)   params.status     = STATUSES.indexOf(statusFilter)
      if (priorityFilter) params.priority   = PRIORITIES.indexOf(priorityFilter)
      if (categoryFilter) params.categoryId = categoryFilter

      const data = await listTickets(params)
      const rows = data?.items ?? []
      const subtitle = `Total: ${rows.length} ticket${rows.length !== 1 ? 's' : ''}${hasFilters ? ' (filtered)' : ''} · Generated ${new Date().toLocaleString()}`

      if (format === 'pdf') {
        exportToPdf({ title: 'Tickets Report', subtitle, columns: TICKET_COLUMNS, rows, filename: 'lante-tickets' })
      } else {
        exportToExcel({ title: 'Tickets', subtitle, columns: TICKET_COLUMNS, rows, filename: 'lante-tickets', sheetName: 'Tickets' })
      }
    } catch {
      // silently ignore
    } finally {
      setExporting(false)
    }
  }

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8" onClick={() => setExportMenu(false)}>
        {/* Scope banner */}
        {ticketScope !== 'all' && (
          <div className="flex items-center gap-2 mb-4 px-4 py-2.5 bg-amber-50 border border-amber-200 rounded-lg text-sm text-amber-800">
            <svg className="w-4 h-4 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M12 2a10 10 0 100 20A10 10 0 0012 2z" />
            </svg>
            {ticketScope === 'dept'
              ? <>Showing tickets for <strong className="mx-1">{user?.departmentName ?? 'your department'}</strong> only.</>
              : <>Showing tickets <strong className="mx-1">assigned to or created by you</strong> only.</>}
          </div>
        )}

        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Tickets</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${total} ticket${total !== 1 ? 's' : ''} found`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {hasPermission('reports.export') && (
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
                  <button onClick={() => handleExport('pdf')} className="flex items-center gap-2 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 rounded-t-lg">
                    <span className="text-red-500 font-bold text-xs">PDF</span> Export as PDF
                  </button>
                  <button onClick={() => handleExport('excel')} className="flex items-center gap-2 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 border-t border-gray-100 rounded-b-lg">
                    <span className="text-green-600 font-bold text-xs">XLS</span> Export as Excel
                  </button>
                </div>
              )}
            </div>
            )}
            {hasPermission('tickets.write') && (
            <button
              onClick={() => navigate('/modules/ticketing/new')}
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              New Ticket
            </button>
            )}
          </div>
        </div>

        {/* View tabs */}
        <div className="flex gap-1 border-b border-gray-200 mb-5">
          {[['tickets', 'Tickets'], ['feedback', 'Customer Feedback']].map(([id, label]) => (
            <button
              key={id}
              onClick={() => setView(id)}
              className={`px-4 py-2.5 text-sm font-bold border-b-2 -mb-px transition-colors ${view === id ? 'border-gold text-navy' : 'border-transparent text-gray-500 hover:text-navy'}`}
            >
              {label}
            </button>
          ))}
        </div>

        {view === 'tickets' && (<>
        {/* Filters */}
        <div className="bg-white rounded-xl border border-gray-200 p-4 mb-5">
          <form onSubmit={handleSearchSubmit} className="flex flex-wrap gap-3 items-end">
            <div className="flex-1 min-w-[200px]">
              <label className="block text-xs font-medium text-gray-500 mb-1">Search</label>
              <input
                type="text"
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="Search title or description…"
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold focus:border-transparent"
              />
            </div>

            <div className="w-36">
              <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
              <select
                value={statusFilter}
                onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">All</option>
                {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>

            <div className="w-32">
              <label className="block text-xs font-medium text-gray-500 mb-1">Priority</label>
              <select
                value={priorityFilter}
                onChange={e => { setPriorityFilter(e.target.value); setPage(1) }}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">All</option>
                {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>

            <div className="w-44">
              <label className="block text-xs font-medium text-gray-500 mb-1">Category</label>
              <select
                value={categoryFilter}
                onChange={e => { setCategoryFilter(e.target.value); setPage(1) }}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">All</option>
                {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>

            <button
              type="submit"
              className="px-4 py-2 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors"
            >
              Search
            </button>

            {hasFilters && (
              <button
                type="button"
                onClick={clearFilters}
                className="px-4 py-2 text-gray-500 hover:text-gray-700 text-sm font-medium rounded-lg border border-gray-200 hover:bg-gray-50 transition-colors"
              >
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

        {/* Table */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">
            {error}
          </div>
        )}

        {loading ? (
          <div className="space-y-3">
            {[1,2,3,4,5].map(i => (
              <div key={i} className="h-16 bg-white rounded-xl border border-gray-100 animate-pulse" />
            ))}
          </div>
        ) : tickets.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="text-4xl mb-3">🎫</div>
            <h3 className="font-semibold text-gray-700">No tickets found</h3>
            <p className="text-sm text-gray-400 mt-1">
              {hasFilters ? 'Try adjusting your filters.' : 'Create your first ticket to get started.'}
            </p>
          </div>
        ) : layout === 'cards' ? (
          <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(330px, 1fr))' }}>
            {tickets.map(ticket => {
              const assignee = userMap[ticket.assignedToUserId] ?? ticket.assigneeName ?? null
              const due = ticket.resolutionDueAt
              const overdue = due && new Date(due) < new Date(new Date().toDateString()) &&
                !['Resolved', 'Closed'].includes(ticket.statusLabel)
              return (
                <button
                  key={ticket.id}
                  onClick={() => navigate(`/modules/ticketing/${ticket.id}`)}
                  className="text-left bg-white rounded-xl border border-gray-200 hover:border-gold hover:shadow-md transition p-5"
                >
                  <div className="flex items-start justify-between gap-3 mb-3">
                    <div className="min-w-0">
                      <p className="font-bold text-navy leading-snug truncate">{ticket.title}</p>
                      <p className="text-xs text-gray-500 mt-0.5 truncate">
                        {ticket.reference && <span className="font-mono text-gray-400">{ticket.reference} · </span>}
                        {ticket.categoryName ?? 'Uncategorised'}
                        {ticket.customerName && <span className="text-gray-400"> · {ticket.customerName}</span>}
                      </p>
                    </div>
                    <TicketStatusBadge status={ticket.statusLabel} />
                  </div>
                  <div className="flex items-center gap-2 mb-4 flex-wrap">
                    <TicketPriorityBadge priority={ticket.priorityLabel} />
                    {ticket.isEscalated && (
                      <span className="text-xs font-semibold text-red-600 bg-red-50 border border-red-200 px-2 py-0.5 rounded">⚠ Escalated</span>
                    )}
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <div className="bg-offwhite rounded-lg px-3 py-2.5">
                      <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">Assignee</p>
                      <p className="text-sm font-semibold text-navy truncate">{assignee ?? <span className="text-gray-400 font-normal">Unassigned</span>}</p>
                    </div>
                    <div className="bg-offwhite rounded-lg px-3 py-2.5">
                      <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">Due</p>
                      <p className={`text-sm font-semibold ${overdue ? 'text-red-600' : 'text-navy'}`}>
                        {due ? new Date(due).toLocaleDateString() : '—'}{overdue ? ' · Overdue' : ''}
                      </p>
                    </div>
                  </div>
                </button>
              )
            })}
          </div>
        ) : (
          /* List view — CRM-style data table (navy header) */
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
            <DataTable
              headers={['Ticket', 'Category', 'Client', 'Assignee', 'Status', 'Priority', 'Due', 'Actions']}
              onRowClick={(_, i) => tickets[i] && navigate(`/modules/ticketing/${tickets[i].id}`)}
              rows={tickets.map(ticket => {
                const assignee = userMap[ticket.assignedToUserId] ?? ticket.assigneeName ?? null
                const due = ticket.resolutionDueAt
                const overdue = due && new Date(due) < new Date(new Date().toDateString()) &&
                  !['Resolved', 'Closed'].includes(ticket.statusLabel)
                return [
                  <div style={{ minWidth: 0 }}>
                    <span style={{ fontWeight: 600, color: T.navy }}>{ticket.title}</span>
                    {ticket.isEscalated && <span style={{ color: T.red, marginLeft: 4 }} title="Escalated">⚠</span>}
                    {ticket.reference && <div style={{ fontSize: 11, color: T.mgrey, fontFamily: 'monospace' }}>{ticket.reference}</div>}
                  </div>,
                  ticket.categoryName ?? '—',
                  ticket.customerName ?? '—',
                  assignee ?? <span style={{ color: T.mgrey }}>Unassigned</span>,
                  <Badge variant={STATUS_VARIANT[ticket.statusLabel] ?? 'default'}>{ticket.statusLabel === 'InProgress' ? 'In Progress' : ticket.statusLabel}</Badge>,
                  <Badge variant={PRIORITY_VARIANT[ticket.priorityLabel] ?? 'default'}>{ticket.priorityLabel}</Badge>,
                  <span style={{ color: overdue ? T.red : T.dgrey, fontWeight: overdue ? 700 : 400, whiteSpace: 'nowrap' }}>
                    {due ? new Date(due).toLocaleDateString() : '—'}
                  </span>,
                  <Btn size="sm" variant="outline" onClick={(e) => { e.stopPropagation(); navigate(`/modules/ticketing/${ticket.id}`) }}>View</Btn>,
                ]
              })}
            />
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
                disabled={page === totalPages}
                onClick={() => setPage(p => p + 1)}
                className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors"
              >
                Next
              </button>
            </div>
          </div>
        )}
        </>)}

        {view === 'feedback' && (
          <CustomerFeedback data={feedback} loading={feedbackLoading} onExport={handleExport} exporting={exporting} canExport={hasPermission('reports.export')} />
        )}
      </main>
    </>
  )
}
