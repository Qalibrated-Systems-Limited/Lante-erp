import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { listServiceRequests } from '../../services/operations.js'
import { useAuth } from '../../context/AuthContext.jsx'

const STATUSES = [
  'PendingVerification','Submitted','UnderReview','QuotationDraft',
  'QuotationSent','QuotationApproved','QuotationRejected',
  'InProgress','Completed','Rejected','Cancelled',
]

const FORM_TYPES = ['SRF','CRF_NAWI','CRF_MASS']

const STATUS_BADGE = {
  PendingVerification : 'bg-gray-100 text-gray-500',
  Submitted           : 'bg-blue-100 text-blue-700',
  UnderReview         : 'bg-amber-100 text-amber-700',
  QuotationDraft      : 'bg-purple-100 text-purple-700',
  QuotationSent       : 'bg-indigo-100 text-indigo-700',
  QuotationApproved   : 'bg-green-100 text-green-700',
  QuotationRejected   : 'bg-red-100 text-red-600',
  InProgress          : 'bg-cyan-100 text-cyan-700',
  Completed           : 'bg-green-100 text-green-800',
  Rejected            : 'bg-red-100 text-red-700',
  Cancelled           : 'bg-gray-100 text-gray-500',
}

const FORM_BADGE = {
  SRF      : 'bg-blue-100 text-blue-700',
  CRF_NAWI : 'bg-amber-100 text-amber-700',
  CRF_MASS : 'bg-green-100 text-green-700',
}

const FORM_LABEL = {
  SRF      : 'SRF',
  CRF_NAWI : 'CRF-NAWI',
  CRF_MASS : 'CRF-MASS',
}

