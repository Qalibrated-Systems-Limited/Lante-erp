import { useState, useEffect } from 'react'
import { expiryState } from '../../utils/expiry.js'
import { useParams, useNavigate } from 'react-router-dom'
import { Car, AlertTriangle, Camera, Map } from 'lucide-react'
import api from '../../api/axios.js'
import Collapsible from '../../components/Collapsible.jsx'

const STATUS_BADGE = {
  Available: 'bg-green-100 text-green-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  UnderMaintenance: 'bg-amber-100 text-amber-700',
  Decommissioned: 'bg-gray-100 text-gray-500',
}

const DISPATCH_STATUS_BADGE = {
  Pending: 'bg-amber-100 text-amber-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  Returned: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-600',
  Cancelled: 'bg-gray-100 text-gray-500',
}

const TABS = [
  { id: 'overview', label: 'Overview' },
  { id: 'photos', label: 'Photos' },
  { id: 'history', label: 'Dispatch History' },
]

function fmt(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-KE', { day: 'numeric', month: 'short', year: 'numeric' })
}

function fmtDt(d) {
  if (!d) return '—'
  return new Date(d).toLocaleString('en-KE', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function FieldVehicleDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [vehicle, setVehicle] = useState(null)
  const [loading, setLoading] = useState(true)
  const [tab, setTab] = useState('overview')

  const [photos, setPhotos] = useState([])
  const [photosLoading, setPhotosLoading] = useState(false)

  const [dispatches, setDispatches] = useState([])
  const [dispatchesLoading, setDispatchesLoading] = useState(false)

  useEffect(() => {
    setLoading(true)
    api.get(`/api/v1/fleet/field-vehicles/${id}`)
      .then(res => setVehicle(res.data?.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [id])

  useEffect(() => {
    if (tab !== 'photos') return
    setPhotosLoading(true)
    api.get(`/api/v1/FieldVehiclePhotos/field-vehicle/${id}`)
      .then(res => setPhotos(res.data?.data ?? []))
      .catch(() => {})
      .finally(() => setPhotosLoading(false))
  }, [tab, id])

  useEffect(() => {
    if (tab !== 'history') return
    setDispatchesLoading(true)
    api.get(`/api/v1/fleet/field-vehicles/${id}/dispatches`)
      .then(res => setDispatches(res.data?.data ?? []))
      .catch(() => {})
      .finally(() => setDispatchesLoading(false))
  }, [tab, id])

  if (loading) {
    return (
      <>
        <div className="flex items-center justify-center min-h-[60vh] text-gray-400 text-sm">Loading…</div>
      </>
    )
  }

  if (!vehicle) {
    return (
      <>
        <div className="flex flex-col items-center justify-center min-h-[60vh] text-gray-400">
          <p className="font-medium">Vehicle not found.</p>
          <button
            onClick={() => navigate('/modules/fleet/field-vehicles')}
            className="mt-4 text-amber-600 text-sm hover:underline"
          >
            Back to Field Vehicles
          </button>
        </div>
      </>
    )
  }

  const today = new Date()
  // InsuranceExpiry is free text on the server, so `new Date(v) < today` failed OPEN: an unparseable
  // value produced an Invalid Date, every comparison came back false, and the vehicle read as
  // insured. "31/01/2020" — six years expired, in the format a Kenyan user types — showed no warning
  // at all. expiryState reports that as `unknown` rather than folding it into "fine".
  const insurance = expiryState(vehicle.insuranceExpiry, today)
  const insuranceExpired = insurance.state === 'expired'
  const insuranceUnreadable = insurance.state === 'unknown'

  // Mileage decides due/not-due outright once ServiceIntervalKm is configured — a heavily-used
  // vehicle can blow past its interval in days while the calendar date still looks fine. Date is
  // only a fallback for vehicles that haven't been set up with mileage tracking yet.
  const mileageTracked = vehicle.nextServiceOdometer != null
  let serviceOverdue = false
  let serviceSoon = false
  if (mileageTracked) {
    serviceOverdue = vehicle.isServiceDueByMileage === true
  } else {
    const nextServiceDate = vehicle.nextServiceDate ? new Date(vehicle.nextServiceDate) : null
    serviceOverdue = nextServiceDate && nextServiceDate < today
    serviceSoon = nextServiceDate && !serviceOverdue && (nextServiceDate - today) < 30 * 24 * 60 * 60 * 1000
  }

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
        {/* Breadcrumb */}
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
          <button
            onClick={() => navigate('/modules/fleet/field-vehicles')}
            className="hover:text-amber-600 transition-colors"
          >
            Field Vehicles
          </button>
          <span>/</span>
          <span className="text-gray-900 font-medium">{vehicle.registrationNumber}</span>
        </div>

        <Collapsible title="About this vehicle" dismissKey="fleet.pageInfo.fieldVehicleDetail.dismissed">
          <p className="text-sm text-gray-700">
            Service, insurance, and dispatch history for this vehicle — the alerts above flag anything overdue or expiring soon.
          </p>
        </Collapsible>

        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 mb-6">
          <div className="flex items-start gap-4">
            <div className="w-16 h-16 rounded-2xl bg-amber-50 border border-amber-100 flex items-center justify-center text-amber-600 flex-shrink-0">
              <Car size={28} />
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-2xl font-bold text-gray-900 font-mono">{vehicle.registrationNumber}</h1>
                <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${STATUS_BADGE[vehicle.status] ?? 'bg-gray-100 text-gray-500'}`}>
                  {vehicle.status === 'UnderMaintenance' ? 'Under Maintenance' : vehicle.status}
                </span>
              </div>
              <p className="text-gray-600 mt-0.5 text-sm">
                {vehicle.year} {vehicle.make} {vehicle.model}
                {vehicle.color ? ` · ${vehicle.color}` : ''}
                {vehicle.type && !vehicle.model?.toLowerCase().includes(vehicle.type.toLowerCase()) ? ` · ${vehicle.type}` : ''}
              </p>
            </div>
          </div>

          {/* Status alerts */}
          {(serviceOverdue || serviceSoon || insuranceExpired || insuranceUnreadable) && (
            <div className="flex flex-col gap-1.5">
              {serviceOverdue && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium bg-red-50 text-red-700 border border-red-100 rounded-lg px-2.5 py-1">
                  <AlertTriangle size={14} /> Service overdue
                </span>
              )}
              {serviceSoon && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium bg-amber-50 text-amber-700 border border-amber-100 rounded-lg px-2.5 py-1">
                  <AlertTriangle size={14} /> Service due soon
                </span>
              )}
              {insuranceExpired && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium bg-red-50 text-red-700 border border-red-100 rounded-lg px-2.5 py-1">
                  <AlertTriangle size={14} /> Insurance expired
                </span>
              )}
              {/* Amber, not red: this does not claim the cover has lapsed, only that the recorded
                  date cannot be read — the state that previously showed nothing at all. */}
              {insuranceUnreadable && (
                <span className="inline-flex items-center gap-1.5 text-xs font-medium bg-amber-50 text-amber-700 border border-amber-100 rounded-lg px-2.5 py-1">
                  <AlertTriangle size={14} /> Insurance expiry unreadable — check the record
                </span>
              )}
            </div>
          )}
        </div>

        {/* Tabs */}
        <div className="flex gap-1 mb-6 border-b border-gray-200">
          {TABS.map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`px-4 py-2.5 text-sm font-medium rounded-t-lg transition-colors -mb-px ${
                tab === t.id
                  ? 'text-amber-600 border-b-2 border-amber-500'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>

        {/* ── Overview ── */}
        {tab === 'overview' && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div className="bg-white border border-gray-200 rounded-2xl p-5">
              <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-4">Vehicle Details</h2>
              <dl className="space-y-3">
                <Row label="Registration" value={vehicle.registrationNumber} mono />
                <Row label="Make / Model" value={`${vehicle.make} ${vehicle.model}`} />
                <Row label="Year" value={vehicle.year} />
                <Row label="Type" value={vehicle.type} />
                {vehicle.color && <Row label="Color" value={vehicle.color} />}
                <Row label="Odometer" value={`${Number(vehicle.currentOdometer).toLocaleString()} km`} />
              </dl>
            </div>

            <div className="bg-white border border-gray-200 rounded-2xl p-5">
              <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-4">Service &amp; Insurance</h2>
              <dl className="space-y-3">
                <Row label="Last Service" value={fmt(vehicle.lastServiceDate)} />
                <Row
                  label="Last Service Odo"
                  value={vehicle.lastServiceOdometer != null ? `${Number(vehicle.lastServiceOdometer).toLocaleString()} km` : '—'}
                />
                <Row
                  label="Next Service"
                  value={mileageTracked
                    ? `${Number(vehicle.nextServiceOdometer).toLocaleString()} km${serviceOverdue ? ' (due now)' : ''}`
                    : fmt(vehicle.nextServiceDate)}
                  highlight={serviceOverdue ? 'red' : serviceSoon ? 'amber' : null}
                />
                <Row
                  label="Insurance Expiry"
                  value={vehicle.insuranceExpiry ? fmt(vehicle.insuranceExpiry) : '—'}
                  highlight={insuranceExpired ? 'red' : insuranceUnreadable ? 'amber' : null}
                />
              </dl>
            </div>

            {vehicle.notes && (
              <div className="md:col-span-2 bg-white border border-gray-200 rounded-2xl p-5">
                <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Notes</h2>
                <p className="text-sm text-gray-600 whitespace-pre-line">{vehicle.notes}</p>
              </div>
            )}

            <div className="md:col-span-2 bg-gray-50 border border-gray-100 rounded-2xl px-4 py-3">
              <div className="flex flex-wrap gap-x-8 text-xs text-gray-400">
                <span>Added {fmt(vehicle.createdAt)}</span>
                <span>Updated {fmt(vehicle.updatedAt)}</span>
              </div>
            </div>
          </div>
        )}

        {/* ── Photos ── */}
        {tab === 'photos' && (
          <div>
            {photosLoading ? (
              <div className="text-gray-400 text-sm py-16 text-center">Loading photos…</div>
            ) : photos.length === 0 ? (
              <div className="flex flex-col items-center py-24 text-gray-400">
                <div className="flex justify-center mb-3"><Camera size={48} /></div>
                <p className="font-medium">No photos uploaded</p>
                <p className="text-sm mt-1">Edit this vehicle to add photos.</p>
              </div>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                {photos.map(p => (
                  <a key={p.id} href={p.photoUrl} target="_blank" rel="noopener noreferrer">
                    <img
                      src={p.photoUrl}
                      alt={p.caption ?? 'Vehicle photo'}
                      className="w-full aspect-square object-cover rounded-xl border border-gray-100 hover:opacity-90 transition-opacity"
                      onError={e => { e.target.parentElement.style.display = 'none' }}
                    />
                  </a>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ── Dispatch History ── */}
        {tab === 'history' && (
          <div>
            {dispatchesLoading ? (
              <div className="text-gray-400 text-sm py-16 text-center">Loading dispatch history…</div>
            ) : dispatches.length === 0 ? (
              <div className="flex flex-col items-center py-24 text-gray-400">
                <div className="flex justify-center mb-3"><Map size={48} /></div>
                <p className="font-medium">No dispatch records yet</p>
                <p className="text-sm mt-1">This vehicle hasn&apos;t been dispatched on any assignment.</p>
              </div>
            ) : (
              <div className="space-y-3">
                <p className="text-sm text-gray-500 mb-1">{dispatches.length} dispatch record{dispatches.length !== 1 ? 's' : ''}</p>
                {dispatches.map(d => {
                  const km = d.returnOdometer != null && d.departureOdometer != null
                    ? Number(d.returnOdometer) - Number(d.departureOdometer)
                    : null
                  const totalLitres = d.fuelLogs?.reduce((s, f) => s + Number(f.amountLitres), 0) ?? 0
                  const totalCost = d.fuelLogs?.reduce((s, f) => s + Number(f.costKes), 0) ?? 0
                  return (
                    <div key={d.id} className="bg-white border border-gray-200 rounded-2xl p-5">
                      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-2 mb-4">
                        <div>
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="font-semibold text-gray-900">{d.assignmentTitle || 'Assignment'}</span>
                            <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${DISPATCH_STATUS_BADGE[d.status] ?? 'bg-gray-100 text-gray-500'}`}>
                              {d.status}
                            </span>
                          </div>
                          <p className="text-xs text-gray-500 mt-0.5">
                            Driver: <span className="font-medium text-gray-700">{d.driverName}</span>
                          </p>
                        </div>
                        <div className="text-right text-xs text-gray-400 flex-shrink-0">
                          <div>{fmtDt(d.departureDatetime)}</div>
                          {d.returnDatetime && <div className="mt-0.5">→ {fmtDt(d.returnDatetime)}</div>}
                        </div>
                      </div>

                      <div className="flex flex-wrap gap-3">
                        <Stat label="Departure Odo" value={`${Number(d.departureOdometer).toLocaleString()} km`} />
                        {d.returnOdometer != null && (
                          <Stat label="Return Odo" value={`${Number(d.returnOdometer).toLocaleString()} km`} />
                        )}
                        {km != null && km > 0 && (
                          <Stat label="Distance" value={`${km.toLocaleString()} km`} highlight />
                        )}
                        {d.fuelLevelOut && <Stat label="Fuel Out" value={d.fuelLevelOut} />}
                        {d.fuelLevelIn && <Stat label="Fuel In" value={d.fuelLevelIn} />}
                        {totalLitres > 0 && (
                          <Stat label="Refuelled" value={`${totalLitres.toFixed(1)} L`} />
                        )}
                        {totalCost > 0 && (
                          <Stat label="Fuel Cost" value={`KES ${totalCost.toLocaleString()}`} />
                        )}
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        )}
      </main>
    </>
  )
}

function Row({ label, value, mono, highlight }) {
  const valueClass = highlight === 'red'
    ? 'text-red-600 font-medium'
    : highlight === 'amber'
    ? 'text-amber-600 font-medium'
    : 'text-gray-900'
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-xs text-gray-500 flex-shrink-0">{label}</dt>
      <dd className={`text-sm text-right ${valueClass} ${mono ? 'font-mono font-semibold' : ''}`}>{value ?? '—'}</dd>
    </div>
  )
}

function Stat({ label, value, highlight }) {
  return (
    <div className="bg-gray-50 rounded-xl px-3 py-2">
      <div className="text-xs text-gray-400">{label}</div>
      <div className={`text-sm font-semibold mt-0.5 ${highlight ? 'text-amber-700' : 'text-gray-800'}`}>{value}</div>
    </div>
  )
}
