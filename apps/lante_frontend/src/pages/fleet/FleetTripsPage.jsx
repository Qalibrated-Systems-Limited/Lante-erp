import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Download, Truck } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel, TRIP_COLUMNS } from '../../utils/export.js'
import Collapsible from '../../components/Collapsible.jsx'
import Pagination from '../../components/Pagination.jsx'

const FILTERS = ['All', 'Pending', 'InProgress', 'Completed', 'Cancelled']
const EXPORT_CAP = 1000

const STATUS_COLORS = {
  Pending:    { badge: 'bg-blue-100 text-blue-700',   label: 'Pending' },
  InProgress: { badge: 'bg-amber-100 text-amber-700', label: 'In Progress' },
  Completed:  { badge: 'bg-green-100 text-green-700', label: 'Completed' },
  Cancelled:  { badge: 'bg-red-100 text-red-700',     label: 'Cancelled' },
}

function fmt(n) {
  return n > 0
      ? new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n)
      : '—'
}

// A trip is "Banked" once its driver has recorded an M-Pesa deposit against it
// (Fleet > Record Banking, mobile-only for now). Only meaningful for Completed
// trips — Pending/Cancelled trips have nothing to bank yet.
function bankedLabel(trip, depositedTripIds) {
  if (depositedTripIds.has(trip.id)) return 'Yes'
  return trip.status === 'Completed' ? 'No' : '—'
}

