import { useNavigate } from 'react-router-dom'
import { useState, useEffect } from 'react'
import { DataTable, Badge, Btn } from '../ui.jsx'
import { T } from '../../theme/tokens.js'
import { useCustomers, TIERS, CUSTOMER_STATUSES, STATUS_VARIANT, statusLabel } from '../../hooks/crm/useCustomers.js'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)

// Clients / Customers tab of the Commercial page — the customer master (C1).
export default function CustomersTab() {
  const navigate = useNavigate()
  const {
    customers, total, totalPages, loading, error, canWrite,
    search, setSearch, statusF, setStatusF, activityF, setActivityF, tierF, setTierF, page, setPage,
  } = useCustomers()

  const [layout, setLayout] = useState(() => localStorage.getItem('crm_customers_layout') || 'cards')
  useEffect(() => { localStorage.setItem('crm_customers_layout', layout) }, [layout])

  const hasFilters = search || statusF || activityF || tierF

  return (
    <div>
      {/* Toolbar */}
      <div className="flex items-center justify-between gap-3 flex-wrap mb-4">
        <p className="text-sm text-gray-500">{loading ? 'Loading…' : `${total} client${total !== 1 ? 's' : ''}`}</p>
        {canWrite && (
          <button onClick={() => navigate('/modules/crm/customers/new')}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" /></svg>
            New Customer
          </button>
        )}
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border border-gray-200 p-4 mb-4">
        <form onSubmit={e => { e.preventDefault(); setPage(1) }} className="flex flex-wrap gap-3 items-end">
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-medium text-gray-500 mb-1">Search</label>
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Name, email or reference…" className="input" />
          </div>
          <div className="w-48">
            <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
            {/* Engagement, not approval: a client can be approved (Active) yet dormant — the C7
                sweep stamps DormantSince after 90 days with no interaction. */}
            <select value={activityF} onChange={e => { setActivityF(e.target.value); setPage(1) }} className="input">
              <option value="">All clients</option>
              <option value="active">Active (engaged)</option>
              <option value="dormant">Dormant (90+ days quiet)</option>
            </select>
            <select value={statusF} onChange={e => { setStatusF(e.target.value); setPage(1) }} className="input">
              <option value="">All</option>
              {CUSTOMER_STATUSES.map(s => <option key={s} value={s}>{statusLabel(s)}</option>)}
            </select>
          </div>
          <div className="w-36">
            <label className="block text-xs font-medium text-gray-500 mb-1">Tier</label>
            <select value={tierF} onChange={e => { setTierF(e.target.value); setPage(1) }} className="input">
              <option value="">All</option>
              {TIERS.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <button type="submit" className="px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-semibold rounded-lg">Search</button>
        </form>
      </div>

      {/* View toggle */}
      <div className="flex items-center justify-end mb-4">
        <div className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5">
          <button type="button" onClick={() => setLayout('cards')}
            className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${layout === 'cards' ? 'bg-navy text-white' : 'text-gray-500 hover:text-navy'}`}>
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="M4 5h6v6H4V5zM14 5h6v6h-6V5zM4 15h6v4H4v-4zM14 15h6v4h-6v-4z"/></svg>
            Cards
          </button>
          <button type="button" onClick={() => setLayout('list')}
            className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${layout === 'list' ? 'bg-navy text-white' : 'text-gray-500 hover:text-navy'}`}>
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16"/></svg>
            List
          </button>
        </div>
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{error}</div>}

      {loading ? (
        <div className="space-y-3">{[1,2,3,4].map(i => <div key={i} className="h-20 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
      ) : customers.length === 0 ? (
        <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
          <div className="text-4xl mb-3">🤝</div>
          <h3 className="font-semibold text-gray-700">No customers found</h3>
          <p className="text-sm text-gray-400 mt-1">{hasFilters ? 'Adjust your filters.' : 'Onboard your first client to get started.'}</p>
        </div>
      ) : layout === 'cards' ? (
        <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))' }}>
          {customers.map(c => (
            <button key={c.id} onClick={() => navigate(`/modules/crm/customers/${c.id}`)}
              className="text-left bg-white rounded-xl border border-gray-200 hover:border-gold hover:shadow-md transition p-5">
              <div className="flex items-start justify-between gap-3 mb-3">
                <div className="min-w-0">
                  <p className="font-bold text-navy leading-snug truncate">{c.name}</p>
                  <p className="text-xs text-gray-500 mt-0.5 truncate">{c.industry ?? '—'}{c.email ? ` · ${c.email}` : ''}</p>
                </div>
                <Badge variant={STATUS_VARIANT[c.status] ?? 'default'}>{statusLabel(c.status)}</Badge>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="bg-offwhite rounded-lg px-3 py-2.5">
                  <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">Tier</p>
                  <p className="text-sm font-bold text-navy">{c.accountTier}</p>
                </div>
                <div className="bg-offwhite rounded-lg px-3 py-2.5">
                  <p className="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">Credit limit</p>
                  <p className="text-sm font-bold text-navy">{fmtKes(c.creditLimit)}</p>
                </div>
              </div>
            </button>
          ))}
        </div>
      ) : (
        <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
          <DataTable
            headers={['Customer', 'Industry', 'Tier', 'Status', 'Credit Limit', 'Actions']}
            onRowClick={(_, i) => customers[i] && navigate(`/modules/crm/customers/${customers[i].id}`)}
            rows={customers.map(c => [
              <span style={{ fontWeight: 600, color: T.navy }}>{c.name}</span>,
              c.industry ?? '—',
              c.accountTier,
              <Badge variant={STATUS_VARIANT[c.status] ?? 'default'}>{statusLabel(c.status)}</Badge>,
              <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.creditLimit)}</span>,
              <Btn size="sm" variant="outline" onClick={(e) => { e.stopPropagation(); navigate(`/modules/crm/customers/${c.id}`) }}>View</Btn>,
            ])}
          />
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-5">
          <p className="text-sm text-gray-500">Page {page} of {totalPages}</p>
          <div className="flex gap-2">
            <button disabled={page === 1} onClick={() => setPage(p => p - 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Previous</button>
            <button disabled={page === totalPages} onClick={() => setPage(p => p + 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Next</button>
          </div>
        </div>
      )}
    </div>
  )
}
