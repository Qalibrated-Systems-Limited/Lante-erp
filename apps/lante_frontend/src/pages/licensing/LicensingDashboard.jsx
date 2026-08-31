import { useState, useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { licensingApi } from '../../api/licensingApi.js'
import { featureLabel } from '../../utils/featureCatalogue.js'

const APP_COLORS = [
  'bg-amber-500', 'bg-zinc-900', 'bg-purple-500', 'bg-green-500',
  'bg-blue-500',  'bg-rose-500', 'bg-orange-500', 'bg-teal-500',
]

export default function LicensingDashboard() {
  const navigate = useNavigate()
  const [licenses, setLicenses]   = useState([])
  const [expiring, setExpiring]   = useState([])
  const [loading, setLoading]     = useState(true)

  useEffect(() => {
    async function load() {
      try {
        const [allRes, expRes] = await Promise.allSettled([
          licensingApi.getAll({ pageSize: 500 }),
          licensingApi.getExpiring(30),
        ])
        if (allRes.status === 'fulfilled')
          setLicenses(allRes.value.data?.data ?? [])
        if (expRes.status === 'fulfilled')
          setExpiring(expRes.value.data?.data ?? [])
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [])

  const total      = licenses.length
  const active     = licenses.filter(l => !l.revoked && !l.isExpired).length
  const revoked    = licenses.filter(l => l.revoked).length
  const expired    = licenses.filter(l => !l.revoked && l.isExpired).length

  // Group by appId
  const appGroups = licenses.reduce((acc, l) => {
    acc[l.appId] = (acc[l.appId] || 0) + 1
    return acc
  }, {})
  const appEntries = Object.entries(appGroups).sort((a, b) => b[1] - a[1])

  const recent = [...licenses]
    .sort((a, b) => new Date(b.issuedAt) - new Date(a.issuedAt))
    .slice(0, 8)

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">

        {/* Header */}
        <div className="flex items-center justify-between mb-7">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">Licensing</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Overview of issued licenses across all Qalibrated apps
            </p>
          </div>
          <div className="flex gap-2">
            <Link
              to="/modules/licensing/list"
              className="inline-flex items-center gap-2 px-4 py-2.5 border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-lg transition-colors"
            >
              View All Licenses
            </Link>
            <button
              onClick={() => navigate('/modules/licensing/list?issue=1')}
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Issue License
            </button>
          </div>
        </div>

        {loading ? <LoadingSkeleton /> : (
          <>
            {/* Expiring banner */}
            {expiring.length > 0 && (
              <div className="mb-6 flex items-center gap-3 bg-orange-50 border border-orange-200 rounded-xl px-5 py-3.5 text-sm text-orange-800">
                <svg className="w-5 h-5 text-orange-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                </svg>
                <span>
                  <strong>{expiring.length} license{expiring.length > 1 ? 's' : ''}</strong> expiring within 30 days.{' '}
                  <Link to="/modules/licensing/list" className="underline font-semibold hover:text-orange-900">
                    Review now →
                  </Link>
                </span>
              </div>
            )}

            {/* KPI row */}
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <KPICard label="Total Licenses" value={total}   icon="🔑" sub="across all apps"       subColor="text-gray-500" onClick={() => navigate('/modules/licensing/list')} />
              <KPICard label="Active"          value={active}  icon="✅" sub="valid & not revoked"   subColor="text-green-600" />
              <KPICard label="Expiring Soon"   value={expiring.length} icon="⏳" sub="within 30 days" subColor={expiring.length > 0 ? 'text-orange-500' : 'text-gray-500'} />
              <KPICard label="Revoked"         value={revoked} icon="🚫" sub={expired > 0 ? `+ ${expired} expired` : 'no expired licenses'} subColor={revoked > 0 ? 'text-red-500' : 'text-gray-500'} />
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
              {/* By App */}
              <div className="lg:col-span-2 bg-white rounded-xl border border-gray-200 p-5">
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Licenses by App</h2>
                  <Link to="/modules/licensing/list" className="text-xs font-medium text-amber-600 hover:text-amber-800">
                    View all →
                  </Link>
                </div>
                {appEntries.length === 0 ? (
                  <p className="text-sm text-gray-400 py-4 text-center">No licenses issued yet.</p>
                ) : (
                  <div className="space-y-3">
                    {appEntries.map(([appId, count], i) => (
                      <div key={appId}>
                        <div className="flex items-center justify-between mb-1">
                          <span className="text-sm font-mono text-gray-700 bg-gray-100 px-2 py-0.5 rounded text-xs">
                            {appId}
                          </span>
                          <span className="text-xs font-semibold text-gray-600">{count}</span>
                        </div>
                        <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                          <div
                            className={`h-full rounded-full transition-all ${APP_COLORS[i % APP_COLORS.length]}`}
                            style={{ width: total ? `${(count / total) * 100}%` : '0%' }}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Status summary */}
              <div className="bg-white rounded-xl border border-gray-200 p-5">
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Status Breakdown</h2>
                </div>
                <div className="space-y-3">
                  {[
                    { label: 'Active',        count: active,          cls: 'bg-green-100 text-green-700' },
                    { label: 'Expiring Soon', count: expiring.length, cls: 'bg-orange-100 text-orange-700' },
                    { label: 'Expired',       count: expired,         cls: 'bg-gray-100 text-gray-600' },
                    { label: 'Revoked',       count: revoked,         cls: 'bg-red-100 text-red-700' },
                  ].map(s => (
                    <div key={s.label} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                      <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${s.cls}`}>{s.label}</span>
                      <span className="text-sm font-bold text-gray-900">{s.count}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Recent licenses */}
            <div className="bg-white rounded-xl border border-gray-200 p-5">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Recently Issued</h2>
                <Link to="/modules/licensing/list" className="text-xs font-medium text-amber-600 hover:text-amber-800">
                  View all →
                </Link>
              </div>
              {recent.length === 0 ? (
                <div className="text-center py-8">
                  <p className="text-sm text-gray-400 mb-3">No licenses issued yet.</p>
                  <Link to="/modules/licensing/list?issue=1" className="text-sm font-medium text-amber-600 hover:text-amber-800">
                    Issue your first license →
                  </Link>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-gray-100">
                        <th className="text-left pb-2 pr-4 text-xs font-semibold text-gray-500">Customer</th>
                        <th className="text-left pb-2 pr-4 text-xs font-semibold text-gray-500">App</th>
                        <th className="text-left pb-2 pr-4 text-xs font-semibold text-gray-500 hidden sm:table-cell">Features</th>
                        <th className="text-left pb-2 pr-4 text-xs font-semibold text-gray-500 hidden md:table-cell">Expires</th>
                        <th className="text-left pb-2 text-xs font-semibold text-gray-500">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {recent.map(lic => (
                        <tr
                          key={lic.id}
                          onClick={() => navigate('/modules/licensing/list')}
                          className="hover:bg-amber-50 cursor-pointer transition-colors"
                        >
                          <td className="py-3 pr-4">
                            <p className="font-medium text-gray-900">{lic.customerName || lic.customerId}</p>
                            <p className="text-xs text-gray-400">{lic.customerId}</p>
                          </td>
                          <td className="py-3 pr-4">
                            <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded">{lic.appId}</span>
                          </td>
                          <td className="py-3 pr-4 hidden sm:table-cell">
                            <div className="flex flex-wrap gap-1">
                              {(lic.features || []).map(f => (
                                <span key={f} className="text-xs bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded">{featureLabel(f)}</span>
                              ))}
                            </div>
                          </td>
                          <td className="py-3 pr-4 text-gray-500 hidden md:table-cell">
                            {new Date(lic.expiresAt).toLocaleDateString()}
                          </td>
                          <td className="py-3">
                            <LicenseBadge lic={lic} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </main>
    </>
  )
}

function LicenseBadge({ lic }) {
  if (lic.revoked)              return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-700">Revoked</span>
  if (lic.isExpired)            return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-600">Expired</span>
  if (lic.daysUntilExpiry <= 30) return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-orange-100 text-orange-700">Expiring Soon</span>
  return <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-green-100 text-green-700">Active</span>
}

function KPICard({ label, value, icon, sub, subColor, onClick }) {
  return (
    <div
      onClick={onClick}
      className={`bg-white rounded-xl border border-gray-200 p-5 ${onClick ? 'cursor-pointer hover:border-amber-300 hover:shadow-sm transition-all' : ''}`}
    >
      <div className="flex items-start justify-between mb-3">
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">{label}</p>
        <span className="text-2xl">{icon}</span>
      </div>
      <p className="text-2xl font-extrabold text-zinc-950 mb-1">{value}</p>
      {sub && <p className={`text-xs font-medium ${subColor ?? 'text-gray-400'}`}>{sub}</p>}
    </div>
  )
}

function LoadingSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[1,2,3,4].map(i => <div key={i} className="h-28 bg-white rounded-xl border border-gray-100 animate-pulse" />)}
      </div>
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 h-56 bg-white rounded-xl border border-gray-100 animate-pulse" />
        <div className="h-56 bg-white rounded-xl border border-gray-100 animate-pulse" />
      </div>
      <div className="h-64 bg-white rounded-xl border border-gray-100 animate-pulse" />
    </div>
  )
}