function exportCsv(trips, driverMap = {}, truckMap = {}, depositedTripIds = new Set()) {
  const headers = ['Date', 'From', 'To', 'Trip Type', 'Driver', 'Truck', 'Status', 'Mileage (km)', 'Revenue (KES)', 'Cost (KES)', 'Profit (KES)', 'Banked']
  const rows = trips.map(t => [
    new Date(t.date).toLocaleDateString(),
    t.startLocation ?? '',
    t.endLocation ?? '',
    t.tripTypeName ?? '',
    driverMap[t.driverId] ?? t.driverName ?? '—',
    truckMap[t.truckId] ?? t.licensePlate ?? t.truckPlate ?? '—',
    t.status ?? '',
    t.totalMileage ?? '',
    t.revenue ?? 0,
    t.totalCost ?? 0,
    t.profit ?? 0,
    bankedLabel(t, depositedTripIds),
  ])
  const csv = [headers, ...rows].map(r => r.map(v => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `fleet-trips-${new Date().toISOString().slice(0,10)}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

export default function FleetTripsPage() {
  const navigate = useNavigate()

  const [trips, setTrips]           = useState([])
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')

  const [page, setPage]             = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  // Filters
  const [search, setSearch]             = useState('')
  const [activeFilter, setActiveFilter] = useState('All')
  const [tripTypeFilter, setTripTypeFilter] = useState('')
  const [startDate, setStartDate]       = useState('')
  const [endDate, setEndDate]           = useState('')

  const PAGE_SIZE = 20

  const [driverMap, setDriverMap] = useState({}) // driverId -> fullName
  const [truckMap, setTruckMap]   = useState({}) // truckId  -> "licensePlate — model"
  const [depositedTripIds, setDepositedTripIds] = useState(new Set()) // tripId -> has a recorded bank deposit
  const fetchedDriverIds = useRef(new Set())
  const fetchedTruckIds  = useRef(new Set())

  // Loaded once — used to compute the "Banked" column on-screen and in exports.
  useEffect(() => {
    api.get('/api/v1/tripdeposits')
      .then(res => setDepositedTripIds(new Set((res.data?.data ?? []).map(d => d.tripId))))
      .catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      const params = { pageNumber: page, pageSize: PAGE_SIZE }
      if (activeFilter !== 'All') params.status = activeFilter

      const res = await api.get('/api/v1/Trips', { params })
      const data = res.data?.data || {}

      setTrips(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } catch (err) {
      setError('Failed to load trips.')
    } finally {
      setLoading(false)
    }
  }, [page, activeFilter])

  useEffect(() => {
    load()
  }, [load])

  // Resolve driver names and truck labels from IDs (same approach as TripDetailPage)
  useEffect(() => {
    if (!trips.length) return

    const newDriverIds = [...new Set(trips.map(t => t.driverId).filter(Boolean))]
      .filter(id => !fetchedDriverIds.current.has(id))
    const newTruckIds = [...new Set(trips.map(t => t.truckId).filter(Boolean))]
      .filter(id => !fetchedTruckIds.current.has(id))

    newDriverIds.forEach(id => {
      fetchedDriverIds.current.add(id)
      api.get(`/api/v1/DriverProfiles/driver/${id}/current`)
        .then(res => {
          const name = res.data?.data?.fullName
          if (name) setDriverMap(prev => ({ ...prev, [id]: name }))
        })
        .catch(() => {})
    })

    newTruckIds.forEach(id => {
      fetchedTruckIds.current.add(id)
      api.get(`/api/v1/trucks/${id}`)
        .then(res => {
          const t = res.data?.data
          if (t) setTruckMap(prev => ({ ...prev, [id]: `${t.licensePlate} — ${t.model}` }))
        })
        .catch(() => {})
    })
  }, [trips])

  // Combined Filtering
  const filteredTrips = useMemo(() => {
    let result = [...trips]

    if (search.trim()) {
      const term = search.toLowerCase()
      result = result.filter(t => {
        const driver = driverMap[t.driverId] ?? t.driverName ?? ''
        const truck  = truckMap[t.truckId]   ?? t.licensePlate ?? t.truckPlate ?? ''
        return (
          t.startLocation?.toLowerCase().includes(term) ||
          t.endLocation?.toLowerCase().includes(term) ||
          t.tripTypeName?.toLowerCase().includes(term) ||
          driver.toLowerCase().includes(term) ||
          truck.toLowerCase().includes(term)
        )
      })
    }

    if (tripTypeFilter.trim()) {
      result = result.filter(t =>
          t.tripTypeName?.toLowerCase().includes(tripTypeFilter.toLowerCase())
      )
    }

    if (startDate) {
      result = result.filter(t => new Date(t.date) >= new Date(startDate))
    }
    if (endDate) {
      result = result.filter(t => new Date(t.date) <= new Date(endDate))
    }

    return result
  }, [trips, search, tripTypeFilter, startDate, endDate, driverMap, truckMap])

  // Reset page when filters change
  useEffect(() => {
    setPage(1)
  }, [activeFilter, search, tripTypeFilter, startDate, endDate])

  // Export — fetches up to EXPORT_CAP matching the server-side status filter, then
  // applies the same client-side search/tripType/date filters as the table, and
  // resolves driver/truck labels (reusing the same id->label maps as the table).
  const [exporting, setExporting] = useState(false)

  async function fetchExportRows() {
    const res = await api.get('/api/v1/Trips', {
      params: { pageNumber: 1, pageSize: EXPORT_CAP, status: activeFilter !== 'All' ? activeFilter : undefined }
    })
    let rows = res.data?.data?.items ?? []
    const totalMatchingFilter = res.data?.data?.totalCount ?? rows.length

    if (search.trim()) {
      const term = search.toLowerCase()
      rows = rows.filter(t =>
          t.startLocation?.toLowerCase().includes(term) ||
          t.endLocation?.toLowerCase().includes(term) ||
          t.tripTypeName?.toLowerCase().includes(term) ||
          t.driverName?.toLowerCase().includes(term) ||
          t.licensePlate?.toLowerCase().includes(term)
      )
    }
    if (tripTypeFilter.trim()) {
      rows = rows.filter(t => t.tripTypeName?.toLowerCase().includes(tripTypeFilter.toLowerCase()))
    }
    if (startDate) rows = rows.filter(t => new Date(t.date) >= new Date(startDate))
    if (endDate) rows = rows.filter(t => new Date(t.date) <= new Date(endDate))

    const fullTruckMap = { ...truckMap }
    const fullDriverMap = { ...driverMap }
    const missingTruckIds = [...new Set(rows.map(t => t.truckId).filter(Boolean))].filter(id => !fullTruckMap[id])
    const missingDriverIds = [...new Set(rows.map(t => t.driverId).filter(Boolean))].filter(id => !fullDriverMap[id])

    await Promise.all([
      ...missingTruckIds.map(id =>
        api.get(`/api/v1/trucks/${id}`)
          .then(r => { const t = r.data?.data; if (t) fullTruckMap[id] = `${t.licensePlate} — ${t.model}` })
          .catch(() => {})
      ),
      ...missingDriverIds.map(id =>
        api.get(`/api/v1/DriverProfiles/driver/${id}/current`)
          .then(r => { const name = r.data?.data?.fullName; if (name) fullDriverMap[id] = name })
          .catch(() => {})
      ),
    ])

    const normalized = rows.map(t => ({
      ...t,
      driverLabel: fullDriverMap[t.driverId] ?? t.driverName ?? '—',
      truckLabel: fullTruckMap[t.truckId] ?? t.licensePlate ?? t.truckPlate ?? '—',
      statusLabel: STATUS_COLORS[t.status]?.label ?? t.status,
      bankedLabel: bankedLabel(t, depositedTripIds),
    }))

    return { rows: normalized, capped: totalMatchingFilter > (res.data?.data?.items?.length ?? 0), total: totalMatchingFilter }
  }

  const handleExportPdf = async () => {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: "Fleet Trips",
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching trips` : `${rows.length} matching trip${rows.length !== 1 ? 's' : ''}`,
        columns: TRIP_COLUMNS,
        rows,
        filename: `Fleet-Trips-${new Date().toISOString().slice(0,10)}`,
        theme: 'navy',
        docModule: 'FLEET',
      })
    } catch {
      setError('Failed to export PDF.')
    } finally {
      setExporting(false)
    }
  }

  const handleExportExcel = async () => {
    setExporting(true)
    try {
      const { rows } = await fetchExportRows()
      exportToExcel({
        title: "Fleet Trips",
        columns: TRIP_COLUMNS,
        rows,
        filename: `Fleet-Trips-${new Date().toISOString().slice(0,10)}`,
        sheetName: "Trips"
      })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  return (
      <>
        <main className="w-full px-4 sm:px-6 py-6 space-y-4">
          <FleetNav />

          <Collapsible title="About Trips" dismissKey="fleet.pageInfo.trips.dismissed">
            <p className="text-sm text-gray-700">
              Trips are the core Fleet record — each links a driver, truck, and trip type, and tracks revenue/cost for profit reporting.
            </p>
          </Collapsible>

          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-extrabold text-navy">Fleet Trips</h1>
              <p className="text-sm text-gray-400 mt-0.5">
                {loading ? 'Loading…' : `${totalCount} total trips`}
              </p>
            </div>

            <div className="flex items-center gap-2">
              <button onClick={() => navigate('/modules/fleet/trips/new')}
                      className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors shadow">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                New Trip
              </button>

              <button onClick={handleExportExcel} disabled={exporting}
                      className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50">
                <Download size={14} /> Excel
              </button>

              <button onClick={handleExportPdf} disabled={exporting}
                      className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50">
                <Download size={14} /> PDF
              </button>

              <button onClick={async () => {
                const fullTruckMap  = { ...truckMap }
                const fullDriverMap = { ...driverMap }
                await Promise.all([
                  ...[...new Set(filteredTrips.map(t => t.truckId).filter(Boolean))].filter(id => !fullTruckMap[id]).map(id =>
                    api.get(`/api/v1/trucks/${id}`).then(r => { const t = r.data?.data; if (t) fullTruckMap[id] = `${t.licensePlate} — ${t.model}` }).catch(() => {})
                  ),
                  ...[...new Set(filteredTrips.map(t => t.driverId).filter(Boolean))].filter(id => !fullDriverMap[id]).map(id =>
                    api.get(`/api/v1/DriverProfiles/driver/${id}/current`).then(r => { const name = r.data?.data?.fullName; if (name) fullDriverMap[id] = name }).catch(() => {})
                  ),
                ])
                exportCsv(filteredTrips, fullDriverMap, fullTruckMap, depositedTripIds)
              }} disabled={filteredTrips.length === 0}
                      className="inline-flex items-center gap-2 px-4 py-2 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors disabled:opacity-40">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
                Export CSV
              </button>
            </div>
          </div>

          {/* Filters */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-3">
            <div className="relative">
              <input
                  type="text"
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  placeholder="Search route or driver..."
                  className="w-full pl-10 py-2.5 bg-gray-100 rounded-xl text-sm border border-gray-200 focus:ring-2 focus:ring-gold"
              />
              <div className="absolute left-3.5 top-3 text-gray-400">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </div>
            </div>

            <select value={activeFilter} onChange={e => setActiveFilter(e.target.value)}
                    className="px-4 py-2.5 bg-gray-100 rounded-xl text-sm border border-gray-200 focus:ring-2 focus:ring-gold">
              {FILTERS.map(f => (
                  <option key={f} value={f}>
                    {f === 'InProgress' ? 'In Progress' : f}
                  </option>
              ))}
            </select>

            <input
                type="text"
                value={tripTypeFilter}
                onChange={e => setTripTypeFilter(e.target.value)}
                placeholder="Trip Type"
                className="px-4 py-2.5 bg-gray-100 rounded-xl text-sm border border-gray-200 focus:ring-2 focus:ring-gold"
            />

            <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)}
                   className="px-4 py-2.5 bg-gray-100 rounded-xl text-sm border border-gray-200 focus:ring-2 focus:ring-gold" />

            <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)}
                   className="px-4 py-2.5 bg-gray-100 rounded-xl text-sm border border-gray-200 focus:ring-2 focus:ring-gold" />
          </div>

          {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-3 text-sm">{error}</div>}

          {/* Table */}
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Date</th>
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider min-w-[160px]">Route</th>
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Trip Type</th>
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Driver</th>
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Truck</th>
                  <th className="text-left px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Status</th>
                  <th className="text-right px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Mileage</th>
                  <th className="text-right px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Revenue</th>
                  <th className="text-right px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Cost</th>
                  <th className="text-right px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Profit</th>
                  <th className="text-center px-3 py-2.5 font-semibold text-gray-500 uppercase tracking-wider whitespace-nowrap">Banked</th>
                  <th className="px-3 py-2.5 w-16"></th>
                </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                {loading ? (
                    [...Array(5)].map((_, i) => (
                        <tr key={i}>
                          {[...Array(11)].map((_, j) => (
                              <td key={j} className="px-3 py-2.5">
                                <div className="h-3 bg-gray-100 rounded animate-pulse" />
                              </td>
                          ))}
                        </tr>
                    ))
                ) : filteredTrips.length === 0 ? (
                    <tr>
                      <td colSpan={12} className="text-center py-16 text-gray-400">
                        <div className="flex justify-center mb-2 text-gray-400"><Truck size={36} /></div>
                        <p className="text-sm font-medium">No trips found</p>
                      </td>
                    </tr>
                ) : (
                    filteredTrips.map(trip => {
                      const sc = STATUS_COLORS[trip.status] ?? { badge: 'bg-gray-100 text-gray-600', label: trip.status }
                      const profit = trip.profit ?? (trip.revenue - trip.totalCost)

                      return (
                          <tr
                              key={trip.id}
                              onClick={() => navigate(`/modules/fleet/trips/${trip.id}`)}
                              className="hover:bg-offwhite cursor-pointer transition-colors align-top"
                          >
                            <td className="px-3 py-2.5 text-gray-500 whitespace-nowrap">{new Date(trip.date).toLocaleDateString()}</td>
                            <td className="px-3 py-2.5 min-w-[160px]"><span className="font-semibold text-gray-900 leading-snug">{trip.startLocation} → {trip.endLocation}</span></td>
                            <td className="px-3 py-2.5 text-gray-500 whitespace-nowrap">{trip.tripTypeName ?? '—'}</td>
                            <td className="px-3 py-2.5 font-medium whitespace-nowrap">
                              {driverMap[trip.driverId] ?? trip.driverName ?? (trip.driverId ? <span className="font-mono text-gray-400">{trip.driverId.slice(0,8)}…</span> : '—')}
                            </td>
                            <td className="px-3 py-2.5 font-medium whitespace-nowrap">
                              {truckMap[trip.truckId] ?? trip.licensePlate ?? trip.truckPlate ?? (trip.truckId ? <span className="font-mono text-gray-400">{trip.truckId.slice(0,8)}…</span> : '—')}
                            </td>
                            <td className="px-3 py-2.5 whitespace-nowrap">
                              <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${sc.badge}`}>{sc.label}</span>
                            </td>
                            <td className="px-3 py-2.5 text-right text-gray-600 whitespace-nowrap">{trip.totalMileage != null ? `${trip.totalMileage} km` : '—'}</td>
                            <td className="px-3 py-2.5 text-right text-green-700 font-medium whitespace-nowrap">{fmt(trip.revenue)}</td>
                            <td className="px-3 py-2.5 text-right text-gray-700 font-medium whitespace-nowrap">{fmt(trip.totalCost)}</td>
                            <td className={`px-3 py-2.5 text-right font-semibold whitespace-nowrap ${profit >= 0 ? 'text-green-700' : 'text-red-600'}`}>
                              {fmt(profit)}
                            </td>
                            <td className="px-3 py-2.5 text-center whitespace-nowrap">
                              {(() => {
                                const label = bankedLabel(trip, depositedTripIds)
                                const cls = label === 'Yes' ? 'bg-green-100 text-green-700'
                                    : label === 'No' ? 'bg-red-50 text-red-500'
                                    : 'bg-gray-100 text-gray-400'
                                return <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${cls}`}>{label}</span>
                              })()}
                            </td>
                            <td className="px-3 py-2.5 text-right whitespace-nowrap" onClick={e => e.stopPropagation()}>
                              <button
                                  onClick={() => navigate(`/modules/fleet/trips/${trip.id}`)}
                                  className="text-xs px-2.5 py-1 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-medium"
                              >
                                View
                              </button>
                            </td>
                          </tr>
                      )
                    })
                )}
                </tbody>
              </table>
            </div>

            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </div>
        </main>
      </>
  )
}