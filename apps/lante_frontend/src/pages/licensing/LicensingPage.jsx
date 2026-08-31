import { useState, useEffect, useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { licensingApi } from '../../api/licensingApi.js'
import IssueLicenseModal from './IssueLicenseModal.jsx'
import LicenseDetailModal from './LicenseDetailModal.jsx'
import RenewLicenseModal from './RenewLicenseModal.jsx'
import { featureLabel } from '../../utils/featureCatalogue.js'

export default function LicensingPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const [licenses, setLicenses] = useState([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState('')
  const [total, setTotal]       = useState(0)

  const [filterApp, setFilterApp]       = useState('')
  const [filterStatus, setFilterStatus] = useState('')

  const [modal, setModal]       = useState(null)
  const [selected, setSelected] = useState(null)

  const [expiring, setExpiring] = useState([])

  // Metadata loaded from backend — single source of truth for app IDs + features
  const [appIds, setAppIds]     = useState([])
  const [features, setFeatures] = useState([])

  // Auto-open issue modal when ?issue=1 is in URL (from dashboard button)
  useEffect(() => {
    if (searchParams.get('issue') === '1') {
      setModal('issue')
      setSearchParams({})
    }
  }, [])

  useEffect(() => {
    licensingApi.getMetadata()
      .then(res => {
        const meta = res.data?.data ?? {}
        setAppIds(meta.appIds ?? [])
        // Flatten grouped features into { value, label, group } objects
        const flat = (meta.features ?? []).flatMap(g =>
          (g.items ?? []).map(f => ({ value: f.value, label: f.label, group: g.group }))
        )
        setFeatures(flat)
      })
      .catch(() => {})

    licensingApi.getExpiring(30)
      .then(res => setExpiring(res.data?.data ?? []))
      .catch(() => {})
  }, [])

  const fetchLicenses = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = {}
      if (filterApp)                     params.appId  = filterApp
      if (filterStatus === 'active')     params.active = true
      if (filterStatus === 'inactive')   params.active = false

      const res = await licensingApi.getAll(params)
      const data = res.data?.data ?? []
      setLicenses(Array.isArray(data) ? data : data.items ?? [])
      setTotal(Array.isArray(data) ? data.length : data.totalCount ?? 0)
    } catch {
      setError('Failed to load licenses.')
    } finally {
      setLoading(false)
    }
  }, [filterApp, filterStatus])

  useEffect(() => { fetchLicenses() }, [fetchLicenses])

  function clearFilters() {
    setFilterApp('')
    setFilterStatus('')
  }

  const hasFilters = filterApp || filterStatus

  async function handleRevoke(license) {
    const reason = window.prompt(`Revoke reason for "${license.customerName || license.customerId}" / ${license.appId}:`)
    if (!reason?.trim()) return
    try {
      await licensingApi.revoke(license.id, reason.trim())
      setModal(null)
      setSelected(null)
      fetchLicenses()
    } catch {
      alert('Failed to revoke license.')
    }
  }

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">

        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">All Licenses</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${total} license${total !== 1 ? 's' : ''} found`}
            </p>
          </div>
          <button
            onClick={() => setModal('issue')}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Issue License
          </button>
        </div>

        {/* Expiring banner */}
        {expiring.length > 0 && (
          <div className="mb-5 flex items-center gap-3 bg-orange-50 border border-orange-200 rounded-xl px-5 py-3.5 text-sm text-orange-800">
            <svg className="w-5 h-5 text-orange-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <span>
              <strong>{expiring.length} license{expiring.length > 1 ? 's' : ''}</strong> expiring within 30 days.
            </span>
          </div>
        )}

        {/* Filters */}
        <div className="bg-white rounded-xl border border-gray-200 p-4 mb-5">
          <div className="flex flex-wrap gap-3 items-end">
            <div className="w-52">
              <label className="block text-xs font-medium text-gray-500 mb-1">Application</label>
              <select
                value={filterApp}
                onChange={e => setFilterApp(e.target.value)}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                <option value="">All Apps</option>
                {appIds.map(id => <option key={id} value={id}>{id}</option>)}
              </select>
            </div>

            <div className="w-40">
              <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
              <select
                value={filterStatus}
                onChange={e => setFilterStatus(e.target.value)}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                <option value="">All</option>
                <option value="active">Active only</option>
                <option value="inactive">Inactive only</option>
              </select>
            </div>

            {hasFilters && (
              <button
                onClick={clearFilters}
                className="px-4 py-2 text-gray-500 hover:text-gray-700 text-sm font-medium rounded-lg border border-gray-200 hover:bg-gray-50 transition-colors"
              >
                Clear
              </button>
            )}
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">
            {error}
          </div>
        )}

        {/* Table */}
        {loading ? (
          <div className="space-y-3">
            {[1,2,3,4,5].map(i => (
              <div key={i} className="h-16 bg-white rounded-xl border border-gray-100 animate-pulse" />
            ))}
          </div>
        ) : licenses.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="text-4xl mb-3">🔑</div>
            <h3 className="font-semibold text-gray-700">No licenses found</h3>
            <p className="text-sm text-gray-400 mt-1">
              {hasFilters ? 'Try adjusting your filters.' : 'Issue your first license to get started.'}
            </p>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-5 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Customer</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden md:table-cell">App</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden sm:table-cell">Features</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Expires</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider hidden lg:table-cell">Last Seen</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 text-xs uppercase tracking-wider">Status</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {licenses.map(lic => (
                  <tr
                    key={lic.id}
                    onClick={() => { setSelected(lic); setModal('detail') }}
                    className="hover:bg-amber-50 cursor-pointer transition-colors"
                  >
                    <td className="px-5 py-4">
                      <div className="font-medium text-gray-900">{lic.customerName || lic.customerId}</div>
                      <div className="text-xs text-gray-400">{lic.customerId}</div>
                    </td>
                    <td className="px-4 py-4 hidden md:table-cell">
                      <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded">{lic.appId}</span>
                    </td>
                    <td className="px-4 py-4 hidden sm:table-cell">
                      <div className="flex flex-wrap gap-1">
                        {(lic.features || []).map(f => (
                          <span key={f} className="text-xs bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded">{featureLabel(f)}</span>
                        ))}
                      </div>
                    </td>
                    <td className="px-4 py-4 hidden lg:table-cell">
                      <div className="text-gray-600">{new Date(lic.expiresAt).toLocaleDateString()}</div>
                      {!lic.revoked && !lic.isExpired && lic.daysUntilExpiry <= 30 && (
                        <div className="text-xs text-orange-600">{lic.daysUntilExpiry}d left</div>
                      )}
                    </td>
                    <td className="px-4 py-4 text-gray-400 text-xs hidden lg:table-cell">
                      {lic.lastSeen ? new Date(lic.lastSeen).toLocaleDateString() : <span className="text-gray-300">—</span>}
                    </td>
                    <td className="px-4 py-4">
                      <LicenseBadge lic={lic} />
                    </td>
                    <td className="px-4 py-4 text-right" onClick={e => e.stopPropagation()}>
                      <div className="flex gap-3 justify-end">
                        <button
                          onClick={() => { setSelected(lic); setModal('detail') }}
                          className="text-xs text-zinc-900 hover:underline font-medium"
                        >
                          View
                        </button>
                        {!lic.revoked && (
                          <button
                            onClick={() => { setSelected(lic); setModal('renew') }}
                            className="text-xs text-amber-600 hover:underline font-medium"
                          >
                            Renew
                          </button>
                        )}
                        {!lic.revoked && !lic.isExpired && (
                          <button
                            onClick={() => handleRevoke(lic)}
                            className="text-xs text-red-500 hover:underline font-medium"
                          >
                            Revoke
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>

      {modal === 'issue' && (
        <IssueLicenseModal
          appIds={appIds}
          features={features}
          onClose={() => setModal(null)}
          onIssued={() => { setModal(null); fetchLicenses() }}
        />
      )}

      {modal === 'detail' && selected && (
        <LicenseDetailModal
          license={selected}
          onClose={() => { setModal(null); setSelected(null) }}
          onRevoke={() => handleRevoke(selected)}
          onRenew={() => setModal('renew')}
        />
      )}

      {modal === 'renew' && selected && (
        <RenewLicenseModal
          license={selected}
          onClose={() => { setModal(null); setSelected(null) }}
          onRenewed={() => { setModal(null); setSelected(null); fetchLicenses() }}
        />
      )}
    </>
  )
}

function LicenseBadge({ lic }) {
  if (lic.revoked)               return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-700">Revoked</span>
  if (lic.isExpired)             return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-600">Expired</span>
  if (lic.daysUntilExpiry <= 30) return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-orange-100 text-orange-700">Expiring Soon</span>
  return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-green-100 text-green-700">Active</span>
}