export default function ServiceRequestQueuePage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canCreate = hasPermission?.('operations.write')
  const [newMenu, setNewMenu] = useState(false)

  const [requests, setRequests]     = useState([])
  const [total, setTotal]           = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')

  const [statusFilter, setStatusFilter]   = useState('')
  const [typeFilter, setTypeFilter]       = useState('')
  const [from, setFrom]                   = useState('')
  const [to, setTo]                       = useState('')
  const [page, setPage]                   = useState(1)
  const PAGE_SIZE = 20

  const fetchRequests = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const params = { page, pageSize: PAGE_SIZE }
      if (statusFilter) params.status   = statusFilter
      if (typeFilter)   params.formType = typeFilter
      if (from)         params.from     = from
      if (to)           params.to       = to
      const res = await listServiceRequests(params)
      setRequests(res?.data ?? [])
      setTotal(res?.total ?? 0)
      setTotalPages(res?.pages ?? 1)
    } catch {
      setError('Failed to load service requests.')
    } finally {
      setLoading(false)
    }
  }, [page, statusFilter, typeFilter, from, to])

  useEffect(() => { fetchRequests() }, [fetchRequests])

  const clearFilters = () => {
    setStatusFilter(''); setTypeFilter(''); setFrom(''); setTo(''); setPage(1)
  }

  const hasFilters = statusFilter || typeFilter || from || to

  return (
    <>
      <div className="p-6 max-w-7xl mx-auto" onClick={() => setNewMenu(false)}>
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
          <div>
            <h1 className="text-xl font-extrabold text-zinc-950">Service Requests</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Review and action SRF / CRF-NAWI / CRF-MASS submissions
            </p>
          </div>
          <div className="flex items-center gap-4">
            {total > 0 && <span className="text-sm text-gray-500">{total} request{total !== 1 ? 's' : ''}</span>}
            {canCreate && (
              <div className="relative" onClick={e => e.stopPropagation()}>
                <button onClick={() => setNewMenu(v => !v)}
                  className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors">
                  + New Request
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" /></svg>
                </button>
                {newMenu && (
                  <div className="absolute right-0 mt-1 w-60 bg-white border border-gray-200 rounded-lg shadow-lg z-10 overflow-hidden">
                    {[
                      ['SRF', '🛠️ Service Request', 'General field / on-site service'],
                      ['CRF_NAWI', '⚖️ NAWI Calibration', 'Non-automatic weighing instruments'],
                      ['CRF_MASS', '🧱 Mass Calibration', 'Weights & mass standards'],
                    ].map(([type, title, sub]) => (
                      <button key={type} onClick={() => navigate(`/modules/operations/service-requests/new?type=${type}`)}
                        className="block w-full text-left px-4 py-2.5 hover:bg-gray-50 border-b border-gray-100 last:border-0">
                        <p className="text-sm font-semibold text-navy">{title}</p>
                        <p className="text-xs text-gray-400">{sub}</p>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Filters */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 mb-5 flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-semibold text-gray-500 mb-1">Status</label>
            <select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-400">
              <option value="">All statuses</option>
              {STATUSES.map(s => <option key={s} value={s}>{s.replace(/([A-Z])/g, ' $1').trim()}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-semibold text-gray-500 mb-1">Form Type</label>
            <select value={typeFilter} onChange={e => { setTypeFilter(e.target.value); setPage(1) }}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-400">
              <option value="">All types</option>
              {FORM_TYPES.map(t => <option key={t} value={t}>{FORM_LABEL[t]}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-semibold text-gray-500 mb-1">From</label>
            <input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1) }}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
          </div>
          <div>
            <label className="block text-xs font-semibold text-gray-500 mb-1">To</label>
            <input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1) }}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
          </div>
          {hasFilters && (
            <button onClick={clearFilters}
              className="px-3 py-1.5 text-sm text-red-600 hover:text-red-800 font-medium">
              Clear filters
            </button>
          )}
        </div>

        {/* Table */}
        {error && <p className="text-sm text-red-600 mb-4">{error}</p>}

        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          {loading ? (
            <div className="py-16 text-center text-sm text-gray-400">Loading…</div>
          ) : requests.length === 0 ? (
            <div className="py-16 text-center">
              <p className="text-gray-400 text-sm">No service requests found.</p>
              {hasFilters && <button onClick={clearFilters} className="mt-2 text-sm text-amber-600 hover:underline">Clear filters</button>}
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Reference</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Type</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Client</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide hidden sm:table-cell">Organisation</th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide hidden md:table-cell">Instruments</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide hidden lg:table-cell">Date</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {requests.map(r => (
                  <tr key={r.referenceNumber}
                    className="hover:bg-gray-50 transition-colors cursor-pointer"
                    onClick={() => navigate(`/modules/operations/service-requests/${r.referenceNumber}`)}>
                    <td className="px-4 py-3 font-mono font-semibold text-zinc-800 text-xs">{r.referenceNumber}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-block text-xs font-semibold px-2 py-0.5 rounded-full ${FORM_BADGE[r.formType] ?? 'bg-gray-100 text-gray-600'}`}>
                        {FORM_LABEL[r.formType] ?? r.formType}
                      </span>
                      {r.serviceLocation && (
                        <span className={`ml-1 inline-block text-xs font-medium px-1.5 py-0.5 rounded-full ${
                          r.serviceLocation === 'InLab' ? 'bg-purple-50 text-purple-600' : 'bg-cyan-50 text-cyan-600'
                        }`}>
                          {r.serviceLocation === 'InLab' ? '🔬' : '🚗'}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <p className="font-medium text-gray-900">{r.clientName}</p>
                      <p className="text-xs text-gray-400">{r.clientEmail}</p>
                    </td>
                    <td className="px-4 py-3 text-gray-600 hidden sm:table-cell">{r.clientOrganization || '—'}</td>
                    <td className="px-4 py-3 text-center text-gray-600 hidden md:table-cell">{r.instrumentCount}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-block text-xs font-semibold px-2 py-0.5 rounded-full ${STATUS_BADGE[r.status] ?? 'bg-gray-100 text-gray-500'}`}>
                        {r.status.replace(/([A-Z])/g, ' $1').trim()}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-400 text-xs hidden lg:table-cell">
                      {new Date(r.createdAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <span className="text-xs text-amber-600 font-semibold">Review →</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-4 text-sm text-gray-500">
            <span>Page {page} of {totalPages}</span>
            <div className="flex gap-2">
              <button disabled={page === 1} onClick={() => setPage(p => p - 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors">
                ← Prev
              </button>
              <button disabled={page === totalPages} onClick={() => setPage(p => p + 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors">
                Next →
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
